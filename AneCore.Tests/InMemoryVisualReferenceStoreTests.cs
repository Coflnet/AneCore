using Coflnet.Ane.Opensearch;

namespace AneCore.Tests;

[TestFixture]
public class InMemoryVisualReferenceStoreTests
{
    private static float[] Unit(int dim, int hot)
    {
        var v = new float[dim];
        v[hot] = 1f;
        return v;
    }

    private static ClothingVisualDocument Doc(
        string listingId, float[] embedding, string groupKey = "women/tops", string modelId = "fashion-clip-v1", bool seed = false, DateTime? createdAt = null) =>
        new(listingId, "Ebay", groupKey, null, seed, "https://x/1.jpg", "blue", "solid", "women", modelId, createdAt ?? DateTime.UtcNow, embedding);

    [Test]
    public async Task Search_OrdersByCosineSimilarityDescending()
    {
        var store = new InMemoryVisualReferenceStore();
        // query is the unit vector on axis 0; "near" is a slightly rotated copy (still mostly axis 0),
        // "far" is orthogonal (axis 1) - near must rank first.
        var query = new[] { 1f, 0f, 0f };
        var near = new[] { 0.9f, 0.1f, 0f };
        var far = new[] { 0f, 1f, 0f };

        await store.IndexAsync(0, Doc("near", near), default);
        await store.IndexAsync(0, Doc("far", far), default);

        var hits = await store.SearchAsync(query, k: 10, groupKey: "women/tops", modelId: "fashion-clip-v1", excludeListingId: "self", default);

        Assert.That(hits, Has.Count.EqualTo(2));
        Assert.That(hits[0].Document.ListingId, Is.EqualTo("near"));
        Assert.That(hits[1].Document.ListingId, Is.EqualTo("far"));
        Assert.That(hits[0].Score, Is.GreaterThan(hits[1].Score));
    }

    [Test]
    public async Task Search_ReturnsEmbeddingOnHits()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(4, 1);
        await store.IndexAsync(0, Doc("l1", vector), default);

        var hits = await store.SearchAsync(vector, 10, "women/tops", "fashion-clip-v1", "self", default);

        Assert.That(hits, Has.Count.EqualTo(1));
        Assert.That(hits[0].Document.Embedding, Is.EqualTo(vector));
    }

    [Test]
    public async Task Search_FiltersByGroupKeyAndModelId()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(3, 0);
        await store.IndexAsync(0, Doc("wrong-group", vector, groupKey: "men/jackets"), default);
        await store.IndexAsync(0, Doc("wrong-model", vector, modelId: "fashion-clip-v2"), default);
        await store.IndexAsync(0, Doc("matching", vector), default);

        var hits = await store.SearchAsync(vector, 10, "women/tops", "fashion-clip-v1", "self", default);

        Assert.That(hits.Select(h => h.Document.ListingId), Is.EqualTo(new[] { "matching" }));
    }

    [Test]
    public async Task Search_ExcludesGivenListingId()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(3, 0);
        await store.IndexAsync(0, Doc("self-listing", vector), default);
        await store.IndexAsync(0, Doc("other-listing", vector), default);

        var hits = await store.SearchAsync(vector, 10, "women/tops", "fashion-clip-v1", excludeListingId: "self-listing", default);

        Assert.That(hits.Select(h => h.Document.ListingId), Is.EqualTo(new[] { "other-listing" }));
    }

    [Test]
    public async Task Search_RespectsK()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(3, 0);
        for (var i = 0; i < 5; i++)
            await store.IndexAsync(0, Doc($"l{i}", vector), default);

        var hits = await store.SearchAsync(vector, k: 2, "women/tops", "fashion-clip-v1", "self", default);

        Assert.That(hits, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteByListing_RemovesAllItsPhotosOnly()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(3, 0);
        await store.IndexAsync(0, Doc("l1", vector), default);
        await store.IndexAsync(1, Doc("l1", vector), default);
        await store.IndexAsync(0, Doc("l2", vector), default);

        await store.DeleteByListingAsync("l1", "Ebay", default);

        var hits = await store.SearchAsync(vector, 10, "women/tops", "fashion-clip-v1", "self", default);
        Assert.That(hits.Select(h => h.Document.ListingId), Is.EqualTo(new[] { "l2" }));
    }

    [Test]
    public async Task DeleteOlderThan_SplitsBySeedFlag()
    {
        var store = new InMemoryVisualReferenceStore();
        var vector = Unit(3, 0);
        var old = DateTime.UtcNow.AddDays(-30);
        var recent = DateTime.UtcNow;
        await store.IndexAsync(0, Doc("old-seed", vector, seed: true, createdAt: old), default);
        await store.IndexAsync(0, Doc("old-nonseed", vector, seed: false, createdAt: old), default);
        await store.IndexAsync(0, Doc("recent-seed", vector, seed: true, createdAt: recent), default);

        var deleted = await store.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), seed: true, default);

        Assert.That(deleted, Is.EqualTo(1));
        var hits = await store.SearchAsync(vector, 10, "women/tops", "fashion-clip-v1", "self", default);
        Assert.That(hits.Select(h => h.Document.ListingId), Is.EquivalentTo(new[] { "old-nonseed", "recent-seed" }));
    }

    [Test]
    public void IsAvailable_AlwaysTrue()
    {
        var store = new InMemoryVisualReferenceStore();
        Assert.That(store.IsAvailable, Is.True);
    }
}
