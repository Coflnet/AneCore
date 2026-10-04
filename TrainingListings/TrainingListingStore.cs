using System.Text;
using Cassandra;
using Microsoft.Extensions.Logging;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.TrainingListings;

/// <summary>Position after a row of a partition: the clustering key <c>(platform, listing_id)</c>, opaque to callers.</summary>
public static class TrainingListingCursor
{
    public static string Encode(int platform, string listingId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{platform}:{listingId}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? cursor, out int platform, out string listingId)
    {
        platform = 0;
        listingId = "";
        if (string.IsNullOrEmpty(cursor))
            return false;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var split = text.IndexOf(':');
            if (split <= 0 || !int.TryParse(text.AsSpan(0, split), out platform))
                return false;
            listingId = text[(split + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Rows written to one day partition (all shards), see <see cref="ITrainingListingStore.ListDaysAsync"/>.</summary>
public sealed record TrainingListingDay(string Day, long Rows, IReadOnlyDictionary<int, long> RowsByShard);

/// <summary>
/// One page of a partition. <see cref="Rows"/> streams the listings (the store reads them in small pages while the caller consumes
/// them), <see cref="NextCursor"/> is known up front and null when the partition is finished.
/// </summary>
public sealed record TrainingListingPage(string? NextCursor, IAsyncEnumerable<TrainingListing> Rows);

/// <summary>
/// Store of <see cref="TrainingListing"/> rows for the export. Table <c>training_listings</c>, partition <c>(day, shard)</c> with 16 shards,
/// clustering <c>(platform, listing_id)</c>, TTL (default 14 days, the workstation holds the permanent copy).
/// </summary>
public interface ITrainingListingStore
{
    /// <summary>Creates the tables when the store was built with createTables (otherwise a no-op).</summary>
    Task InitializeAsync();

    /// <summary>
    /// Stores the listing in the partition of the UTC day of <see cref="TrainingListing.FirstSeenAt"/> (now when missing) and counts it.
    /// Idempotent per (day, platform, listing id): returns false and changes nothing when the row exists already, so a re-delivered message
    /// or a retried write never creates a second row. Without a first-seen time the row of today and of yesterday counts as existing (midnight).
    /// </summary>
    Task<bool> AddAsync(TrainingListing listing);

    /// <summary>Removes the listing from every day; returns the number of rows removed (takedown).</summary>
    Task<int> DeleteAsync(Platform platform, string listingId);

    /// <summary>The days that had rows written with the number of rows written, oldest first.</summary>
    Task<IReadOnlyList<TrainingListingDay>> ListDaysAsync();

    /// <summary>
    /// Up to <paramref name="limit"/> rows of one partition after <paramref name="cursor"/> (null starts at the beginning), ordered by
    /// (platform, listing id).
    /// </summary>
    Task<TrainingListingPage> ReadPartitionAsync(string day, int shard, int limit, string? cursor);
}

/// <summary>The two statements the store needs to write a listing; the seam lets tests simulate a slow or failing node.</summary>
public interface ITrainingListingCql
{
    /// <summary>Runs a select and tells whether it returned a row.</summary>
    Task<bool> AnyRowAsync(SimpleStatement statement);
    Task ExecuteAsync(SimpleStatement statement);
}

internal sealed class SessionCql(ISession session) : ITrainingListingCql
{
    public async Task<bool> AnyRowAsync(SimpleStatement statement) => (await session.ExecuteAsync(statement)).Any();
    public Task ExecuteAsync(SimpleStatement statement) => session.ExecuteAsync(statement);
}

/// <summary>Cassandra store. Only int/text/double/boolean/timestamp/list/map columns; platform and price kind are ints, never enum parameters.</summary>
public class CassandraTrainingListingStore : ITrainingListingStore
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(14);
    /// <summary>Rows fetched per driver page while a page is streamed.</summary>
    public const int FetchPageSize = 500;

    private const string Columns =
        "day, shard, platform, listing_id, url, title, description, description_short, category, categories, attributes, price, currency, "
        + "price_kind, condition, image_urls, country, region, locality, commercial, shipping, created_at, first_seen_at, scope, scope_reason";

    private readonly ISession? sessionOrNull;
    private ISession session => sessionOrNull ?? throw new InvalidOperationException("This store was built for writing only");
    private readonly ITrainingListingCql cql;
    private readonly bool createTables;
    private readonly TimeSpan ttl;
    private ILogger? logger;
    private readonly Func<DateTime> clock;
    private readonly TrainingRetryPolicy retry;
    private static bool tablesInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    /// <param name="createTables">Whether <see cref="InitializeAsync"/> creates the tables. AneApi creates them at start.</param>
    /// <param name="ttl">Lifetime of a row; default <see cref="DefaultTtl"/>.</param>
    /// <param name="logger">Receives retry and give-up lines (error type only, no listing content).</param>
    /// <param name="retry">Backoff for transient Cassandra errors, default <see cref="TrainingRetryPolicy.Default"/>.</param>
    public CassandraTrainingListingStore(ISession session, bool createTables = true, TimeSpan? ttl = null, ILogger? logger = null,
        TrainingRetryPolicy? retry = null, Func<DateTime>? clock = null)
        : this(new SessionCql(session), createTables, ttl, logger, retry, clock)
    {
        sessionOrNull = session;
    }

    /// <summary>Write-only store over the given statement runner (tests); reads and takedowns need the session constructor.</summary>
    public CassandraTrainingListingStore(ITrainingListingCql cql, bool createTables = false, TimeSpan? ttl = null, ILogger? logger = null,
        TrainingRetryPolicy? retry = null, Func<DateTime>? clock = null)
    {
        this.cql = cql;
        this.createTables = createTables;
        this.ttl = ttl ?? DefaultTtl;
        this.logger = logger;
        this.retry = retry ?? TrainingRetryPolicy.Default;
        this.clock = clock ?? (() => DateTime.UtcNow);
    }

    public const string CreateListingsCql =
        "CREATE TABLE IF NOT EXISTS training_listings (day text, shard int, platform int, listing_id text, url text, title text, description text, "
        + "description_short text, category text, categories list<text>, attributes map<text, text>, price double, currency text, price_kind int, "
        + "condition text, image_urls list<text>, country text, region text, locality text, commercial boolean, shipping text, created_at timestamp, "
        + "first_seen_at timestamp, scope text, scope_reason text, PRIMARY KEY ((day, shard), platform, listing_id))";

    /// <summary>Counter table: rows written per day and shard. A counter cannot expire, the few rows per day stay.</summary>
    public const string CreateDaysCql =
        "CREATE TABLE IF NOT EXISTS training_listing_days (day text, shard int, written counter, PRIMARY KEY (day, shard))";

    public async Task InitializeAsync()
    {
        if (tablesInitialized || !createTables) return;
        await InitLock.WaitAsync();
        try
        {
            if (tablesInitialized) return;
            await session.ExecuteAsync(new SimpleStatement(CreateListingsCql));
            await session.ExecuteAsync(new SimpleStatement(CreateDaysCql));
            tablesInitialized = true;
        }
        finally { InitLock.Release(); }
    }

    /// <summary>
    /// Plain insert of one row (no lightweight transaction, so no Paxos round). It is idempotent: a repeat writes the same values under the
    /// same key. The platform is bound as int.
    /// </summary>
    public static SimpleStatement BuildInsertStatement(TrainingListing listing, string day, int shard, TimeSpan ttl) =>
        new SimpleStatement(
            $"INSERT INTO training_listings ({Columns}) VALUES ({string.Join(", ", Enumerable.Repeat("?", 25))}) USING TTL {Math.Max(1, (int)ttl.TotalSeconds)}",
            day, shard, (int)listing.Platform, listing.ListingId, listing.Url, listing.Title, listing.Description, listing.DescriptionShort,
            listing.Category, listing.Categories, listing.Attributes, listing.Price, listing.Currency, (int)listing.PriceKind, listing.Condition,
            listing.ImageUrls, listing.Country, listing.Region, listing.Locality, listing.Commercial, listing.Shipping,
            listing.CreatedAt?.ToUniversalTime(), listing.FirstSeenAt?.ToUniversalTime(), listing.Scope, listing.ScopeReason);

    public static SimpleStatement BuildExistsStatement(string day, int shard, int platform, string listingId) =>
        new SimpleStatement("SELECT listing_id FROM training_listings WHERE day = ? AND shard = ? AND platform = ? AND listing_id = ?", day, shard, platform, listingId);

    public static SimpleStatement BuildCountStatement(string day, int shard) =>
        new SimpleStatement("UPDATE training_listing_days SET written = written + 1 WHERE day = ? AND shard = ?", day, shard);

    /// <summary>Keys of up to <paramref name="count"/> rows after the cursor. Auto paging stays on: the LIMIT bounds the total, the driver fetches it in pages of 5000.</summary>
    public static SimpleStatement BuildKeysStatement(string day, int shard, int count, int? cursorPlatform, string? cursorListingId)
    {
        var statement = cursorListingId == null
            ? new SimpleStatement("SELECT platform, listing_id FROM training_listings WHERE day = ? AND shard = ? LIMIT ?", day, shard, count)
            : new SimpleStatement(
                "SELECT platform, listing_id FROM training_listings WHERE day = ? AND shard = ? AND (platform, listing_id) > (?, ?) LIMIT ?",
                day, shard, cursorPlatform!.Value, cursorListingId, count);
        return (SimpleStatement)statement.SetPageSize(5000);
    }

    /// <summary>Full rows after the cursor, in pages of <see cref="FetchPageSize"/> that are fetched one by one (no auto paging).</summary>
    public static SimpleStatement BuildRowsStatement(string day, int shard, int? cursorPlatform, string? cursorListingId, byte[]? pagingState)
    {
        var statement = cursorListingId == null
            ? new SimpleStatement($"SELECT {Columns} FROM training_listings WHERE day = ? AND shard = ?", day, shard)
            : new SimpleStatement($"SELECT {Columns} FROM training_listings WHERE day = ? AND shard = ? AND (platform, listing_id) > (?, ?)",
                day, shard, cursorPlatform!.Value, cursorListingId);
        statement.SetPageSize(FetchPageSize).SetAutoPage(false);
        if (pagingState != null)
            statement.SetPagingState(pagingState);
        return statement;
    }

    /// <summary>
    /// Exists check, then a plain insert, then the counter. Every statement is retried on transient errors; the check makes a retry or a
    /// re-delivery after an unknown write outcome a no-op (and keeps the first write, a later message of the same key does not overwrite it).
    /// The counter is not idempotent: it is only repeated when the replicas were unavailable (nothing was written) and given up with a log
    /// line otherwise, so the day counts can be one too low, never too high because of a retry.
    /// </summary>
    public async Task<bool> AddAsync(TrainingListing listing)
    {
        await InitializeAsync();
        // the time is taken once: a retry or a later delivery with the same first-seen time lands on the same key
        var day = TrainingListingKeys.DayBucket(listing.FirstSeenAt ?? clock());
        var shard = TrainingListingKeys.ShardOf((int)listing.Platform, listing.ListingId);
        var candidates = listing.FirstSeenAt == null ? new[] { day, TrainingListingKeys.DayBucket(clock().AddDays(-1)) } : new[] { day };
        foreach (var candidate in candidates.Distinct())
            if (await Retried("exists", () => cql.AnyRowAsync(BuildExistsStatement(candidate, shard, (int)listing.Platform, listing.ListingId))))
                return false;
        await Retried("insert", async () =>
        {
            await cql.ExecuteAsync(BuildInsertStatement(listing, day, shard, ttl));
            return true;
        });
        try
        {
            await TrainingStoreRetry.ExecuteAsync(async () =>
            {
                await cql.ExecuteAsync(BuildCountStatement(day, shard));
                return true;
            }, "count", retry, logger, retryable: ex => ex is UnavailableException);
        }
        catch (Exception ex) when (TrainingStoreRetry.IsTransient(ex))
        {
            logger?.LogWarning("Training listing day counter not incremented ({Error}), the row was stored", ex.GetType().Name);
        }
        return true;
    }

    /// <summary>Sets the logger for retry lines when the store was built without one (the sink does this, the store comes from DI).</summary>
    public void AttachLogger(ILogger log) => logger ??= log;

    private Task<bool> Retried(string operation, Func<Task<bool>> action) => TrainingStoreRetry.ExecuteAsync(action, operation, retry, logger);

    public async Task<int> DeleteAsync(Platform platform, string listingId)
    {
        await InitializeAsync();
        var shard = TrainingListingKeys.ShardOf((int)platform, listingId);
        var removed = 0;
        foreach (var day in await ListDaysAsync())
        {
            var exists = await session.ExecuteAsync(new SimpleStatement(
                "SELECT listing_id FROM training_listings WHERE day = ? AND shard = ? AND platform = ? AND listing_id = ?", day.Day, shard, (int)platform, listingId));
            if (!exists.Any())
                continue;
            await session.ExecuteAsync(new SimpleStatement(
                "DELETE FROM training_listings WHERE day = ? AND shard = ? AND platform = ? AND listing_id = ?", day.Day, shard, (int)platform, listingId));
            await session.ExecuteAsync(new SimpleStatement("UPDATE training_listing_days SET written = written - 1 WHERE day = ? AND shard = ?", day.Day, shard));
            removed++;
        }
        return removed;
    }

    public async Task<IReadOnlyList<TrainingListingDay>> ListDaysAsync()
    {
        await InitializeAsync();
        var rows = await session.ExecuteAsync(new SimpleStatement("SELECT day, shard, written FROM training_listing_days"));
        return rows.GroupBy(r => r.GetValue<string>("day")).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var byShard = g.ToDictionary(r => r.GetValue<int>("shard"), r => Math.Max(0, r.GetValue<long>("written")));
                return new TrainingListingDay(g.Key, byShard.Values.Sum(), byShard);
            }).ToList();
    }

    public async Task<TrainingListingPage> ReadPartitionAsync(string day, int shard, int limit, string? cursor)
    {
        await InitializeAsync();
        limit = Math.Max(1, limit);
        int? cursorPlatform = null;
        string? cursorId = null;
        if (TrainingListingCursor.TryDecode(cursor, out var platform, out var id))
        {
            cursorPlatform = platform;
            cursorId = id;
        }
        // pass one reads the keys only (small), so the last key of the page, and with it the cursor, is known before the first row is written
        var keys = new List<(int Platform, string ListingId)>();
        var keyRows = await session.ExecuteAsync(BuildKeysStatement(day, shard, limit + 1, cursorPlatform, cursorId));
        foreach (var row in keyRows)
            keys.Add((row.GetValue<int>("platform"), row.GetValue<string>("listing_id")));
        var hasMore = keys.Count > limit;
        var last = keys.Count == 0 ? default : keys[Math.Min(limit, keys.Count) - 1];
        var next = hasMore ? TrainingListingCursor.Encode(last.Platform, last.ListingId) : null;
        return new TrainingListingPage(next, keys.Count == 0 ? Empty() : StreamAsync(day, shard, cursorPlatform, cursorId, last));
    }

    private static async IAsyncEnumerable<TrainingListing> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }

    // pass two: the rows in pages, up to the last key of pass one (rows written in between cannot move the cursor, later ones belong to the next page)
    private async IAsyncEnumerable<TrainingListing> StreamAsync(string day, int shard, int? cursorPlatform, string? cursorId, (int Platform, string ListingId) last)
    {
        byte[]? state = null;
        do
        {
            var rows = await session.ExecuteAsync(BuildRowsStatement(day, shard, cursorPlatform, cursorId, state));
            foreach (var row in rows)
            {
                var listing = FromRow(row);
                if (Compare((int)listing.Platform, listing.ListingId, last.Platform, last.ListingId) > 0)
                    yield break;
                yield return listing;
            }
            state = rows.PagingState;
        } while (state != null);
    }

    /// <summary>Order of the clustering key: platform as int, then the id as UTF-8 bytes (what Cassandra sorts text by).</summary>
    internal static int Compare(int platformA, string idA, int platformB, string idB)
    {
        var byPlatform = platformA.CompareTo(platformB);
        return byPlatform != 0 ? byPlatform : CompareUtf8(idA, idB);
    }

    internal static int CompareUtf8(string a, string b) =>
        Encoding.UTF8.GetBytes(a).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(b));

    private static TrainingListing FromRow(Row row) => new()
    {
        Platform = (Platform)row.GetValue<int>("platform"),
        ListingId = row.GetValue<string>("listing_id"),
        Url = row.GetValue<string?>("url"),
        Title = row.GetValue<string?>("title"),
        Description = row.GetValue<string?>("description"),
        DescriptionShort = row.GetValue<string?>("description_short"),
        Category = row.GetValue<string?>("category"),
        Categories = row.IsNull("categories") ? null : row.GetValue<IEnumerable<string>>("categories").ToArray(),
        Attributes = row.IsNull("attributes") ? null : new Dictionary<string, string>(row.GetValue<IDictionary<string, string>>("attributes")),
        Price = row.IsNull("price") ? null : row.GetValue<double>("price"),
        Currency = row.GetValue<string?>("currency"),
        PriceKind = (PriceKind)(row.IsNull("price_kind") ? 0 : row.GetValue<int>("price_kind")),
        Condition = row.GetValue<string?>("condition"),
        ImageUrls = row.IsNull("image_urls") ? null : row.GetValue<IEnumerable<string>>("image_urls").ToArray(),
        Country = row.GetValue<string?>("country"),
        Region = row.GetValue<string?>("region"),
        Locality = row.GetValue<string?>("locality"),
        Commercial = row.IsNull("commercial") ? null : row.GetValue<bool>("commercial"),
        Shipping = row.GetValue<string?>("shipping"),
        CreatedAt = Utc(row, "created_at"),
        FirstSeenAt = Utc(row, "first_seen_at"),
        Scope = row.GetValue<string?>("scope") ?? TrainingListingScope.Kept,
        ScopeReason = row.GetValue<string?>("scope_reason")
    };

    private static DateTime? Utc(Row row, string column) =>
        row.IsNull(column) ? null : DateTime.SpecifyKind(row.GetValue<DateTime>(column), DateTimeKind.Utc);
}

/// <summary>In-memory store for tests and non-Cassandra use, same semantics as the Cassandra one (expiry is applied when reading).</summary>
public class InMemoryTrainingListingStore : ITrainingListingStore
{
    private readonly object gate = new();
    private readonly Func<DateTime> clock;
    private readonly TimeSpan ttl;
    private readonly Dictionary<(string Day, int Shard, int Platform, string Id), (TrainingListing Row, DateTime ExpiresAt)> rows = new();
    private readonly Dictionary<(string Day, int Shard), long> written = new();

    public InMemoryTrainingListingStore(Func<DateTime>? clock = null, TimeSpan? ttl = null)
    {
        this.clock = clock ?? (() => DateTime.UtcNow);
        this.ttl = ttl ?? CassandraTrainingListingStore.DefaultTtl;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private static TrainingListing Copy(TrainingListing l) => MessagePack.MessagePackSerializer.Deserialize<TrainingListing>(MessagePack.MessagePackSerializer.Serialize(l));

    public Task<bool> AddAsync(TrainingListing listing)
    {
        var day = TrainingListingKeys.DayBucket(listing.FirstSeenAt ?? clock());
        var shard = TrainingListingKeys.ShardOf((int)listing.Platform, listing.ListingId);
        var key = (day, shard, (int)listing.Platform, listing.ListingId);
        lock (gate)
        {
            var days = listing.FirstSeenAt == null ? new[] { day, TrainingListingKeys.DayBucket(clock().AddDays(-1)) } : new[] { day };
            if (days.Any(d => rows.TryGetValue((d, shard, (int)listing.Platform, listing.ListingId), out var existing) && existing.ExpiresAt > clock()))
                return Task.FromResult(false);
            rows[key] = (Copy(listing), clock() + ttl);
            written[(day, shard)] = written.GetValueOrDefault((day, shard)) + 1;
            return Task.FromResult(true);
        }
    }

    public Task<int> DeleteAsync(Platform platform, string listingId)
    {
        lock (gate)
        {
            var keys = rows.Keys.Where(k => k.Platform == (int)platform && k.Id == listingId).ToList();
            foreach (var key in keys)
            {
                rows.Remove(key);
                written[(key.Day, key.Shard)] = written.GetValueOrDefault((key.Day, key.Shard)) - 1;
            }
            return Task.FromResult(keys.Count);
        }
    }

    public Task<IReadOnlyList<TrainingListingDay>> ListDaysAsync()
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<TrainingListingDay>>(written.GroupBy(w => w.Key.Day).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var byShard = g.ToDictionary(w => w.Key.Shard, w => Math.Max(0, w.Value));
                    return new TrainingListingDay(g.Key, byShard.Values.Sum(), byShard);
                }).ToList());
    }

    public Task<TrainingListingPage> ReadPartitionAsync(string day, int shard, int limit, string? cursor)
    {
        limit = Math.Max(1, limit);
        var hasCursor = TrainingListingCursor.TryDecode(cursor, out var cursorPlatform, out var cursorId);
        List<TrainingListing> page;
        bool hasMore;
        lock (gate)
        {
            var ordered = rows.Where(kv => kv.Key.Day == day && kv.Key.Shard == shard && kv.Value.ExpiresAt > clock())
                .OrderBy(kv => kv.Key.Platform).ThenBy(kv => kv.Key.Id, Comparer<string>.Create(CassandraTrainingListingStore.CompareUtf8))
                .Where(kv => !hasCursor || CassandraTrainingListingStore.Compare(kv.Key.Platform, kv.Key.Id, cursorPlatform, cursorId) > 0)
                .Take(limit + 1).Select(kv => Copy(kv.Value.Row)).ToList();
            hasMore = ordered.Count > limit;
            page = ordered.Take(limit).ToList();
        }
        var next = hasMore ? TrainingListingCursor.Encode((int)page[^1].Platform, page[^1].ListingId) : null;
        return Task.FromResult(new TrainingListingPage(next, ToAsync(page)));
    }

    private static async IAsyncEnumerable<TrainingListing> ToAsync(IEnumerable<TrainingListing> items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }
}
