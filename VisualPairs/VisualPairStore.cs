using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.VisualPairs;

public interface IVisualPairStore
{
    Task InitializeAsync();

    /// <summary>Stores a candidate. Idempotent on <see cref="VisualPairCandidate.PairId"/>: returns false (and changes nothing) when it exists already.</summary>
    Task<bool> AddCandidateAsync(VisualPairCandidate candidate);

    Task<VisualPairCandidate?> GetCandidateAsync(string pairId);

    /// <summary>Up to <paramref name="limit"/> candidates of one UTC day bucket, newest first.</summary>
    Task<IReadOnlyList<VisualPairCandidate>> GetCandidatesAsync(DateOnly day, int limit);

    Task<IReadOnlyList<VisualPairLabel>> GetLabelsForPairAsync(string pairId);

    /// <summary>Labels of several pairs at once, keyed by pair id (pairs without labels are absent).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<VisualPairLabel>>> GetLabelsForPairsAsync(IEnumerable<string> pairIds);

    /// <summary>Stores the label; a second call for the same user and pair overwrites the first.</summary>
    Task AddLabelAsync(VisualPairLabel label);

    /// <summary>One page of all labels for the export. Labels of one pair are adjacent. Pass the returned state to continue; null state means the end.</summary>
    Task<(IReadOnlyList<VisualPairLabel> Labels, byte[]? NextPagingState)> GetLabelsAsync(int pageSize, byte[]? pagingState);

    /// <summary>Number of labels per label value.</summary>
    Task<IReadOnlyDictionary<string, long>> CountLabelsAsync();
}

/// <summary>Cassandra-backed store. Tables use only int/text/double/timestamp columns; platforms are ints.</summary>
public class CassandraVisualPairStore : IVisualPairStore
{
    /// <summary>90 days.</summary>
    public const int CandidateTimeToLiveSeconds = 90 * 24 * 60 * 60;

    private const int GetPageSize = 500;

    private readonly ISession session;
    private readonly Table<VisualPairCandidate> candidates;
    private readonly Table<VisualPairCandidateLocation> locations;
    private readonly Table<VisualPairLabel> labels;
    private static bool tablesInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public CassandraVisualPairStore(ISession session)
    {
        this.session = session;
        var mapping = BuildMapping();
        candidates = new Table<VisualPairCandidate>(session, mapping);
        locations = new Table<VisualPairCandidateLocation>(session, mapping);
        labels = new Table<VisualPairLabel>(session, mapping);
    }

    public static MappingConfiguration BuildMapping() =>
        new MappingConfiguration()
            .Define(new Map<VisualPairCandidate>()
                .TableName("visual_pair_candidates")
                .PartitionKey(c => c.Day, c => c.Shard)
                .ClusteringKey(c => c.PairId)
                .Column(c => c.Day, cm => cm.WithName("day"))
                .Column(c => c.Shard, cm => cm.WithName("shard"))
                .Column(c => c.PairId, cm => cm.WithName("pair_id"))
                .Column(c => c.APlatformValue, cm => cm.WithName("a_platform"))
                .Column(c => c.AListingId, cm => cm.WithName("a_listing_id"))
                .Column(c => c.AImageIndex, cm => cm.WithName("a_image_index"))
                .Column(c => c.AImageUrl, cm => cm.WithName("a_image_url"))
                .Column(c => c.BPlatformValue, cm => cm.WithName("b_platform"))
                .Column(c => c.BListingId, cm => cm.WithName("b_listing_id"))
                .Column(c => c.BImageIndex, cm => cm.WithName("b_image_index"))
                .Column(c => c.BImageUrl, cm => cm.WithName("b_image_url"))
                .Column(c => c.GroupKey, cm => cm.WithName("group_key"))
                .Column(c => c.Similarity, cm => cm.WithName("similarity"))
                .Column(c => c.SecondBest, cm => cm.WithName("second_best"))
                .Column(c => c.Decision, cm => cm.WithName("decision"))
                .Column(c => c.ClusterSeoId, cm => cm.WithName("cluster_seo_id"))
                .Column(c => c.TextSeoId, cm => cm.WithName("text_seo_id"))
                .Column(c => c.ModelId, cm => cm.WithName("model_id"))
                .Column(c => c.Source, cm => cm.WithName("source"))
                .Column(c => c.CreatedAt, cm => cm.WithName("created_at"))
                .Column(c => c.A, cm => cm.Ignore())
                .Column(c => c.B, cm => cm.Ignore()))
            .Define(new Map<VisualPairCandidateLocation>()
                .TableName("visual_pair_candidate_days")
                .PartitionKey(l => l.PairId)
                .Column(l => l.PairId, cm => cm.WithName("pair_id"))
                .Column(l => l.Day, cm => cm.WithName("day")))
            .Define(new Map<VisualPairLabel>()
                .TableName("visual_pair_labels")
                .PartitionKey(l => l.PairId)
                .ClusteringKey(l => l.UserId)
                .Column(l => l.PairId, cm => cm.WithName("pair_id"))
                .Column(l => l.UserId, cm => cm.WithName("user_id"))
                .Column(l => l.Label, cm => cm.WithName("label"))
                .Column(l => l.ModelId, cm => cm.WithName("model_id"))
                .Column(l => l.Similarity, cm => cm.WithName("similarity"))
                .Column(l => l.GroupKey, cm => cm.WithName("group_key"))
                .Column(l => l.CreatedAt, cm => cm.WithName("created_at"))
                .Column(l => l.APlatformValue, cm => cm.WithName("a_platform"))
                .Column(l => l.AListingId, cm => cm.WithName("a_listing_id"))
                .Column(l => l.AImageIndex, cm => cm.WithName("a_image_index"))
                .Column(l => l.AImageUrl, cm => cm.WithName("a_image_url"))
                .Column(l => l.BPlatformValue, cm => cm.WithName("b_platform"))
                .Column(l => l.BListingId, cm => cm.WithName("b_listing_id"))
                .Column(l => l.BImageIndex, cm => cm.WithName("b_image_index"))
                .Column(l => l.BImageUrl, cm => cm.WithName("b_image_url"))
                .Column(l => l.A, cm => cm.Ignore())
                .Column(l => l.B, cm => cm.Ignore()));

    public async Task InitializeAsync()
    {
        if (tablesInitialized) return;
        await InitLock.WaitAsync();
        try
        {
            if (tablesInitialized) return;
            await candidates.CreateIfNotExistsAsync();
            await locations.CreateIfNotExistsAsync();
            await labels.CreateIfNotExistsAsync();
            foreach (var table in new[] { "visual_pair_candidates", "visual_pair_candidate_days" })
                await session.ExecuteAsync(new global::Cassandra.SimpleStatement(
                    $"ALTER TABLE {table} WITH default_time_to_live = {CandidateTimeToLiveSeconds}"));
            tablesInitialized = true;
        }
        finally { InitLock.Release(); }
    }

    public async Task<bool> AddCandidateAsync(VisualPairCandidate candidate)
    {
        await InitializeAsync();
        var pairId = candidate.PairId;
        var existing = await locations.Where(l => l.PairId == pairId).ExecuteAsync();
        if (existing.Any())
            return false;
        if (candidate.CreatedAt == default)
            candidate.CreatedAt = DateTime.UtcNow;
        candidate.Day = VisualPairId.DayBucket(candidate.CreatedAt);
        candidate.Shard = VisualPairId.ShardOf(pairId);
        await candidates.Insert(candidate).SetTTL(CandidateTimeToLiveSeconds).ExecuteAsync();
        await locations.Insert(new VisualPairCandidateLocation { PairId = pairId, Day = candidate.Day })
            .SetTTL(CandidateTimeToLiveSeconds).ExecuteAsync();
        return true;
    }

    public async Task<VisualPairCandidate?> GetCandidateAsync(string pairId)
    {
        await InitializeAsync();
        var location = (await locations.Where(l => l.PairId == pairId).ExecuteAsync()).FirstOrDefault();
        if (location == null)
            return null;
        var shard = VisualPairId.ShardOf(pairId);
        var day = location.Day;
        return (await candidates.Where(c => c.Day == day && c.Shard == shard && c.PairId == pairId).ExecuteAsync()).FirstOrDefault();
    }

    public async Task<IReadOnlyList<VisualPairCandidate>> GetCandidatesAsync(DateOnly day, int limit)
    {
        await InitializeAsync();
        var dayKey = VisualPairId.DayBucket(day);
        var perShard = await Task.WhenAll(Enumerable.Range(0, VisualPairId.ShardCount).Select(async shard =>
            (await candidates.Where(c => c.Day == dayKey && c.Shard == shard).Take(limit).ExecuteAsync()).ToList()));
        return perShard.SelectMany(x => x).OrderByDescending(c => c.CreatedAt).Take(limit).ToList();
    }

    public async Task<IReadOnlyList<VisualPairLabel>> GetLabelsForPairAsync(string pairId)
    {
        await InitializeAsync();
        return (await labels.Where(l => l.PairId == pairId).ExecuteAsync()).ToList();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<VisualPairLabel>>> GetLabelsForPairsAsync(IEnumerable<string> pairIds)
    {
        var gate = new SemaphoreSlim(16);
        var results = await Task.WhenAll(pairIds.Distinct().Select(async id =>
        {
            await gate.WaitAsync();
            try { return (id, labels: await GetLabelsForPairAsync(id)); }
            finally { gate.Release(); }
        }));
        return results.Where(r => r.labels.Count > 0).ToDictionary(r => r.id, r => r.labels);
    }

    public async Task AddLabelAsync(VisualPairLabel label)
    {
        await InitializeAsync();
        if (label.CreatedAt == default)
            label.CreatedAt = DateTime.UtcNow;
        await labels.Insert(label).ExecuteAsync();
    }

    public async Task<(IReadOnlyList<VisualPairLabel> Labels, byte[]? NextPagingState)> GetLabelsAsync(int pageSize, byte[]? pagingState)
    {
        await InitializeAsync();
        var query = labels.SetPageSize(pageSize);
        if (pagingState != null)
            query = query.SetPagingState(pagingState);
        var page = await query.ExecutePagedAsync();
        return (page.ToList(), page.PagingState);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountLabelsAsync()
    {
        var counts = new Dictionary<string, long>();
        byte[]? state = null;
        do
        {
            var (page, next) = await GetLabelsAsync(GetPageSize * 4, state);
            foreach (var label in page)
                counts[label.Label] = counts.GetValueOrDefault(label.Label) + 1;
            state = next;
        } while (state != null);
        return counts;
    }
}

/// <summary>In-memory store for tests and non-Cassandra use; same semantics as the Cassandra one (no TTL).</summary>
public class InMemoryVisualPairStore : IVisualPairStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, VisualPairCandidate> candidates = new();
    private readonly Dictionary<(string PairId, string UserId), VisualPairLabel> labels = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<bool> AddCandidateAsync(VisualPairCandidate candidate)
    {
        lock (gate)
        {
            if (candidates.ContainsKey(candidate.PairId))
                return Task.FromResult(false);
            if (candidate.CreatedAt == default)
                candidate.CreatedAt = DateTime.UtcNow;
            candidate.Day = VisualPairId.DayBucket(candidate.CreatedAt);
            candidate.Shard = VisualPairId.ShardOf(candidate.PairId);
            candidates[candidate.PairId] = candidate;
            return Task.FromResult(true);
        }
    }

    public Task<VisualPairCandidate?> GetCandidateAsync(string pairId)
    {
        lock (gate) return Task.FromResult(candidates.GetValueOrDefault(pairId));
    }

    public Task<IReadOnlyList<VisualPairCandidate>> GetCandidatesAsync(DateOnly day, int limit)
    {
        var key = VisualPairId.DayBucket(day);
        lock (gate)
            return Task.FromResult<IReadOnlyList<VisualPairCandidate>>(
                candidates.Values.Where(c => c.Day == key).OrderByDescending(c => c.CreatedAt).Take(limit).ToList());
    }

    public Task<IReadOnlyList<VisualPairLabel>> GetLabelsForPairAsync(string pairId)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<VisualPairLabel>>(labels.Values.Where(l => l.PairId == pairId).ToList());
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<VisualPairLabel>>> GetLabelsForPairsAsync(IEnumerable<string> pairIds)
    {
        var result = new Dictionary<string, IReadOnlyList<VisualPairLabel>>();
        foreach (var id in pairIds.Distinct())
        {
            var found = await GetLabelsForPairAsync(id);
            if (found.Count > 0)
                result[id] = found;
        }
        return result;
    }

    public Task AddLabelAsync(VisualPairLabel label)
    {
        lock (gate)
        {
            if (label.CreatedAt == default)
                label.CreatedAt = DateTime.UtcNow;
            labels[(label.PairId, label.UserId)] = label;
        }
        return Task.CompletedTask;
    }

    /// <summary>The in-memory paging state is the 4-byte offset into the labels ordered by pair id.</summary>
    public Task<(IReadOnlyList<VisualPairLabel> Labels, byte[]? NextPagingState)> GetLabelsAsync(int pageSize, byte[]? pagingState)
    {
        lock (gate)
        {
            var all = labels.Values.OrderBy(l => l.PairId, StringComparer.Ordinal).ThenBy(l => l.UserId, StringComparer.Ordinal).ToList();
            var offset = pagingState == null ? 0 : BitConverter.ToInt32(pagingState);
            var page = all.Skip(offset).Take(pageSize).ToList();
            var next = offset + page.Count < all.Count ? BitConverter.GetBytes(offset + page.Count) : null;
            return Task.FromResult<(IReadOnlyList<VisualPairLabel>, byte[]?)>((page, next));
        }
    }

    public Task<IReadOnlyDictionary<string, long>> CountLabelsAsync()
    {
        lock (gate)
            return Task.FromResult<IReadOnlyDictionary<string, long>>(
                labels.Values.GroupBy(l => l.Label).ToDictionary(g => g.Key, g => (long)g.Count()));
    }
}
