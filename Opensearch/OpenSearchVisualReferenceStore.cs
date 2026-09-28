using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

/// <summary>
/// OpenSearch-backed <see cref="IVisualReferenceStore"/> on top of <see cref="ClothingVisualIndex"/>.
/// </summary>
public class OpenSearchVisualReferenceStore(
    ClothingVisualIndex index,
    ILogger<OpenSearchVisualReferenceStore> logger) : IVisualReferenceStore
{
    private volatile bool available;

    public bool IsAvailable => available;

    /// <summary>
    /// Creates the index (idempotent) and pings the cluster. Any failure - most notably the cluster
    /// lacking the k-NN plugin, which makes the <c>knn_vector</c> mapping in <see cref="ClothingVisualIndex.IndexFunc"/>
    /// get rejected - is caught here, logged once as a warning, and leaves <see cref="IsAvailable"/> false;
    /// it is never rethrown, so this can safely be awaited from service startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await index.Client(ct);
            available = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            available = false;
            logger.LogWarning(ex,
                "Clothing visual index {Index} is unavailable (the cluster likely lacks the k-NN plugin); " +
                "visual clustering will be skipped until this is resolved.", index.IndexName());
        }
    }

    public async Task IndexAsync(int imageIndex, ClothingVisualDocument document, CancellationToken ct = default)
    {
        if (!available)
            return;
        var client = await index.Client(ct);
        var id = ClothingVisualIndex.BuildDocumentId(document.Platform, document.ListingId, imageIndex);
        var response = await client.IndexAsync(document, i => i.Index(index.IndexName()).Id(id), ct);
        if (!response.IsValid)
            throw new InvalidOperationException($"Failed to index clothing visual document {id}: {response.DebugInformation}");
    }

    public async Task DeleteByListingAsync(string listingId, string platform, CancellationToken ct = default)
    {
        if (!available)
            return;
        var client = await index.Client(ct);
        var response = await client.DeleteByQueryAsync<ClothingVisualDocument>(d => d
            .Index(index.IndexName())
            .Conflicts(Conflicts.Proceed)
            .Query(q => q.Bool(b => b.Filter(
                f => f.Term(t => t.Field(x => x.ListingId).Value(listingId)),
                f => f.Term(t => t.Field(x => x.Platform).Value(platform))))),
            ct);
        if (!response.IsValid)
            throw new InvalidOperationException(
                $"Failed to delete clothing visual documents for listing {listingId}: {response.DebugInformation}");
    }

    public async Task<IReadOnlyList<VisualSearchHit>> SearchAsync(
        float[] queryVector, int k, string groupKey, string modelId, string excludeListingId,
        CancellationToken ct = default)
    {
        if (!available)
            return [];
        var client = await index.Client(ct);
        var response = await client.SearchAsync<ClothingVisualDocument>(s => s
            .Index(index.IndexName())
            .Size(k)
            .Query(q => q.Knn(kn => kn
                .Field(f => f.Embedding)
                .Vector(queryVector)
                .K(k)
                .Filter(f => f.Bool(b => b
                    .Filter(
                        ff => ff.Term(t => t.Field(x => x.GroupKey).Value(groupKey)),
                        ff => ff.Term(t => t.Field(x => x.ModelId).Value(modelId)))
                    .MustNot(mn => mn.Term(t => t.Field(x => x.ListingId).Value(excludeListingId))))))),
            ct);
        if (!response.IsValid)
            throw new InvalidOperationException($"Clothing visual kNN search failed: {response.DebugInformation}");

        return response.Hits
            .Select(h => new VisualSearchHit(h.Id, h.Score ?? 0, h.Source))
            .ToList();
    }

    public async Task<long> DeleteOlderThanAsync(DateTime cutoff, bool seed, CancellationToken ct = default)
    {
        if (!available)
            return 0;
        var client = await index.Client(ct);
        var response = await client.DeleteByQueryAsync<ClothingVisualDocument>(d => d
            .Index(index.IndexName())
            .Conflicts(Conflicts.Proceed)
            .Query(q => q.Bool(b => b.Filter(
                f => f.DateRange(r => r.Field(x => x.CreatedAt).LessThan(cutoff)),
                f => f.Term(t => t.Field(x => x.Seed).Value(seed))))),
            ct);
        if (!response.IsValid)
            throw new InvalidOperationException($"Failed to delete expired clothing visual documents: {response.DebugInformation}");
        return response.Deleted;
    }
}
