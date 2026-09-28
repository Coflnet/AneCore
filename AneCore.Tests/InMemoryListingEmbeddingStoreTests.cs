using Coflnet.Ane;
using Coflnet.Ane.Embeddings;

namespace AneCore.Tests;

[TestFixture]
public class InMemoryListingEmbeddingStoreTests
{
    private static ListingImageEmbedding Embedding(string listingId, Platform platform, int imageIndex, string? clusterSeoId = null) =>
        new()
        {
            ListingId = listingId,
            Platform = platform,
            ImageIndex = imageIndex,
            ImageUrl = $"https://example.com/{listingId}/{imageIndex}.jpg",
            ModelId = "fashion-clip-v1",
            Embedding = EmbeddingBlobCodec.Encode(new float[EmbeddingBlobCodec.Dimensions]),
            GroupKey = "women/tops",
            ClusterSeoId = clusterSeoId,
            CreatedAt = DateTime.UtcNow,
        };

    [Test]
    public async Task UpsertThenGet_ReturnsEmbeddingsOrderedByImageIndex()
    {
        var store = new InMemoryListingEmbeddingStore();
        await store.UpsertEmbeddingsAsync([
            Embedding("l1", Platform.Ebay, 2),
            Embedding("l1", Platform.Ebay, 0),
            Embedding("l1", Platform.Ebay, 1),
        ]);

        var result = await store.GetEmbeddingsAsync("l1", Platform.Ebay);

        Assert.That(result.Select(e => e.ImageIndex), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public async Task GetEmbeddings_UnknownListing_ReturnsEmpty()
    {
        var store = new InMemoryListingEmbeddingStore();
        var result = await store.GetEmbeddingsAsync("missing", Platform.Ebay);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Upsert_SameImageIndex_Replaces()
    {
        var store = new InMemoryListingEmbeddingStore();
        await store.UpsertEmbeddingsAsync([Embedding("l1", Platform.Ebay, 0)]);
        var replacement = Embedding("l1", Platform.Ebay, 0);
        replacement.ImageUrl = "https://example.com/replaced.jpg";
        await store.UpsertEmbeddingsAsync([replacement]);

        var result = await store.GetEmbeddingsAsync("l1", Platform.Ebay);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ImageUrl, Is.EqualTo("https://example.com/replaced.jpg"));
    }

    [Test]
    public async Task DifferentPlatforms_AreDistinctPartitions()
    {
        var store = new InMemoryListingEmbeddingStore();
        await store.UpsertEmbeddingsAsync([
            Embedding("same-id", Platform.Ebay, 0),
            Embedding("same-id", Platform.Kleinanzeigen, 0),
        ]);

        var ebay = await store.GetEmbeddingsAsync("same-id", Platform.Ebay);
        var kleinanzeigen = await store.GetEmbeddingsAsync("same-id", Platform.Kleinanzeigen);

        Assert.That(ebay, Has.Count.EqualTo(1));
        Assert.That(kleinanzeigen, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SetClusterSeoId_UpdatesEveryImageOfTheListing()
    {
        var store = new InMemoryListingEmbeddingStore();
        await store.UpsertEmbeddingsAsync([
            Embedding("l1", Platform.Ebay, 0),
            Embedding("l1", Platform.Ebay, 1),
            Embedding("other", Platform.Ebay, 0),
        ]);

        await store.SetClusterSeoIdAsync("l1", Platform.Ebay, "blue-hoodie-cluster-1");

        var l1 = await store.GetEmbeddingsAsync("l1", Platform.Ebay);
        var other = await store.GetEmbeddingsAsync("other", Platform.Ebay);
        Assert.That(l1.Select(e => e.ClusterSeoId), Is.All.EqualTo("blue-hoodie-cluster-1"));
        Assert.That(other[0].ClusterSeoId, Is.Null);
    }

    [Test]
    public async Task SetClusterSeoId_UnknownListing_DoesNotThrow()
    {
        var store = new InMemoryListingEmbeddingStore();
        Assert.DoesNotThrowAsync(() => store.SetClusterSeoIdAsync("missing", Platform.Ebay, "x"));
        await Task.CompletedTask;
    }
}
