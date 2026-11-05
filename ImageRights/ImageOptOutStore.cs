using System.Collections.Concurrent;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.ImageRights;

/// <summary>Monotonic per-listing image withdrawal, independent of host permissions and account deletion.</summary>
public interface IImageOptOutStore
{
    Task MarkOptedOutAsync(Platform platform, string listingId);
    Task<bool> IsOptedOutAsync(Platform platform, string listingId);
}

/// <summary>Stores no image or seller data. Markers never expire and there is no automatic reinstatement.</summary>
public sealed class CassandraImageOptOutStore(ISession session, bool createTable = true) : IImageOptOutStore
{
    private readonly Table<ImageOptOutMarker> markers = new(session, BuildMapping());
    private readonly SemaphoreSlim initialization = new(1, 1);
    private bool initialized;

    public static MappingConfiguration BuildMapping() => new MappingConfiguration().Define(new Map<ImageOptOutMarker>()
        .TableName("image_opt_outs").PartitionKey(row => row.PlatformValue, row => row.ListingId)
        .Column(row => row.PlatformValue, column => column.WithName("platform"))
        .Column(row => row.ListingId, column => column.WithName("listing_id"))
        .Column(row => row.OptedOutAt, column => column.WithName("opted_out_at").WithDbType<DateTime>()));

    public async Task InitializeAsync()
    {
        if (!createTable || initialized) return;
        await initialization.WaitAsync();
        try
        {
            if (initialized) return;
            await markers.CreateIfNotExistsAsync();
            initialized = true;
        }
        finally { initialization.Release(); }
    }

    public async Task MarkOptedOutAsync(Platform platform, string listingId)
    {
        Validate(platform, listingId);
        await InitializeAsync();
        await session.ExecuteAsync(new global::Cassandra.SimpleStatement(
            "INSERT INTO image_opt_outs (platform, listing_id, opted_out_at) VALUES (?, ?, ?) USING TTL 0",
            (int)platform, listingId, DateTime.UtcNow).SetConsistencyLevel(global::Cassandra.ConsistencyLevel.LocalQuorum));
    }

    public async Task<bool> IsOptedOutAsync(Platform platform, string listingId)
    {
        Validate(platform, listingId);
        await InitializeAsync();
        // No cached negative permission: a withdrawal on another replica must take effect on the next check.
        return (await session.ExecuteAsync(new global::Cassandra.SimpleStatement(
            "SELECT opted_out_at FROM image_opt_outs WHERE platform = ? AND listing_id = ?", (int)platform, listingId)
            .SetConsistencyLevel(global::Cassandra.ConsistencyLevel.LocalQuorum))).Any();
    }

    internal static void Validate(Platform platform, string listingId)
    {
        if (!Enum.IsDefined(platform) || platform == Platform.Unknown || string.IsNullOrWhiteSpace(listingId))
            throw new ArgumentException("An image withdrawal requires an exact known platform and listing id");
    }
}

public sealed class ImageOptOutMarker
{
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    public DateTime OptedOutAt { get; set; }
}

public sealed class InMemoryImageOptOutStore : IImageOptOutStore
{
    private readonly ConcurrentDictionary<(Platform Platform, string ListingId), byte> markers = new();
    public Task MarkOptedOutAsync(Platform platform, string listingId)
    {
        CassandraImageOptOutStore.Validate(platform, listingId);
        markers[(platform, listingId)] = 0;
        return Task.CompletedTask;
    }
    public Task<bool> IsOptedOutAsync(Platform platform, string listingId)
    {
        CassandraImageOptOutStore.Validate(platform, listingId);
        return Task.FromResult(markers.ContainsKey((platform, listingId)));
    }
}
