using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Embeddings;

/// <summary>
/// Cassandra-backed <see cref="IListingEmbeddingStore"/>. Columns are all plain scalar types (text, blob,
/// int, timestamp), so unlike <c>CassandraKnownProductStore</c> the driver's POCO-&gt;DDL inference via
/// <c>Table&lt;T&gt;.CreateIfNotExistsAsync()</c> is exact and no hand-written CQL is needed for the schema.
/// The <c>platform</c> column is backed by the <c>int</c> property
/// <see cref="ListingImageEmbedding.PlatformValue"/>; the enum property is ignored. <c>WithDbType&lt;int&gt;()</c>
/// on the enum is not enough for a partition key: routing key calculation serializes the raw value.
/// </summary>
public partial class CassandraListingEmbeddingStore : IListingEmbeddingStore
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
        table = new Table<ListingImageEmbedding>(session, BuildMapping());
    }

    /// <summary>Table mapping, separate from the constructor so tests can inspect it without a session.</summary>
    public static MappingConfiguration BuildMapping()
    {
        return new MappingConfiguration()
            .Define(new Map<ListingImageEmbedding>()
                .TableName("listing_image_embeddings")
                .PartitionKey(e => e.ListingId, e => e.PlatformValue)
                .ClusteringKey(e => e.ImageIndex)
                .Column(e => e.ListingId, cm => cm.WithName("listing_id"))
                .Column(e => e.PlatformValue, cm => cm.WithName("platform"))
                .Column(e => e.Platform, cm => cm.Ignore())
                .Column(e => e.ImageIndex, cm => cm.WithName("image_index"))
                .Column(e => e.ImageUrl, cm => cm.WithName("image_url"))
                .Column(e => e.ModelId, cm => cm.WithName("model_id"))
                .Column(e => e.Embedding, cm => cm.WithName("embedding"))
                .Column(e => e.GroupKey, cm => cm.WithName("group_key"))
                .Column(e => e.ClusterSeoId, cm => cm.WithName("cluster_seo_id"))
                .Column(e => e.CreatedAt, cm => cm.WithName("created_at")));
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
        var platformValue = (int)platform;
        var rows = await table
            .Where(e => e.ListingId == listingId && e.PlatformValue == platformValue)
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
        var platformValue = (int)platform;
        var rows = await table
            .Where(e => e.ListingId == listingId && e.PlatformValue == platformValue)
            .ExecuteAsync();
        foreach (var row in rows)
        {
            var imageIndex = row.ImageIndex;
            await table
                .Where(e => e.ListingId == listingId && e.PlatformValue == platformValue && e.ImageIndex == imageIndex)
                .Select(e => new ListingImageEmbedding { ClusterSeoId = clusterSeoId })
                .Update()
                .ExecuteAsync();
        }
    }
}
