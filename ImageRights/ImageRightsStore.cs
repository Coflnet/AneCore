using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.ImageRights;

/// <summary>One row of <c>image_rights_status</c>: the last verdict for one image host.</summary>
public class ImageRightsRecord
{
    /// <summary>Partition key: image host, lower case.</summary>
    public string Host { get; set; } = "";
    /// <summary>Platform name (text, never an enum parameter).</summary>
    public string Platform { get; set; } = "";
    /// <summary>Name of <see cref="ImageRightsStatus"/> (text).</summary>
    public string Status { get; set; } = nameof(ImageRightsStatus.Unknown);
    public List<string> Evidence { get; set; } = [];
    public DateTime CheckedAt { get; set; }
    /// <summary>SHA-256 (hex) of the robots.txt texts read, to see when the files changed.</summary>
    public string RobotsHash { get; set; } = "";

    /// <summary>Not a column: <see cref="Status"/> parsed, Unknown for anything unrecognised.</summary>
    public ImageRightsStatus StatusValue =>
        Enum.TryParse<ImageRightsStatus>(Status, out var s) ? s : ImageRightsStatus.Unknown;
}

public interface IImageRightsStore
{
    Task InitializeAsync();
    Task UpsertAsync(ImageRightsRecord record);
    Task<ImageRightsRecord?> GetAsync(string host);
    Task<IReadOnlyList<ImageRightsRecord>> GetAllAsync();
}

/// <summary>Cassandra-backed store, all columns plain text/timestamp/list of text.</summary>
public class CassandraImageRightsStore : IImageRightsStore
{
    private readonly ISession session;
    private readonly Table<ImageRightsRecord> table;
    private static bool tableInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private readonly bool createTable;

    /// <param name="createTable">
    /// Whether <see cref="InitializeAsync"/> creates the table. AneApi (writer of the rows) creates it, read-only
    /// consumers such as AneNotifier pass false: a missing table then makes reads throw and counts as "unavailable".
    /// </param>
    public CassandraImageRightsStore(ISession session, bool createTable = true)
    {
        this.session = session;
        this.createTable = createTable;
        table = new Table<ImageRightsRecord>(session, BuildMapping());
    }

    public static MappingConfiguration BuildMapping() =>
        new MappingConfiguration().Define(new Map<ImageRightsRecord>()
            .TableName("image_rights_status")
            .PartitionKey(r => r.Host)
            .Column(r => r.Host, cm => cm.WithName("host"))
            .Column(r => r.Platform, cm => cm.WithName("platform"))
            .Column(r => r.Status, cm => cm.WithName("status"))
            .Column(r => r.Evidence, cm => cm.WithName("evidence"))
            .Column(r => r.CheckedAt, cm => cm.WithName("checked_at"))
            .Column(r => r.RobotsHash, cm => cm.WithName("robots_hash"))
            .Column(r => r.StatusValue, cm => cm.Ignore()));

    public async Task InitializeAsync()
    {
        if (tableInitialized || !createTable) return;
        await InitLock.WaitAsync();
        try
        {
            if (tableInitialized) return;
            await table.CreateIfNotExistsAsync();
            tableInitialized = true;
        }
        finally { InitLock.Release(); }
    }

    public async Task UpsertAsync(ImageRightsRecord record)
    {
        await InitializeAsync();
        record.Host = record.Host.ToLowerInvariant();
        await table.Insert(record).ExecuteAsync();
    }

    public async Task<ImageRightsRecord?> GetAsync(string host)
    {
        await InitializeAsync();
        var lower = host.ToLowerInvariant();
        return (await table.Where(r => r.Host == lower).ExecuteAsync()).FirstOrDefault();
    }

    public async Task<IReadOnlyList<ImageRightsRecord>> GetAllAsync()
    {
        await InitializeAsync();
        return (await table.ExecuteAsync()).ToList();
    }
}

public class InMemoryImageRightsStore : IImageRightsStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageRightsRecord> rows = new();
    public Task InitializeAsync() => Task.CompletedTask;
    public Task UpsertAsync(ImageRightsRecord record)
    {
        rows[record.Host.ToLowerInvariant()] = record;
        return Task.CompletedTask;
    }
    public Task<ImageRightsRecord?> GetAsync(string host) =>
        Task.FromResult(rows.TryGetValue(host.ToLowerInvariant(), out var r) ? r : null);
    public Task<IReadOnlyList<ImageRightsRecord>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<ImageRightsRecord>>(rows.Values.ToList());
}
