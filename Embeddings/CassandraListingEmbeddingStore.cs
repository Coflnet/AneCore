using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Embeddings;

/// <summary>
/// Cassandra-backed <see cref="IListingEmbeddingStore"/>. Columns are all plain scalar types (text, blob,
/// int, timestamp), so unlike <c>CassandraKnownProductStore</c> the driver's POCO-&gt;DDL inference via
/// <c>Table&lt;T&gt;.CreateIfNotExistsAsync()</c> is exact and no hand-written CQL is needed for the schema.
/// <see cref="ListingImageEmbedding.Platform"/> is mapped as <c>int</c> (<c>WithDbType&lt;int&gt;()</c>), not
/// as the enum itself - the Cassandra C# driver cannot map enum-typed properties directly.
/// </summary>
public class CassandraListingEmbeddingStore : IListingEmbeddingStore
{
    /// <summary>14 days, matching the notifier's re-embedding cadence - a listing that stops being seen just ages out.</summary>
    public const int TimeToLiveSeconds = 14 * 24 * 60 * 60;

    private readonly ISession session;
    private readonly Table<ListingImageEmbedding> table;
    private static bool tableInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public CassandraListingEmbeddingStore(ISession session)
    {
        this.session = session;

        var mapping = new MappingConfiguration()
            .Define(new Map<ListingImageEmbedding>()
                .TableName("listing_image_embeddings")
                .PartitionKey(e => e.ListingId, e => e.Platform)
                .ClusteringKey(e => e.ImageIndex)
                .Column(e => e.ListingId, cm => cm.WithName("listing_id"))
                .Column(e => e.Platform, cm => cm.WithDbType<int>().WithName("platform"))
                .Column(e => e.ImageIndex, cm => cm.WithName("image_index"))
                .Column(e => e.ImageUrl, cm => cm.WithName("image_url"))
                .Column(e => e.ModelId, cm => cm.WithName("model_id"))
                .Column(e => e.Embedding, cm => cm.WithName("embedding"))
                .Column(e => e.GroupKey, cm => cm.WithName("group_key"))
                .Column(e => e.ClusterSeoId, cm => cm.WithName("cluster_seo_id"))
                .Column(e => e.CreatedAt, cm => cm.WithName("created_at")));

        table = new Table<ListingImageEmbedding>(session, mapping);
    }

    public async Task InitializeAsync()
    {
        if (tableInitialized) return;
        await InitLock.WaitAsync();
        try
        {
            if (tableInitialized) return;
            await table.CreateIfNotExistsAsync();
            await session.ExecuteAsync(new SimpleStatement(
                $"ALTER TABLE listing_image_embeddings WITH default_time_to_live = {TimeToLiveSeconds}"));
            tableInitialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public async Task<IReadOnlyList<ListingImageEmbedding>> GetEmbeddingsAsync(string listingId, Platform platform)
    {
        var rows = await table
            .Where(e => e.ListingId == listingId && e.Platform == platform)
            .ExecuteAsync();
        return rows.OrderBy(e => e.ImageIndex).ToList();
    }

    public async Task UpsertEmbeddingsAsync(IEnumerable<ListingImageEmbedding> embeddings)
    {
        foreach (var embedding in embeddings)
            await table.Insert(embedding).SetTTL(TimeToLiveSeconds).ExecuteAsync();
    }

    /// <summary>
    /// There is one row per photo (clustering key <c>image_index</c>), so setting the cluster assignment
    /// "for a listing" means a read of the current image indices followed by one partial-column update per
    /// row - CQL has no way to update a whole partition in one statement unless the column is static, and
    /// making it static would force every photo of the listing to share a single cluster even before it
    /// exists here, which is a decision that belongs to the future clustering job, not this storage layer.
    /// </summary>
    public async Task SetClusterSeoIdAsync(string listingId, Platform platform, string? clusterSeoId)
    {
        var rows = await table
            .Where(e => e.ListingId == listingId && e.Platform == platform)
            .ExecuteAsync();
        foreach (var row in rows)
        {
            await table
                .Where(e => e.ListingId == listingId && e.Platform == platform && e.ImageIndex == row.ImageIndex)
                .Select(e => new ListingImageEmbedding { ClusterSeoId = clusterSeoId })
                .Update()
                .ExecuteAsync();
        }
    }
}
