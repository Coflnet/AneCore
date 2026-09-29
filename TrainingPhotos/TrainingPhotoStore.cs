using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.TrainingPhotos;

public interface ITrainingPhotoStore
{
    /// <summary>Creates the tables when the store was built with createTables (otherwise a no-op).</summary>
    Task InitializeAsync();

    /// <summary>Stores the photo and its day index row. Idempotent on (platform, listing id, image index): returns false and changes nothing when it exists already.</summary>
    Task<bool> AddAsync(TrainingPhoto photo);

    /// <summary>The photo, or null. Without <paramref name="includeJpeg"/> the jpeg column is not read (<see cref="TrainingPhoto.Jpeg"/> stays null).</summary>
    Task<TrainingPhoto?> GetAsync(Platform platform, string listingId, int imageIndex, bool includeJpeg = false);

    Task<bool> ExistsAsync(Platform platform, string listingId, int imageIndex);

    /// <summary>All archived photos of one offer ordered by image index.</summary>
    Task<IReadOnlyList<TrainingPhoto>> GetForListingAsync(Platform platform, string listingId, bool includeJpeg = false);

    /// <summary>Up to <paramref name="limit"/> index rows of one UTC day, newest first.</summary>
    Task<IReadOnlyList<TrainingPhotoDayEntry>> ListDayAsync(DateOnly day, int limit);

    Task<long> CountDayAsync(DateOnly day);

    /// <summary>Total, count per host and the count archived at or after <paramref name="since"/> of one day, from the index only.</summary>
    Task<TrainingPhotoDaySummary> GetDaySummaryAsync(DateOnly day, DateTime since);

    /// <summary>Removes the photo rows and the day index rows of an offer; returns the number of photos removed (0 when there were none).</summary>
    Task<int> DeleteListingAsync(Platform platform, string listingId);

    /// <summary>
    /// Removes the photos of <paramref name="host"/> archived before <paramref name="olderThan"/> (walking the day index from
    /// <paramref name="fromDay"/>, default <see cref="TrainingPhotoKeys.EarliestDay"/>); returns the number removed.
    /// </summary>
    Task<int> DeleteHostAsync(string host, DateTime olderThan, DateOnly? fromDay = null);
}

/// <summary>Cassandra-backed store. Only int/text/blob/timestamp columns; platforms are ints, never enum parameters.</summary>
public class CassandraTrainingPhotoStore : ITrainingPhotoStore
{
    private const string LightColumns =
        "platform, listing_id, image_index, image_url, host, width, height, sha256, embedding, model_id, group_key, brand, garment_type, " +
        "category, first_seen_at, verified_online_at, archived_at, rights_status, rights_checked_at, source";
    private const string FullColumns = LightColumns + ", jpeg";

    private readonly ISession session;
    private readonly bool createTables;
    private readonly Table<TrainingPhoto> photos;
    private readonly Table<TrainingPhotoDayEntry> days;
    private readonly IMapper mapper;
    private static bool tablesInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    /// <param name="createTables">
    /// Whether <see cref="InitializeAsync"/> creates the tables. AneApi creates them at start, every other service passes false.
    /// </param>
    public CassandraTrainingPhotoStore(ISession session, bool createTables = true)
    {
        this.session = session;
        this.createTables = createTables;
        var mapping = BuildMapping();
        photos = new Table<TrainingPhoto>(session, mapping);
        days = new Table<TrainingPhotoDayEntry>(session, mapping);
        mapper = new Mapper(session, mapping);
    }

    public static MappingConfiguration BuildMapping() =>
        new MappingConfiguration()
            .Define(new Map<TrainingPhoto>()
                .TableName("training_photos")
                .PartitionKey(p => p.PlatformValue, p => p.ListingId)
                .ClusteringKey(p => p.ImageIndex)
                .Column(p => p.PlatformValue, cm => cm.WithName("platform"))
                .Column(p => p.ListingId, cm => cm.WithName("listing_id"))
                .Column(p => p.ImageIndex, cm => cm.WithName("image_index"))
                .Column(p => p.ImageUrl, cm => cm.WithName("image_url"))
                .Column(p => p.Host, cm => cm.WithName("host"))
                .Column(p => p.Jpeg, cm => cm.WithName("jpeg"))
                .Column(p => p.Width, cm => cm.WithName("width"))
                .Column(p => p.Height, cm => cm.WithName("height"))
                .Column(p => p.Sha256, cm => cm.WithName("sha256"))
                .Column(p => p.Embedding, cm => cm.WithName("embedding"))
                .Column(p => p.ModelId, cm => cm.WithName("model_id"))
                .Column(p => p.GroupKey, cm => cm.WithName("group_key"))
                .Column(p => p.Brand, cm => cm.WithName("brand"))
                .Column(p => p.GarmentType, cm => cm.WithName("garment_type"))
                .Column(p => p.Category, cm => cm.WithName("category"))
                .Column(p => p.FirstSeenAt, cm => cm.WithName("first_seen_at"))
                .Column(p => p.VerifiedOnlineAt, cm => cm.WithName("verified_online_at"))
                .Column(p => p.ArchivedAt, cm => cm.WithName("archived_at"))
                .Column(p => p.RightsStatus, cm => cm.WithName("rights_status"))
                .Column(p => p.RightsCheckedAt, cm => cm.WithName("rights_checked_at"))
                .Column(p => p.Source, cm => cm.WithName("source"))
                .Column(p => p.Platform, cm => cm.Ignore())
                .Column(p => p.EmbeddingVector, cm => cm.Ignore()))
            .Define(new Map<TrainingPhotoDayEntry>()
                .TableName("training_photos_by_day")
                .PartitionKey(e => e.Day, e => e.Shard)
                .ClusteringKey(e => e.PlatformValue)
                .ClusteringKey(e => e.ListingId)
                .ClusteringKey(e => e.ImageIndex)
                .Column(e => e.Day, cm => cm.WithName("day"))
                .Column(e => e.Shard, cm => cm.WithName("shard"))
                .Column(e => e.PlatformValue, cm => cm.WithName("platform"))
                .Column(e => e.ListingId, cm => cm.WithName("listing_id"))
                .Column(e => e.ImageIndex, cm => cm.WithName("image_index"))
                .Column(e => e.Host, cm => cm.WithName("host"))
                .Column(e => e.ArchivedAt, cm => cm.WithName("archived_at"))
                .Column(e => e.Platform, cm => cm.Ignore()));

    public async Task InitializeAsync()
    {
        if (tablesInitialized || !createTables) return;
        await InitLock.WaitAsync();
        try
        {
            if (tablesInitialized) return;
            await photos.CreateIfNotExistsAsync();
            await days.CreateIfNotExistsAsync();
            tablesInitialized = true;
        }
        finally { InitLock.Release(); }
    }

    public async Task<bool> AddAsync(TrainingPhoto photo)
    {
        await InitializeAsync();
        photo.Host = photo.Host.ToLowerInvariant();
        photo.ArchivedAt ??= DateTime.UtcNow;
        var result = await photos.Insert(photo).IfNotExists().ExecuteAsync();
        if (!result.Applied)
            return false;
        await days.Insert(ToEntry(photo)).ExecuteAsync();
        return true;
    }

    private static TrainingPhotoDayEntry ToEntry(TrainingPhoto p) => new()
    {
        Day = TrainingPhotoKeys.DayBucket(p.ArchivedAt!.Value),
        Shard = TrainingPhotoKeys.ShardOf(p.PlatformValue, p.ListingId),
        PlatformValue = p.PlatformValue, ListingId = p.ListingId, ImageIndex = p.ImageIndex,
        Host = p.Host, ArchivedAt = p.ArchivedAt!.Value
    };

    public async Task<TrainingPhoto?> GetAsync(Platform platform, string listingId, int imageIndex, bool includeJpeg = false)
    {
        await InitializeAsync();
        var columns = includeJpeg ? FullColumns : LightColumns;
        var platformValue = (int)platform;
        var rows = await mapper.FetchAsync<TrainingPhoto>(
            $"SELECT {columns} FROM training_photos WHERE platform = ? AND listing_id = ? AND image_index = ?",
            platformValue, listingId, imageIndex);
        return rows.FirstOrDefault();
    }

    public async Task<bool> ExistsAsync(Platform platform, string listingId, int imageIndex)
    {
        await InitializeAsync();
        var platformValue = (int)platform;
        var rows = await mapper.FetchAsync<int>(
            "SELECT image_index FROM training_photos WHERE platform = ? AND listing_id = ? AND image_index = ?",
            platformValue, listingId, imageIndex);
        return rows.Any();
    }

    public async Task<IReadOnlyList<TrainingPhoto>> GetForListingAsync(Platform platform, string listingId, bool includeJpeg = false)
    {
        await InitializeAsync();
        var columns = includeJpeg ? FullColumns : LightColumns;
        var platformValue = (int)platform;
        var rows = await mapper.FetchAsync<TrainingPhoto>(
            $"SELECT {columns} FROM training_photos WHERE platform = ? AND listing_id = ?", platformValue, listingId);
        return rows.OrderBy(p => p.ImageIndex).ToList();
    }

    public async Task<IReadOnlyList<TrainingPhotoDayEntry>> ListDayAsync(DateOnly day, int limit)
    {
        await InitializeAsync();
        var perShard = await Task.WhenAll(Enumerable.Range(0, TrainingPhotoKeys.ShardCount).Select(shard => ReadShardAsync(day, shard, limit)));
        return perShard.SelectMany(x => x).OrderByDescending(e => e.ArchivedAt).Take(limit).ToList();
    }

    private async Task<List<TrainingPhotoDayEntry>> ReadShardAsync(DateOnly day, int shard, int limit)
    {
        var dayKey = TrainingPhotoKeys.DayBucket(day);
        // a plain statement pages automatically, so a LIMIT is only a cap on the total
        var rows = await mapper.FetchAsync<TrainingPhotoDayEntry>(
            "SELECT day, shard, platform, listing_id, image_index, host, archived_at FROM training_photos_by_day WHERE day = ? AND shard = ? LIMIT ?",
            dayKey, shard, limit);
        return rows.ToList();
    }

    public async Task<long> CountDayAsync(DateOnly day)
    {
        await InitializeAsync();
        var dayKey = TrainingPhotoKeys.DayBucket(day);
        var counts = await Task.WhenAll(Enumerable.Range(0, TrainingPhotoKeys.ShardCount).Select(shard =>
            mapper.SingleAsync<long>("SELECT count(*) FROM training_photos_by_day WHERE day = ? AND shard = ?", dayKey, shard)));
        return counts.Sum();
    }

    public async Task<TrainingPhotoDaySummary> GetDaySummaryAsync(DateOnly day, DateTime since)
    {
        var entries = await ListDayAsync(day, int.MaxValue);
        return new TrainingPhotoDaySummary(entries.Count,
            entries.GroupBy(e => e.Host).ToDictionary(g => g.Key, g => (long)g.Count()),
            entries.Count(e => e.ArchivedAt >= since));
    }

    public async Task<int> DeleteListingAsync(Platform platform, string listingId)
    {
        await InitializeAsync();
        var existing = await GetForListingAsync(platform, listingId);
        if (existing.Count == 0)
            return 0;
        var platformValue = (int)platform;
        foreach (var photo in existing)
        {
            if (photo.ArchivedAt == null) continue;
            var entry = ToEntry(photo);
            await mapper.ExecuteAsync(
                "DELETE FROM training_photos_by_day WHERE day = ? AND shard = ? AND platform = ? AND listing_id = ? AND image_index = ?",
                entry.Day, entry.Shard, platformValue, listingId, photo.ImageIndex);
        }
        await mapper.ExecuteAsync("DELETE FROM training_photos WHERE platform = ? AND listing_id = ?", platformValue, listingId);
        return existing.Count;
    }

    public async Task<int> DeleteHostAsync(string host, DateTime olderThan, DateOnly? fromDay = null)
    {
        await InitializeAsync();
        var lower = host.ToLowerInvariant();
        var last = DateOnly.FromDateTime(olderThan.ToUniversalTime());
        var removed = 0;
        for (var day = fromDay ?? TrainingPhotoKeys.EarliestDay; day <= last; day = day.AddDays(1))
        {
            var entries = await ListDayAsync(day, int.MaxValue);
            foreach (var entry in entries.Where(e => e.Host == lower && e.ArchivedAt < olderThan))
            {
                var dayKey = TrainingPhotoKeys.DayBucket(entry.ArchivedAt);
                await mapper.ExecuteAsync(
                    "DELETE FROM training_photos WHERE platform = ? AND listing_id = ? AND image_index = ?",
                    entry.PlatformValue, entry.ListingId, entry.ImageIndex);
                await mapper.ExecuteAsync(
                    "DELETE FROM training_photos_by_day WHERE day = ? AND shard = ? AND platform = ? AND listing_id = ? AND image_index = ?",
                    dayKey, entry.Shard, entry.PlatformValue, entry.ListingId, entry.ImageIndex);
                removed++;
            }
        }
        return removed;
    }
}

/// <summary>In-memory store for tests; same semantics as the Cassandra one.</summary>
public class InMemoryTrainingPhotoStore : ITrainingPhotoStore
{
    private readonly object gate = new();
    private readonly Dictionary<(int Platform, string ListingId, int Index), TrainingPhoto> rows = new();

    public Task InitializeAsync() => Task.CompletedTask;

    private static TrainingPhoto Copy(TrainingPhoto p, bool includeJpeg)
    {
        var c = p.Clone();
        if (!includeJpeg) c.Jpeg = null;
        return c;
    }

    public Task<bool> AddAsync(TrainingPhoto photo)
    {
        lock (gate)
        {
            var key = (photo.PlatformValue, photo.ListingId, photo.ImageIndex);
            if (rows.ContainsKey(key))
                return Task.FromResult(false);
            photo.Host = photo.Host.ToLowerInvariant();
            photo.ArchivedAt ??= DateTime.UtcNow;
            rows[key] = Copy(photo, true);
            return Task.FromResult(true);
        }
    }

    public Task<TrainingPhoto?> GetAsync(Platform platform, string listingId, int imageIndex, bool includeJpeg = false)
    {
        lock (gate)
            return Task.FromResult(rows.TryGetValue(((int)platform, listingId, imageIndex), out var p) ? Copy(p, includeJpeg) : null);
    }

    public async Task<bool> ExistsAsync(Platform platform, string listingId, int imageIndex) =>
        await GetAsync(platform, listingId, imageIndex) != null;

    public Task<IReadOnlyList<TrainingPhoto>> GetForListingAsync(Platform platform, string listingId, bool includeJpeg = false)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<TrainingPhoto>>(rows.Values
                .Where(p => p.PlatformValue == (int)platform && p.ListingId == listingId)
                .OrderBy(p => p.ImageIndex).Select(p => Copy(p, includeJpeg)).ToList());
    }

    private List<TrainingPhotoDayEntry> Entries(DateOnly day)
    {
        var key = TrainingPhotoKeys.DayBucket(day);
        lock (gate)
            return rows.Values.Where(p => TrainingPhotoKeys.DayBucket(p.ArchivedAt!.Value) == key)
                .Select(p => new TrainingPhotoDayEntry
                {
                    Day = key, Shard = TrainingPhotoKeys.ShardOf(p.PlatformValue, p.ListingId), PlatformValue = p.PlatformValue,
                    ListingId = p.ListingId, ImageIndex = p.ImageIndex, Host = p.Host, ArchivedAt = p.ArchivedAt!.Value
                }).ToList();
    }

    public Task<IReadOnlyList<TrainingPhotoDayEntry>> ListDayAsync(DateOnly day, int limit) =>
        Task.FromResult<IReadOnlyList<TrainingPhotoDayEntry>>(Entries(day).OrderByDescending(e => e.ArchivedAt).Take(limit).ToList());

    public Task<long> CountDayAsync(DateOnly day) => Task.FromResult((long)Entries(day).Count);

    public Task<TrainingPhotoDaySummary> GetDaySummaryAsync(DateOnly day, DateTime since)
    {
        var entries = Entries(day);
        return Task.FromResult(new TrainingPhotoDaySummary(entries.Count,
            entries.GroupBy(e => e.Host).ToDictionary(g => g.Key, g => (long)g.Count()),
            entries.Count(e => e.ArchivedAt >= since)));
    }

    public Task<int> DeleteListingAsync(Platform platform, string listingId)
    {
        lock (gate)
        {
            var keys = rows.Keys.Where(k => k.Platform == (int)platform && k.ListingId == listingId).ToList();
            foreach (var k in keys) rows.Remove(k);
            return Task.FromResult(keys.Count);
        }
    }

    public Task<int> DeleteHostAsync(string host, DateTime olderThan, DateOnly? fromDay = null)
    {
        var lower = host.ToLowerInvariant();
        var from = (fromDay ?? TrainingPhotoKeys.EarliestDay).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        lock (gate)
        {
            var keys = rows.Where(kv => kv.Value.Host == lower && kv.Value.ArchivedAt < olderThan && kv.Value.ArchivedAt >= from)
                .Select(kv => kv.Key).ToList();
            foreach (var k in keys) rows.Remove(k);
            return Task.FromResult(keys.Count);
        }
    }
}
