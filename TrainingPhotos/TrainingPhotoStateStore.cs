using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.TrainingPhotos;

/// <summary>
/// Row of <c>training_photo_rechecks</c>, partition <c>(platform, listing_id)</c>: what the training photo job knows about the
/// availability recheck of one offer (latest result and the requests it sent). Written with a TTL. Platform and status are ints.
/// </summary>
public class TrainingRecheckRow
{
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    /// <summary>Value of <see cref="RecheckStatus"/> of the latest result, null while none arrived.</summary>
    public int? StatusValue { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? UnknownReason { get; set; }
    public DateTime? RequestedAt { get; set; }
    public int Attempts { get; set; }

    public TrainingRecheckRow Clone() => (TrainingRecheckRow)MemberwiseClone();
}

/// <summary>
/// Row of <c>training_photo_awaiting</c>, partition <c>day</c> (UTC day of <see cref="RequestedAt"/>), clustering
/// <c>(platform, listing_id, image_index)</c>: a photo the job requested a recheck for and still has to archive. Written with a TTL.
/// </summary>
public class TrainingAwaitingRow
{
    public string Day { get; set; } = "";
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    public int ImageIndex { get; set; }
    public string ImageUrl { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Time of the candidate itself (pair or embedding creation), for the minimum age check.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the offer was first remembered; the row expires the retention after it.</summary>
    public DateTime RequestedAt { get; set; }

    public TrainingAwaitingRow Clone() => (TrainingAwaitingRow)MemberwiseClone();
}

/// <summary>
/// State of the training photo job that has to survive a restart of the notifier: recheck confirmations and pending requests,
/// the time of the last run and the archived photos per group and day.
/// </summary>
public interface ITrainingPhotoStateStore
{
    /// <summary>Creates the tables when the store was built with createTables (otherwise a no-op).</summary>
    Task InitializeAsync();

    /// <summary>Stores the row (replacing an existing one) and lets it expire after <paramref name="ttl"/>.</summary>
    Task SaveRecheckAsync(TrainingRecheckRow row, TimeSpan ttl);

    Task<TrainingRecheckRow?> GetRecheckAsync(Platform platform, string listingId);

    /// <summary>When the last run completed, null when none is known.</summary>
    Task<DateTime?> GetLastRunAsync();

    Task SetLastRunAsync(DateTime utc);

    /// <summary>Photos archived per group key on one UTC day.</summary>
    Task<IReadOnlyDictionary<string, int>> GetGroupCountsAsync(DateOnly day);

    /// <summary>Sets (not increments) the count of one group and day, so repeating a write is harmless.</summary>
    Task SetGroupCountAsync(DateOnly day, string groupKey, int count);

    /// <summary>Stores a photo the job still has to archive (replacing the same photo); <see cref="TrainingAwaitingRow.Day"/> is set from <see cref="TrainingAwaitingRow.RequestedAt"/>.</summary>
    Task SaveAwaitingAsync(TrainingAwaitingRow row, TimeSpan ttl);

    /// <summary>Removes the photo, whatever its request day.</summary>
    Task DeleteAwaitingAsync(TrainingAwaitingRow row);

    /// <summary>All stored photos whose request is not older than <paramref name="maxAge"/>, oldest request first.</summary>
    Task<IReadOnlyList<TrainingAwaitingRow>> ListAwaitingAsync(DateTime now, TimeSpan maxAge);
}

/// <summary>Cassandra-backed state store. Only int/text/timestamp columns; platforms are ints, never enum parameters.</summary>
public class CassandraTrainingPhotoStateStore : ITrainingPhotoStateStore
{
    private const string RunKey = "last_run";
    /// <summary>Group counters are only read for the current day, two days of retention is plenty.</summary>
    public static readonly TimeSpan GroupCountTtl = TimeSpan.FromDays(2);

    private readonly ISession session;
    private readonly bool createTables;
    private readonly Table<TrainingRecheckRow> rechecks;
    private readonly Table<TrainingRunStateRow> runState;
    private readonly Table<TrainingGroupCountRow> groupCounts;
    private readonly Table<TrainingAwaitingRow> awaiting;
    private readonly IMapper mapper;
    private static bool tablesInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    /// <param name="createTables">Whether <see cref="InitializeAsync"/> creates the tables.</param>
    public CassandraTrainingPhotoStateStore(ISession session, bool createTables = true)
    {
        this.session = session;
        this.createTables = createTables;
        var mapping = BuildMapping();
        rechecks = new Table<TrainingRecheckRow>(session, mapping);
        runState = new Table<TrainingRunStateRow>(session, mapping);
        groupCounts = new Table<TrainingGroupCountRow>(session, mapping);
        awaiting = new Table<TrainingAwaitingRow>(session, mapping);
        mapper = new Mapper(session, mapping);
    }

    public static MappingConfiguration BuildMapping() =>
        new MappingConfiguration()
            .Define(new Map<TrainingRecheckRow>()
                .TableName("training_photo_rechecks")
                .PartitionKey(r => r.PlatformValue, r => r.ListingId)
                .Column(r => r.PlatformValue, cm => cm.WithName("platform"))
                .Column(r => r.ListingId, cm => cm.WithName("listing_id"))
                .Column(r => r.StatusValue, cm => cm.WithName("status"))
                .Column(r => r.CheckedAt, cm => cm.WithName("checked_at"))
                .Column(r => r.UnknownReason, cm => cm.WithName("unknown_reason"))
                .Column(r => r.RequestedAt, cm => cm.WithName("requested_at"))
                .Column(r => r.Attempts, cm => cm.WithName("attempts")))
            .Define(new Map<TrainingRunStateRow>()
                .TableName("training_photo_run_state")
                .PartitionKey(r => r.Name)
                .Column(r => r.Name, cm => cm.WithName("name"))
                .Column(r => r.At, cm => cm.WithName("at")))
            .Define(new Map<TrainingGroupCountRow>()
                .TableName("training_photo_group_counts")
                .PartitionKey(r => r.Day)
                .ClusteringKey(r => r.GroupKey)
                .Column(r => r.Day, cm => cm.WithName("day"))
                .Column(r => r.GroupKey, cm => cm.WithName("group_key"))
                .Column(r => r.Photos, cm => cm.WithName("photos")))
            .Define(new Map<TrainingAwaitingRow>()
                .TableName("training_photo_awaiting")
                .PartitionKey(r => r.Day)
                .ClusteringKey(r => r.PlatformValue).ClusteringKey(r => r.ListingId).ClusteringKey(r => r.ImageIndex)
                .Column(r => r.Day, cm => cm.WithName("day"))
                .Column(r => r.PlatformValue, cm => cm.WithName("platform"))
                .Column(r => r.ListingId, cm => cm.WithName("listing_id"))
                .Column(r => r.ImageIndex, cm => cm.WithName("image_index"))
                .Column(r => r.ImageUrl, cm => cm.WithName("image_url"))
                .Column(r => r.GroupKey, cm => cm.WithName("group_key"))
                .Column(r => r.Source, cm => cm.WithName("source"))
                .Column(r => r.CreatedAt, cm => cm.WithName("created_at"))
                .Column(r => r.RequestedAt, cm => cm.WithName("requested_at")));

    public async Task InitializeAsync()
    {
        if (tablesInitialized || !createTables) return;
        await InitLock.WaitAsync();
        try
        {
            if (tablesInitialized) return;
            await rechecks.CreateIfNotExistsAsync();
            await runState.CreateIfNotExistsAsync();
            await groupCounts.CreateIfNotExistsAsync();
            await awaiting.CreateIfNotExistsAsync();
            tablesInitialized = true;
        }
        finally { InitLock.Release(); }
    }

    public async Task SaveRecheckAsync(TrainingRecheckRow row, TimeSpan ttl)
    {
        await InitializeAsync();
        await rechecks.Insert(row).SetTTL(Math.Max(1, (int)ttl.TotalSeconds)).ExecuteAsync();
    }

    public async Task<TrainingRecheckRow?> GetRecheckAsync(Platform platform, string listingId)
    {
        await InitializeAsync();
        var platformValue = (int)platform;
        var rows = await mapper.FetchAsync<TrainingRecheckRow>(
            "SELECT platform, listing_id, status, checked_at, unknown_reason, requested_at, attempts FROM training_photo_rechecks WHERE platform = ? AND listing_id = ?",
            platformValue, listingId);
        return rows.FirstOrDefault();
    }

    public async Task<DateTime?> GetLastRunAsync()
    {
        await InitializeAsync();
        var rows = await mapper.FetchAsync<TrainingRunStateRow>("SELECT name, at FROM training_photo_run_state WHERE name = ?", RunKey);
        var at = rows.FirstOrDefault()?.At;
        return at == null ? null : DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);
    }

    public async Task SetLastRunAsync(DateTime utc)
    {
        await InitializeAsync();
        await runState.Insert(new TrainingRunStateRow { Name = RunKey, At = utc.ToUniversalTime() }).ExecuteAsync();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetGroupCountsAsync(DateOnly day)
    {
        await InitializeAsync();
        var rows = await mapper.FetchAsync<TrainingGroupCountRow>(
            "SELECT day, group_key, photos FROM training_photo_group_counts WHERE day = ?", TrainingPhotoKeys.DayBucket(day));
        return rows.ToDictionary(r => r.GroupKey, r => r.Photos);
    }

    public async Task SetGroupCountAsync(DateOnly day, string groupKey, int count)
    {
        await InitializeAsync();
        await groupCounts.Insert(new TrainingGroupCountRow { Day = TrainingPhotoKeys.DayBucket(day), GroupKey = groupKey, Photos = count })
            .SetTTL((int)GroupCountTtl.TotalSeconds).ExecuteAsync();
    }

    public async Task SaveAwaitingAsync(TrainingAwaitingRow row, TimeSpan ttl)
    {
        await InitializeAsync();
        row.Day = TrainingPhotoKeys.DayBucket(row.RequestedAt);
        await awaiting.Insert(row).SetTTL(Math.Max(1, (int)ttl.TotalSeconds)).ExecuteAsync();
    }

    public async Task DeleteAwaitingAsync(TrainingAwaitingRow row)
    {
        await InitializeAsync();
        await mapper.ExecuteAsync("DELETE FROM training_photo_awaiting WHERE day = ? AND platform = ? AND listing_id = ? AND image_index = ?",
            TrainingPhotoKeys.DayBucket(row.RequestedAt), row.PlatformValue, row.ListingId, row.ImageIndex);
    }

    public async Task<IReadOnlyList<TrainingAwaitingRow>> ListAwaitingAsync(DateTime now, TimeSpan maxAge)
    {
        await InitializeAsync();
        var result = new List<TrainingAwaitingRow>();
        for (var day = DateOnly.FromDateTime((now - maxAge).ToUniversalTime()); day <= DateOnly.FromDateTime(now.ToUniversalTime()); day = day.AddDays(1))
        {
            // the mapper pages by itself until the partition is read; the partition is small (rows of one day, 24 h TTL)
            var rows = await mapper.FetchAsync<TrainingAwaitingRow>(
                "SELECT day, platform, listing_id, image_index, image_url, group_key, source, created_at, requested_at FROM training_photo_awaiting WHERE day = ?",
                TrainingPhotoKeys.DayBucket(day));
            result.AddRange(rows.Select(r =>
            {
                r.CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc);
                r.RequestedAt = DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc);
                return r;
            }));
        }
        return result.Where(r => now - r.RequestedAt <= maxAge).OrderBy(r => r.RequestedAt).ToList();
    }
}

/// <summary>Row of <c>training_photo_run_state</c>: a named point in time.</summary>
public class TrainingRunStateRow
{
    public string Name { get; set; } = "";
    public DateTime? At { get; set; }
}

/// <summary>Row of <c>training_photo_group_counts</c>, partition <c>day</c>, clustering <c>group_key</c>.</summary>
public class TrainingGroupCountRow
{
    public string Day { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public int Photos { get; set; }
}

/// <summary>In-memory state store for tests, same semantics as the Cassandra one (expiry is applied when reading).</summary>
public class InMemoryTrainingPhotoStateStore : ITrainingPhotoStateStore
{
    private readonly object gate = new();
    private readonly Func<DateTime> clock;
    private readonly Dictionary<(int, string), (TrainingRecheckRow Row, DateTime ExpiresAt)> rechecks = new();
    private readonly Dictionary<(string, string), int> groups = new();
    private readonly Dictionary<(int, string, int), (TrainingAwaitingRow Row, DateTime ExpiresAt)> awaiting = new();
    private DateTime? lastRun;

    public InMemoryTrainingPhotoStateStore(Func<DateTime>? clock = null) => this.clock = clock ?? (() => DateTime.UtcNow);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task SaveRecheckAsync(TrainingRecheckRow row, TimeSpan ttl)
    {
        lock (gate) rechecks[(row.PlatformValue, row.ListingId)] = (row.Clone(), clock() + ttl);
        return Task.CompletedTask;
    }

    public Task<TrainingRecheckRow?> GetRecheckAsync(Platform platform, string listingId)
    {
        lock (gate)
            return Task.FromResult(rechecks.TryGetValue(((int)platform, listingId), out var e) && e.ExpiresAt > clock() ? e.Row.Clone() : null);
    }

    public Task<DateTime?> GetLastRunAsync()
    {
        lock (gate) return Task.FromResult(lastRun);
    }

    public Task SetLastRunAsync(DateTime utc)
    {
        lock (gate) lastRun = utc.ToUniversalTime();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, int>> GetGroupCountsAsync(DateOnly day)
    {
        var key = TrainingPhotoKeys.DayBucket(day);
        lock (gate)
            return Task.FromResult<IReadOnlyDictionary<string, int>>(groups.Where(g => g.Key.Item1 == key).ToDictionary(g => g.Key.Item2, g => g.Value));
    }

    public Task SetGroupCountAsync(DateOnly day, string groupKey, int count)
    {
        lock (gate) groups[(TrainingPhotoKeys.DayBucket(day), groupKey)] = count;
        return Task.CompletedTask;
    }

    public Task SaveAwaitingAsync(TrainingAwaitingRow row, TimeSpan ttl)
    {
        var copy = row.Clone();
        copy.Day = TrainingPhotoKeys.DayBucket(copy.RequestedAt);
        lock (gate) awaiting[(copy.PlatformValue, copy.ListingId, copy.ImageIndex)] = (copy, clock() + ttl);
        return Task.CompletedTask;
    }

    public Task DeleteAwaitingAsync(TrainingAwaitingRow row)
    {
        lock (gate) awaiting.Remove((row.PlatformValue, row.ListingId, row.ImageIndex));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrainingAwaitingRow>> ListAwaitingAsync(DateTime now, TimeSpan maxAge)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<TrainingAwaitingRow>>(awaiting.Values
                .Where(e => e.ExpiresAt > clock() && now - e.Row.RequestedAt <= maxAge)
                .Select(e => e.Row.Clone()).OrderBy(r => r.RequestedAt).ToList());
}
}
