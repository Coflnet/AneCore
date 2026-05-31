using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Crowd;

/// <summary>
/// Cassandra-backed queue of <see cref="CrowdScrapeTask"/>s. Any scraper service can
/// enqueue work (e.g. "this marketplace search page should be refreshed") and the API
/// hands batches out to the browser extension. Tasks expire automatically via TTL so a
/// crashed/offline contributor never blocks a task forever.
/// </summary>
public class CrowdScrapeTaskStore
{
    private const int BucketCount = 8;
    private static readonly int DefaultTtlSeconds = (int)TimeSpan.FromHours(12).TotalSeconds;

    private readonly Table<CrowdTaskRow> table;
    private readonly Random random = new();

    public CrowdScrapeTaskStore(ISession session)
    {
        var mapping = new MappingConfiguration().Define(new Map<CrowdTaskRow>()
            .TableName("crowd_scrape_tasks")
            .PartitionKey(t => t.Bucket)
            .ClusteringKey(t => t.Created, SortOrder.Descending)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Bucket, cm => cm.WithName("bucket"))
            .Column(t => t.Created, cm => cm.WithName("created"))
            .Column(t => t.Id, cm => cm.WithName("id"))
            .Column(t => t.Url, cm => cm.WithName("url"))
            .Column(t => t.Xpath, cm => cm.WithName("xpath"))
            .Column(t => t.Platform, cm => cm.WithName("platform").WithDbType<int>())
            .Column(t => t.Reward, cm => cm.WithName("reward"))
            .Column(t => t.Kind, cm => cm.WithName("kind")));
        table = new Table<CrowdTaskRow>(session, mapping);
        table.CreateIfNotExists();
    }

    /// <summary>Enqueue a task for the crowd to pick up. Expires after <paramref name="ttlSeconds"/>.</summary>
    public async Task EnqueueAsync(CrowdScrapeTask task, int? ttlSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(task.Url))
            return;
        if (string.IsNullOrWhiteSpace(task.Id))
            task.Id = Guid.NewGuid().ToString("N");
        var row = new CrowdTaskRow
        {
            Bucket = random.Next(BucketCount),
            Created = DateTimeOffset.UtcNow,
            Id = task.Id,
            Url = task.Url,
            Xpath = task.Xpath,
            Platform = (int)task.Platform,
            Reward = task.Reward,
            Kind = task.Kind
        };
        await table.Insert(row).SetTTL(ttlSeconds ?? DefaultTtlSeconds).ExecuteAsync();
    }

    /// <summary>
    /// Read up to <paramref name="count"/> recent tasks, sampling random buckets so two
    /// contributors asking at the same time tend to get different work.
    /// </summary>
    public async Task<List<CrowdScrapeTask>> GetRecentAsync(int count)
    {
        var result = new List<CrowdScrapeTask>();
        var buckets = Enumerable.Range(0, BucketCount).OrderBy(_ => random.Next()).ToList();
        foreach (var bucket in buckets)
        {
            if (result.Count >= count)
                break;
            var rows = await table.Where(t => t.Bucket == bucket)
                .Take(count - result.Count)
                .ExecuteAsync();
            foreach (var row in rows)
            {
                result.Add(new CrowdScrapeTask
                {
                    Id = row.Id,
                    Url = row.Url,
                    Xpath = row.Xpath,
                    Platform = (Platform)row.Platform,
                    Reward = row.Reward,
                    Kind = row.Kind
                });
            }
        }
        return result.Take(count).ToList();
    }

    public class CrowdTaskRow
    {
        public int Bucket { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Xpath { get; set; } = string.Empty;
        public int Platform { get; set; }
        public int Reward { get; set; }
        public string? Kind { get; set; }
    }
}
