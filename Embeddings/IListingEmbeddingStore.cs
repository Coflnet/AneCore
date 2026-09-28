namespace Coflnet.Ane.Embeddings;

/// <summary>Persistence for clothing listing image embeddings. Implemented by Cassandra (production) and in-memory (tests).</summary>
public interface IListingEmbeddingStore
{
    /// <summary>Creates backing storage if needed. Safe to call repeatedly; no-op for in-memory stores.</summary>
    Task InitializeAsync();

    /// <summary>All embedded photos of one listing, ordered by <see cref="ListingImageEmbedding.ImageIndex"/>.</summary>
    Task<IReadOnlyList<ListingImageEmbedding>> GetEmbeddingsAsync(string listingId, Platform platform);

    /// <summary>Inserts or fully replaces one or more photo embeddings (each carries its own listing id/platform/image index).</summary>
    Task UpsertEmbeddingsAsync(IEnumerable<ListingImageEmbedding> embeddings);

    /// <summary>
    /// Sets <see cref="ListingImageEmbedding.ClusterSeoId"/> on every photo currently stored for this listing
    /// (there is one row per photo, so this touches every row of the listing's partition, not a single cell).
    /// </summary>
    Task SetClusterSeoIdAsync(string listingId, Platform platform, string? clusterSeoId);
}

/// <summary>
/// In-memory <see cref="IListingEmbeddingStore"/> for tests and any other non-Cassandra-backed usage.
/// Thread-safe; no persistence beyond process lifetime.
/// </summary>
public class InMemoryListingEmbeddingStore : IListingEmbeddingStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        (string ListingId, Platform Platform),
        System.Collections.Concurrent.ConcurrentDictionary<int, ListingImageEmbedding>> byListing = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<ListingImageEmbedding>> GetEmbeddingsAsync(string listingId, Platform platform)
    {
        if (!byListing.TryGetValue((listingId, platform), out var images))
            return Task.FromResult<IReadOnlyList<ListingImageEmbedding>>([]);
        return Task.FromResult<IReadOnlyList<ListingImageEmbedding>>(
            images.Values.OrderBy(e => e.ImageIndex).ToList());
    }

    public Task UpsertEmbeddingsAsync(IEnumerable<ListingImageEmbedding> embeddings)
    {
        foreach (var embedding in embeddings)
        {
            var images = byListing.GetOrAdd((embedding.ListingId, embedding.Platform), _ => new());
            images[embedding.ImageIndex] = embedding;
        }
        return Task.CompletedTask;
    }

    public Task SetClusterSeoIdAsync(string listingId, Platform platform, string? clusterSeoId)
    {
        if (byListing.TryGetValue((listingId, platform), out var images))
            foreach (var image in images.Values)
                image.ClusterSeoId = clusterSeoId;
        return Task.CompletedTask;
    }
}
