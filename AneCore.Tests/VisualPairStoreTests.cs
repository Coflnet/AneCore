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
}
