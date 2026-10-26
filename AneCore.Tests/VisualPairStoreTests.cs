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
        var exposure = mapping.Get<VisualPairDiagnosticExposure>();
        Assert.That(exposure.TableName, Is.EqualTo("visual_pair_diagnostic_exposures"));
        Assert.That(exposure.PartitionKeys, Is.EquivalentTo(new[] { "pair_id" }));
        Assert.That(exposure.ClusteringKeys.Select(k => k.Item1), Is.EquivalentTo(new[] { "user_id" }));
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
    [TestCase("pending", "blind", null)]
    [TestCase("pending", "diagnostic", "diagnostic")]
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

    [Test]
    public async Task InMemory_ExposureWithoutVoteIsReviewerScopedAndUpgradesExistingVotes()
    {
        var store = new InMemoryVisualPairStore();
        var c = Candidate("exposure-a", "exposure-b", DateTime.UtcNow);
        await store.MarkDiagnosticExposureAsync(c, "exposed");
        Assert.That(await store.GetLabelsForPairAsync(c.PairId), Is.Empty);
        foreach (var user in new[] { "exposed", "other" })
        {
            var label = VisualPairLabel.From(c, user, "different", DateTime.UtcNow);
            label.ReviewMode = "blind";
            await store.AddLabelAsync(label);
        }
        var votes = await store.GetLabelsForPairAsync(c.PairId);
        Assert.That(votes.Single(v => v.UserId == "exposed").ReviewMode, Is.EqualTo("diagnostic"));
        Assert.That(votes.Single(v => v.UserId == "other").ReviewMode, Is.EqualTo("blind"));
        await store.MarkDiagnosticExposureAsync(c, "other");
        Assert.That(votes.Single(v => v.UserId == "other").ReviewMode, Is.EqualTo("diagnostic"));
        await store.DeleteForListingAsync(c.A.Platform, c.A.ListingId);
        var fresh = VisualPairLabel.From(c, "exposed", "different", DateTime.UtcNow);
        fresh.ReviewMode = "blind";
        await store.AddLabelAsync(fresh);
        Assert.That((await store.GetLabelsForPairAsync(c.PairId)).Single().ReviewMode, Is.EqualTo("blind"), "takedown also removes exposure");
    }

    [Test]
    public async Task InMemory_TakedownRemovesExposureWithoutCandidateOrVote()
    {
        var store = new InMemoryVisualPairStore();
        var c = Candidate("expired-a", "expired-b", DateTime.UtcNow);
        // No candidate or vote remains: exposure must retain both takedown references itself.
        await store.MarkDiagnosticExposureAsync(c, "user");
        Assert.That(await store.DeleteForListingAsync(c.B.Platform, c.B.ListingId), Is.EqualTo((0, 0)));
        var label = VisualPairLabel.From(c, "user", "different", DateTime.UtcNow);
        label.ReviewMode = "blind";
        await store.AddLabelAsync(label);
        Assert.That((await store.GetLabelsForPairAsync(c.PairId)).Single().ReviewMode, Is.EqualTo("blind"));
    }

    public sealed class LabelPage(IEnumerable<VisualPairLabel> rows) : List<VisualPairLabel>(rows), IPage<VisualPairLabel>
    {
        public byte[]? CurrentPagingState => null;
        public byte[]? PagingState => null;
    }

    [TestCase("blind")]
    [TestCase(null)]
    [TestCase("diagnostic")]
    public async Task Cassandra_FirstBlindFinalizationCannotEraseConcurrentUnknownOrDiagnostic(string? concurrent)
    {
        var initialized = typeof(CassandraVisualPairStore).GetField("tablesInitialized", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = initialized.GetValue(null);
        var mapper = DispatchProxy.Create<IMapper, ExposureRaceMapper>();
        var race = (ExposureRaceMapper)mapper;
        var store = (CassandraVisualPairStore)RuntimeHelpers.GetUninitializedObject(typeof(CassandraVisualPairStore));
        typeof(CassandraVisualPairStore).GetField("mapper", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, mapper);
        initialized.SetValue(null, true);
        try
        {
            var c = Candidate("a", "b", DateTime.UtcNow); c.PairId = "pair";
            var first = VisualPairLabel.From(c, "user", "different", DateTime.UtcNow); first.ReviewMode = "blind";
            var pending = store.AddLabelAsync(first);
            await race.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (concurrent != "blind")
            {
                var other = VisualPairLabel.From(c, "user", "same_item", DateTime.UtcNow); other.ReviewMode = concurrent;
                await store.AddLabelAsync(other);
            }
            race.Continue.SetResult();
            await pending;
            Assert.That(race.Vote!.ReviewMode, Is.EqualTo(concurrent));
            Assert.That((await store.GetLabelsForPairAsync("pair")).Single().ReviewMode, Is.EqualTo(concurrent));
        }
        finally { race.Continue.TrySetResult(); initialized.SetValue(null, previous); }
    }

    public class ExposureRaceMapper : DispatchProxy
    {
        public bool PauseBeforeVote { get; set; }
        public bool FailMarkerRead { get; set; }
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public VisualPairLabel? Vote { get; private set; }
        public bool Exposed { get; private set; }
        public List<Cql> MarkerQueries { get; } = [];
        public List<Cql> IndexWrites { get; } = [];
        public int ConditionalUpgrades { get; private set; }

        private async Task<AppliedInfo<VisualPairLabel>> Insert(VisualPairLabel label)
        {
            if (Vote != null) return new AppliedInfo<VisualPairLabel>(CopyVote());
            if (PauseBeforeVote) { Paused.SetResult(); await Continue.Task; }
            Vote = new VisualPairLabel { PairId = label.PairId, UserId = label.UserId, Label = label.Label, ReviewMode = label.ReviewMode };
            return new AppliedInfo<VisualPairLabel>(true);
        }

        private VisualPairLabel CopyVote() => new() { PairId = Vote!.PairId, UserId = Vote.UserId, Label = Vote.Label, ReviewMode = Vote.ReviewMode };

        private async Task<IEnumerable<VisualPairDiagnosticExposure>> Read(Cql cql)
        {
            MarkerQueries.Add(cql);
            if (FailMarkerRead) throw new InvalidOperationException("marker read unavailable");
            var snapshot = Exposed ? new[] { new VisualPairDiagnosticExposure() } : [];
            if (!PauseBeforeVote && !Paused.Task.IsCompleted) { Paused.SetResult(); await Continue.Task; }
            return snapshot;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "InsertIfNotExistsAsync") return Insert((VisualPairLabel)args![0]!);
            if (method.Name == "FetchAsync")
                return method.GetGenericArguments()[0] == typeof(VisualPairLabel)
                    ? Task.FromResult<IEnumerable<VisualPairLabel>>(Vote == null ? [] : [CopyVote()])
                    : Read((Cql)args![0]!);
            if (method.Name == "FetchPageAsync")
                return Task.FromResult<IPage<VisualPairLabel>>(new LabelPage(Vote == null ? [] : [CopyVote()]));
            if (method.Name == "ExecuteAsync")
            {
                var cql = (Cql)args![0]!;
                if (cql.Statement.Contains("visual_pair_diagnostic_exposures")) { MarkerQueries.Add(cql); Exposed = true; }
                else IndexWrites.Add(cql);
                return Task.CompletedTask;
            }
            if (method.Name == "UpdateIfAsync")
            {
                var cql = (string)args![0]!;
                var values = (object[])args[1]!;
                if (cql.EndsWith("IF EXISTS"))
                {
                    Assert.That(values, Is.EqualTo(new object[] { "diagnostic", "pair", "user" }));
                    ConditionalUpgrades++;
                    if (Vote != null) Vote.ReviewMode = "diagnostic";
                    return Task.FromResult(new AppliedInfo<VisualPairLabel>(Vote != null));
                }
                Assert.That(cql, Does.EndWith("IF review_mode = ?"));
                if (Vote == null || Vote.ReviewMode != (string?)values[^1])
                    return Task.FromResult(new AppliedInfo<VisualPairLabel>(CopyVote()));
                if (cql.StartsWith("SET label =")) { Vote.Label = (string)values[0]; Vote.ReviewMode = (string?)values[1]; }
                else Vote.ReviewMode = (string?)values[0];
                return Task.FromResult(new AppliedInfo<VisualPairLabel>(true));
            }
            throw new NotSupportedException(method.Name);
        }
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task Cassandra_ExposureAndBlindVoteHandshakeCoversBothInterleavings(bool pauseBeforeVote, bool failMarkerRead)
    {
        var initialized = typeof(CassandraVisualPairStore).GetField("tablesInitialized", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = initialized.GetValue(null);
        var mapper = DispatchProxy.Create<IMapper, ExposureRaceMapper>();
        var race = (ExposureRaceMapper)mapper;
        race.PauseBeforeVote = pauseBeforeVote;
        race.FailMarkerRead = failMarkerRead;
        var store = (CassandraVisualPairStore)RuntimeHelpers.GetUninitializedObject(typeof(CassandraVisualPairStore));
        typeof(CassandraVisualPairStore).GetField("mapper", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, mapper);
        initialized.SetValue(null, true);
        try
        {
            var c = Candidate("a", "b", DateTime.UtcNow);
            c.PairId = "pair";
            var vote = VisualPairLabel.From(c, "user", "different", DateTime.UtcNow);
            vote.ReviewMode = "blind";
            var pending = store.AddLabelAsync(vote);
            await race.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await store.MarkDiagnosticExposureAsync(c, "user");
            if (pauseBeforeVote) Assert.That(race.Vote, Is.Null, "IF EXISTS must not create a placeholder vote");
            race.Continue.SetResult();
            if (failMarkerRead)
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
                Assert.That(race.Vote!.ReviewMode, Is.EqualTo("pending"), "a failed marker read must not leave a false blind vote");
                Assert.That(race.IndexWrites, Has.Count.EqualTo(4), "both vote and exposure keep durable takedown indices before the failed read");
                Assert.That((await store.GetLabelsForPairAsync("pair")).Single().ReviewMode, Is.Null);
                Assert.That((await store.GetLabelsForPairsAsync(["pair"]))["pair"].Single().ReviewMode, Is.Null);
                Assert.That((await store.GetLabelsAsync(10, null)).Labels.Single().ReviewMode, Is.Null, "the paged export path must normalize pending to unknown");
                Assert.That(race.Vote.ReviewMode, Is.EqualTo("pending"), "normalizing read copies must not mutate persisted CAS state");
                return;
            }
            await pending;
            Assert.That(race.Vote!.ReviewMode, Is.EqualTo("diagnostic"));
            Assert.That(race.ConditionalUpgrades, Is.EqualTo(pauseBeforeVote ? 2 : 1));
            Assert.That(race.MarkerQueries, Has.Count.EqualTo(2));
            foreach (var cql in race.MarkerQueries)
            {
                var statement = new global::Cassandra.SimpleStatement(cql.Statement);
                var options = typeof(Cql).GetProperty("QueryOptions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(cql)!;
                options.GetType().GetMethod("CopyOptionsToStatement", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(options, [statement]);
                Assert.That(statement.ConsistencyLevel, Is.EqualTo(global::Cassandra.ConsistencyLevel.Quorum));
                Assert.That(cql.Statement, Does.Not.Contain("TTL"));
            }
            Assert.That(race.IndexWrites, Has.Count.EqualTo(4));
            Assert.That(race.IndexWrites.All(cql => !cql.Statement.Contains("TTL")), Is.True);
            Assert.That(race.IndexWrites.Select(cql => cql.Arguments[1]).Distinct(), Is.EquivalentTo(new[] { "a", "b" }));
        }
        finally { race.Continue.TrySetResult(); initialized.SetValue(null, previous); }
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
