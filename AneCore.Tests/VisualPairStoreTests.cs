using System.Reflection;
using System.Runtime.CompilerServices;
using Cassandra.Mapping;
using Coflnet.Ane;
using Coflnet.Ane.VisualPairs;

namespace AneCore.Tests;

[TestFixture]
public class VisualPairStoreTests
{
    [Test]
    public void PairId_IsStable_AndMatchesSha256OfSortedKeys()
    {
        // printf 'Ebay:111:0|Vinted:abc:2' | sha256sum | cut -c1-32
        Assert.That(VisualPairId.Compute(Platform.Vinted, "abc", 2, Platform.Ebay, "111", 0),
            Is.EqualTo("10830194fd3adfc8daa133edd979bb5f"));
    }

    [Test]
    public void PairId_DoesNotDependOnOrder_AndDiffersForDifferentPairs()
    {
        var ab = VisualPairId.Compute("Vinted:1:0", "Vinted:2:0");
        Assert.That(VisualPairId.Compute("Vinted:2:0", "Vinted:1:0"), Is.EqualTo(ab));
        Assert.That(ab, Has.Length.EqualTo(32).And.Match("^[0-9a-f]{32}$"));
        Assert.That(VisualPairId.Compute("Vinted:1:0", "Vinted:2:1"), Is.Not.EqualTo(ab));
    }

    [Test]
    public void ShardAndDay_AreStableAndInRange()
    {
        var id = VisualPairId.Compute("a", "b");
        Assert.That(VisualPairId.ShardOf(id), Is.InRange(0, VisualPairId.ShardCount - 1));
        Assert.That(VisualPairId.ShardOf("ff00"), Is.EqualTo(255 % VisualPairId.ShardCount));
        Assert.That(VisualPairId.DayBucket(new DateTime(2026, 9, 29, 23, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-09-29"));
    }

    [Test]
    public void Labels_Validation()
    {
        Assert.That(VisualPairLabels.IsValid("same_item"), Is.True);
        Assert.That(VisualPairLabels.IsValid("same_model_other_variant"), Is.True);
        Assert.That(VisualPairLabels.IsValid("different"), Is.True);
        Assert.That(VisualPairLabels.IsValid("unsure"), Is.True);
        Assert.That(VisualPairLabels.IsValid("SAME_ITEM"), Is.False);
        Assert.That(VisualPairLabels.IsValid(""), Is.False);
        Assert.That(VisualPairLabels.IsValid(null), Is.False);
    }

    [Test]
    public void Mapping_KeysAndPlatformColumnsAreNotEnums()
    {
        var mapping = CassandraVisualPairStore.BuildMapping();
        var candidate = mapping.Get<VisualPairCandidate>();
        Assert.That(candidate.TableName, Is.EqualTo("visual_pair_candidates"));
        Assert.That(candidate.PartitionKeys, Is.EquivalentTo(new[] { "day", "shard" }));
        Assert.That(candidate.ClusteringKeys.Select(k => k.Item1), Is.EquivalentTo(new[] { "pair_id" }));
        var label = mapping.Get<VisualPairLabel>();
        Assert.That(label.TableName, Is.EqualTo("visual_pair_labels"));
        Assert.That(label.PartitionKeys, Is.EquivalentTo(new[] { "pair_id" }));
        Assert.That(label.ClusteringKeys.Select(k => k.Item1), Is.EquivalentTo(new[] { "user_id" }));
        Assert.That(candidate.GetColumnDefinition(typeof(VisualPairCandidate).GetProperty(nameof(VisualPairCandidate.CaptureMetadataJson))!).ColumnName,
            Is.EqualTo("capture_metadata_json"));
        Assert.That(label.GetColumnDefinition(typeof(VisualPairLabel).GetProperty(nameof(VisualPairLabel.CaptureMetadataJson))!).ColumnName,
            Is.EqualTo("capture_metadata_json"));
        Assert.That(label.GetColumnDefinition(typeof(VisualPairLabel).GetProperty(nameof(VisualPairLabel.ReviewMode))!).ColumnName,
            Is.EqualTo("review_mode"));
        foreach (var property in typeof(VisualPairCandidate).GetProperties())
            if (!candidate.GetColumnDefinition(property).Ignore)
                Assert.That(property.PropertyType.IsEnum, Is.False, $"candidate {property.Name}");
        foreach (var property in typeof(VisualPairLabel).GetProperties())
            if (!label.GetColumnDefinition(property).Ignore)
                Assert.That(property.PropertyType.IsEnum, Is.False, $"label {property.Name}");
    }

    private static VisualPairCandidate Candidate(string a, string b, DateTime at) => new()
    {
        PairId = VisualPairId.Compute(a, b),
        A = new(Platform.Vinted, a, 0, "https://images1.vinted.net/" + a),
        B = new(Platform.Vinted, b, 0, "https://images1.vinted.net/" + b),
        GroupKey = "g", Similarity = 0.85, Decision = VisualPairDecisions.Join, ModelId = "m", Source = "test", CreatedAt = at
    };

    [Test]
    public async Task InMemory_AddCandidate_IsIdempotent_AndListedByDay()
    {
        var store = new InMemoryVisualPairStore();
        var at = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
        Assert.That(await store.AddCandidateAsync(Candidate("1", "2", at)), Is.True);
        Assert.That(await store.AddCandidateAsync(Candidate("2", "1", at.AddHours(5))), Is.False);
        await store.AddCandidateAsync(Candidate("3", "4", at.AddDays(-1)));

        Assert.That(await store.GetCandidatesAsync(new DateOnly(2026, 9, 29), 10), Has.Count.EqualTo(1));
        Assert.That(await store.GetCandidatesAsync(new DateOnly(2026, 9, 28), 10), Has.Count.EqualTo(1));
        Assert.That((await store.GetCandidateAsync(VisualPairId.Compute("1", "2")))!.CreatedAt, Is.EqualTo(at));
    }

    [Test]
    public async Task InMemory_SecondLabelOfSameUserOverwrites_OthersAdd()
    {
        var store = new InMemoryVisualPairStore();
        var c = Candidate("1", "2", DateTime.UtcNow);
        await store.AddLabelAsync(VisualPairLabel.From(c, "u1", "same_item", DateTime.UtcNow));
        await store.AddLabelAsync(VisualPairLabel.From(c, "u1", "different", DateTime.UtcNow));
        await store.AddLabelAsync(VisualPairLabel.From(c, "u2", "unsure", DateTime.UtcNow));

        var labels = await store.GetLabelsForPairAsync(c.PairId);
        Assert.That(labels.Select(l => (l.UserId, l.Label)), Is.EquivalentTo(new[] { ("u1", "different"), ("u2", "unsure") }));
        var counts = await store.CountLabelsAsync();
        Assert.That(counts["different"], Is.EqualTo(1));
        Assert.That(counts["unsure"], Is.EqualTo(1));
        Assert.That(counts.ContainsKey("same_item"), Is.False);
    }

    [TestCase("diagnostic", "blind", "diagnostic")]
    [TestCase("diagnostic", null, "diagnostic")]
    [TestCase(null, "blind", null)]
    [TestCase("blind", null, null)]
    [TestCase("blind", "diagnostic", "diagnostic")]
    public async Task InMemory_StaleSubmissionPreservesExposure_WhileOverwritingAnswerAndFrozenFacts(
        string? before, string? requested, string? expected)
    {
        var store = new InMemoryVisualPairStore();
        var candidate = Candidate("a", "b", DateTime.UtcNow);
        var stale = VisualPairLabel.From(candidate, "u", "different", DateTime.UtcNow);
        stale.ReviewMode = requested;
        stale.CaptureMetadataJson = "frozen stale request";
        var earlier = VisualPairLabel.From(candidate, "u", "same_item", DateTime.UtcNow);
        earlier.ReviewMode = before;
        await store.AddLabelAsync(earlier);
        await store.AddLabelAsync(stale);
        var saved = (await store.GetLabelsForPairAsync(candidate.PairId)).Single();
        Assert.That(saved.ReviewMode, Is.EqualTo(expected));
        Assert.That(saved.Label, Is.EqualTo("different"));
        Assert.That(saved.CaptureMetadataJson, Is.EqualTo("frozen stale request"));
    }

    [Test]
    public async Task InMemory_ConcurrentSubmissionsCannotDowngradeDiagnosticExposure()
    {
        var store = new InMemoryVisualPairStore();
        var candidate = Candidate("a", "b", DateTime.UtcNow);
        using var start = new ManualResetEventSlim();
        var writes = Enumerable.Range(0, 32).Select(i => Task.Run(async () =>
        {
            var label = VisualPairLabel.From(candidate, "u", "same_item", DateTime.UtcNow);
            label.ReviewMode = i == 0 ? "diagnostic" : i % 2 == 0 ? "blind" : null;
            start.Wait();
            await store.AddLabelAsync(label);
        })).ToList();
        start.Set();
        await Task.WhenAll(writes);
        Assert.That((await store.GetLabelsForPairAsync(candidate.PairId)).Single().ReviewMode, Is.EqualTo("diagnostic"));
    }

    public class ContendedLabelMapper : DispatchProxy
    {
        public List<(string Cql, object[] Values)> Updates { get; } = [];
        public bool InsertNulls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "InsertIfNotExistsAsync")
            {
                InsertNulls = (bool)args![1]!;
                return Task.FromResult(new AppliedInfo<VisualPairLabel>(new VisualPairLabel { ReviewMode = null }));
            }
            if (method.Name == "UpdateIfAsync")
            {
                Updates.Add(((string)args![0]!, ((object[])args[1]!).ToArray()));
                // Another writer records diagnostic after the initial null/unknown observation.
                return Task.FromResult(new AppliedInfo<VisualPairLabel>(new VisualPairLabel { ReviewMode = "diagnostic" }));
            }
            throw new NotSupportedException(method.Name);
        }
    }

    [Test]
    public void Cassandra_ConditionalOverwriteBindsUnknownNull_AndRetriesObservedExposureWithoutUnsafeFallback()
    {
        var initialized = typeof(CassandraVisualPairStore).GetField("tablesInitialized", BindingFlags.NonPublic | BindingFlags.Static)!;
        var wasInitialized = initialized.GetValue(null);
        var proxy = DispatchProxy.Create<IMapper, ContendedLabelMapper>();
        var recorder = (ContendedLabelMapper)proxy;
        var store = (CassandraVisualPairStore)RuntimeHelpers.GetUninitializedObject(typeof(CassandraVisualPairStore));
        typeof(CassandraVisualPairStore).GetField("mapper", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, proxy);
        var label = VisualPairLabel.From(Candidate("a", "b", DateTime.UtcNow), "u", "different", DateTime.UtcNow);
        label.ReviewMode = "blind";
        label.CaptureMetadataJson = "exact frozen JSON";
        initialized.SetValue(null, true);
        try
        {
            Assert.ThrowsAsync<InvalidOperationException>(() => store.AddLabelAsync(label));
            Assert.That(recorder.InsertNulls, Is.True);
            Assert.That(recorder.Updates, Has.Count.EqualTo(4));
            var first = recorder.Updates[0];
            Assert.That(first.Cql, Does.EndWith("WHERE pair_id = ? AND user_id = ? IF review_mode = ?"));
            Assert.That(first.Cql.Count(c => c == '?'), Is.EqualTo(first.Values.Length));
            Assert.That(first.Values[1], Is.Null); // unknown cannot be upgraded to blind
            Assert.That(first.Values[^1], Is.Null); // a bound null, not an absent/unset comparison
            Assert.That(first.Values[3], Is.EqualTo("exact frozen JSON"));
            Assert.That(first.Values[^3], Is.EqualTo(label.PairId));
            Assert.That(first.Values[^2], Is.EqualTo("u"));
            Assert.That(recorder.Updates.Skip(1).All(u => (string)u.Values[1] == "diagnostic"
                && (string)u.Values[^1] == "diagnostic"), Is.True);
        }
        finally { initialized.SetValue(null, wasInitialized); }
    }

    [Test]
    public async Task InMemory_LabelsPaged_CoverEverythingOnce()
    {
        var store = new InMemoryVisualPairStore();
        for (var i = 0; i < 7; i++)
            await store.AddLabelAsync(VisualPairLabel.From(Candidate("a" + i, "b" + i, DateTime.UtcNow), "u", "different", DateTime.UtcNow));
        var seen = new List<string>();
        byte[]? state = null;
        do
        {
            var (page, next) = await store.GetLabelsAsync(3, state);
            seen.AddRange(page.Select(l => l.PairId));
            state = next;
        } while (state != null);

        Assert.That(seen, Has.Count.EqualTo(7));
        Assert.That(seen.Distinct().Count(), Is.EqualTo(7));
    }

    [Test]
    public void LabelKeepsImageReferencesOfTheCandidate()
    {
        var c = Candidate("1", "2", DateTime.UtcNow);
        var label = VisualPairLabel.From(c, "u", "same_item", DateTime.UtcNow);
        Assert.That(label.A, Is.EqualTo(c.A));
        Assert.That(label.B, Is.EqualTo(c.B));
        Assert.That(label.APlatformValue, Is.EqualTo((int)Platform.Vinted));
        Assert.That(label.Similarity, Is.EqualTo(0.85));
    }

    [Test]
    public void LabelFreezesCaptureProvenance_AndOldRowsRemainUnknown()
    {
        var candidate = Candidate("1", "2", DateTime.UtcNow);
        Assert.That(VisualPairLabel.From(candidate, "u", "same_item", DateTime.UtcNow).CaptureMetadataJson, Is.Null);
        candidate.CaptureMetadataJson = "{\"SchemaVersion\":1,\"ASellerHash\":\"opaque-hmac\"}";
        var label = VisualPairLabel.From(candidate, "u", "same_item", DateTime.UtcNow);
        var captured = candidate.CaptureMetadataJson;
        candidate.CaptureMetadataJson = null; // candidate expiry/change cannot erase label evidence
        Assert.That(label.CaptureMetadataJson, Is.EqualTo(captured));
        Assert.That(label.ReviewMode, Is.Null); // no automatic blind/human evidence claim
    }

    [Test]
    public async Task UpgradeCandidateSource_ChangesSourceOfExistingCandidateOnly()
    {
        IVisualPairStore store = new InMemoryVisualPairStore();
        var id = VisualPairId.Compute("Vinted:1:0", "Vinted:2:0");
        await store.AddCandidateAsync(new VisualPairCandidate { PairId = id, Source = "model", CreatedAt = DateTime.UtcNow });

        Assert.That(await store.UpgradeCandidateSourceAsync(id, "user_report"), Is.True);
        Assert.That((await store.GetCandidateAsync(id))!.Source, Is.EqualTo("user_report"));
        Assert.That(await store.UpgradeCandidateSourceAsync(id, "user_report"), Is.False);
        Assert.That(await store.UpgradeCandidateSourceAsync("missing", "user_report"), Is.False);
    }
}
