// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

/// <summary>One kNN search hit: the matched document plus its embedding, so the caller can recompute exact cosine similarity on top of the approximate kNN ranking.</summary>
public record VisualSearchHit(string Id, double Score, ClothingVisualDocument Document);

/// <summary>
/// Persistence for the clothing visual-similarity reference index. Implemented by OpenSearch (production,
/// <see cref="OpenSearchVisualReferenceStore"/>) and in-memory brute-force cosine (tests,
/// <see cref="InMemoryVisualReferenceStore"/>).
/// </summary>
public interface IVisualReferenceStore
{
    /// <summary>
    /// Creates the index if needed. Safe to call repeatedly. If the cluster lacks the k-NN plugin (or
    /// setup otherwise fails), this logs a warning and leaves <see cref="IsAvailable"/> false instead of
    /// throwing, so a missing plugin never fails service startup.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>True once <see cref="InitializeAsync"/> has completed setup successfully. While false, every other member is a safe no-op (writes are skipped, searches return empty).</summary>
    bool IsAvailable { get; }

    /// <summary>Indexes the full document for one listing photo. Always a full replace, never a partial update.</summary>
    Task IndexAsync(int imageIndex, ClothingVisualDocument document, CancellationToken ct = default);

    /// <summary>Deletes every indexed photo of one listing.</summary>
    Task DeleteByListingAsync(string listingId, string platform, CancellationToken ct = default);

    /// <summary>
    /// k nearest neighbours of <paramref name="queryVector"/>, scoped to <paramref name="groupKey"/> and
    /// <paramref name="modelId"/> and excluding <paramref name="excludeListingId"/>'s own photos (so a
    /// listing never matches itself). Hits carry their embedding back - see <see cref="VisualSearchHit"/>.
    /// </summary>
    Task<IReadOnlyList<VisualSearchHit>> SearchAsync(
        float[] queryVector, int k, string groupKey, string modelId, string excludeListingId,
        CancellationToken ct = default);

    /// <summary>Deletes documents older than <paramref name="cutoff"/> (by <c>createdAt</c>), scoped to <c>seed == </c><paramref name="seed"/>. Returns the number of documents deleted.</summary>
    Task<long> DeleteOlderThanAsync(DateTime cutoff, bool seed, CancellationToken ct = default);
}

/// <summary>
/// In-memory <see cref="IVisualReferenceStore"/> for tests: brute-force cosine similarity via
/// <see cref="VectorMath.CosineSimilarity"/> instead of an approximate kNN index. Thread-safe; no
/// persistence beyond process lifetime. Always available (<see cref="IsAvailable"/> is always true) since
/// there is no plugin to be missing.
/// </summary>
public class InMemoryVisualReferenceStore : IVisualReferenceStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int ImageIndex, ClothingVisualDocument Document)> documents = new();

    public bool IsAvailable => true;

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task IndexAsync(int imageIndex, ClothingVisualDocument document, CancellationToken ct = default)
    {
        var id = ClothingVisualIndex.BuildDocumentId(document.Platform, document.ListingId, imageIndex);
        documents[id] = (imageIndex, document);
        return Task.CompletedTask;
    }

    public Task DeleteByListingAsync(string listingId, string platform, CancellationToken ct = default)
    {
        foreach (var (id, entry) in documents)
            if (entry.Document.ListingId == listingId && entry.Document.Platform == platform)
                documents.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VisualSearchHit>> SearchAsync(
        float[] queryVector, int k, string groupKey, string modelId, string excludeListingId,
        CancellationToken ct = default)
    {
        var hits = documents
            .Where(e => e.Value.Document.GroupKey == groupKey
                        && e.Value.Document.ModelId == modelId
                        && e.Value.Document.ListingId != excludeListingId)
            .Select(e => new VisualSearchHit(e.Key, VectorMath.CosineSimilarity(queryVector, e.Value.Document.Embedding), e.Value.Document))
            .OrderByDescending(h => h.Score)
            .Take(k)
            .ToList();
        return Task.FromResult<IReadOnlyList<VisualSearchHit>>(hits);
    }

    public Task<long> DeleteOlderThanAsync(DateTime cutoff, bool seed, CancellationToken ct = default)
    {
        long deleted = 0;
        foreach (var (id, entry) in documents)
        {
            if (entry.Document.Seed != seed || entry.Document.CreatedAt >= cutoff)
                continue;
            if (documents.TryRemove(id, out _))
                deleted++;
        }
        return Task.FromResult(deleted);
    }
}
