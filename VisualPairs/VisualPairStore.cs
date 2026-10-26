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

    /// <summary>
    /// Raises the <see cref="VisualPairCandidate.Source"/> of an existing candidate (used to upgrade a model candidate when a user reports the same pair).
    /// Returns false when the candidate does not exist or already has this source. Default: not supported.
    /// </summary>
    Task<bool> UpgradeCandidateSourceAsync(string pairId, string source) => Task.FromResult(false);

    /// <summary>Up to <paramref name="limit"/> candidates of one UTC day bucket, newest first.</summary>
    Task<IReadOnlyList<VisualPairCandidate>> GetCandidatesAsync(DateOnly day, int limit);

    Task<IReadOnlyList<VisualPairLabel>> GetLabelsForPairAsync(string pairId);

    /// <summary>Labels of several pairs at once, keyed by pair id (pairs without labels are absent).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<VisualPairLabel>>> GetLabelsForPairsAsync(IEnumerable<string> pairIds);

    /// <summary>Stores the label; a second call overwrites its answer and snapshot while preserving prior diagnostic/unknown review exposure atomically.</summary>
    Task AddLabelAsync(VisualPairLabel label);

    /// <summary>Records diagnostic exposure without creating a vote; failures must prevent disclosure.</summary>
    Task MarkDiagnosticExposureAsync(VisualPairCandidate candidate, string userId) =>
        throw new NotSupportedException("Durable diagnostic exposure is unavailable.");

    /// <summary>One page of all labels for the export. Labels of one pair are adjacent. Pass the returned state to continue; null state means the end.</summary>
    Task<(IReadOnlyList<VisualPairLabel> Labels, byte[]? NextPagingState)> GetLabelsAsync(int pageSize, byte[]? pagingState);

    /// <summary>Number of labels per label value.</summary>
    Task<IReadOnlyDictionary<string, long>> CountLabelsAsync();

    /// <summary>
    /// Takedown of an offer: removes the candidate pairs and labels that reference it on either side. Uses
    /// <c>visual_pairs_by_listing</c> and, for older rows that are not in it, a scan of the candidate day partitions of the last
    /// 90 days. Idempotent; returns the numbers removed.
    /// </summary>
    Task<(int Candidates, int Labels)> DeleteForListingAsync(Platform platform, string listingId);
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
    private readonly Table<VisualPairByListing> byListing;
    private readonly Table<VisualPairDiagnosticExposure> exposures;
    private readonly IMapper mapper;
    private static bool tablesInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public CassandraVisualPairStore(ISession session)
    {
        this.session = session;
        var mapping = BuildMapping();
        candidates = new Table<VisualPairCandidate>(session, mapping);
        locations = new Table<VisualPairCandidateLocation>(session, mapping);
        labels = new Table<VisualPairLabel>(session, mapping);
        byListing = new Table<VisualPairByListing>(session, mapping);
        exposures = new Table<VisualPairDiagnosticExposure>(session, mapping);
        mapper = new Mapper(session, mapping);
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
                .Column(c => c.CaptureMetadataJson, cm => cm.WithName("capture_metadata_json"))
                .Column(c => c.Source, cm => cm.WithName("source"))
                .Column(c => c.CreatedAt, cm => cm.WithName("created_at"))
                .Column(c => c.A, cm => cm.Ignore())
                .Column(c => c.B, cm => cm.Ignore()))
            .Define(new Map<VisualPairCandidateLocation>()
                .TableName("visual_pair_candidate_days")
                .PartitionKey(l => l.PairId)
                .Column(l => l.PairId, cm => cm.WithName("pair_id"))
                .Column(l => l.Day, cm => cm.WithName("day")))
            .Define(new Map<VisualPairByListing>()
                .TableName("visual_pairs_by_listing")
                .PartitionKey(l => l.PlatformValue, l => l.ListingId)
                .ClusteringKey(l => l.PairId)
                .Column(l => l.PlatformValue, cm => cm.WithName("platform"))
                .Column(l => l.ListingId, cm => cm.WithName("listing_id"))
                .Column(l => l.PairId, cm => cm.WithName("pair_id")))
            .Define(new Map<VisualPairDiagnosticExposure>()
                .TableName("visual_pair_diagnostic_exposures")
                .PartitionKey(e => e.PairId)
                .ClusteringKey(e => e.UserId)
                .Column(e => e.PairId, cm => cm.WithName("pair_id"))
                .Column(e => e.UserId, cm => cm.WithName("user_id"))
                .Column(e => e.APlatformValue, cm => cm.WithName("a_platform"))
                .Column(e => e.AListingId, cm => cm.WithName("a_listing_id"))
                .Column(e => e.BPlatformValue, cm => cm.WithName("b_platform"))
                .Column(e => e.BListingId, cm => cm.WithName("b_listing_id")))
            .Define(new Map<VisualPairLabel>()
                .TableName("visual_pair_labels")
                .PartitionKey(l => l.PairId)
                .ClusteringKey(l => l.UserId)
                .Column(l => l.PairId, cm => cm.WithName("pair_id"))
                .Column(l => l.UserId, cm => cm.WithName("user_id"))
                .Column(l => l.ReviewMode, cm => cm.WithName("review_mode"))
                .Column(l => l.Label, cm => cm.WithName("label"))
                .Column(l => l.ModelId, cm => cm.WithName("model_id"))
                .Column(l => l.CaptureMetadataJson, cm => cm.WithName("capture_metadata_json"))
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
            await byListing.CreateIfNotExistsAsync();
            await exposures.CreateIfNotExistsAsync();
            foreach (var table in new[] { "visual_pair_candidates", "visual_pair_candidate_days" })
                await session.ExecuteAsync(new global::Cassandra.SimpleStatement(
                    $"ALTER TABLE {table} WITH default_time_to_live = {CandidateTimeToLiveSeconds}"));
            // CreateIfNotExists does not add columns to tables deployed before capture provenance.
            foreach (var (table, column) in new[] {
                ("visual_pair_candidates", "capture_metadata_json"),
                ("visual_pair_labels", "capture_metadata_json"),
                ("visual_pair_labels", "review_mode") })
            {
                var lookup = new global::Cassandra.SimpleStatement(
                    "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ? AND column_name = ?",
                    session.Keyspace, table, column);
                if (!(await session.ExecuteAsync(lookup)).Any())
                {
                    try
                    {
                        await session.ExecuteAsync(new global::Cassandra.SimpleStatement($"ALTER TABLE {table} ADD {column} text"));
                    }
                    catch (global::Cassandra.InvalidQueryException)
                    {
                        // API and Notifier can race to add the same column; prove it exists before accepting the failure.
                        if (!(await session.ExecuteAsync(lookup)).Any())
                            throw;
                    }
                }
            }
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
        await IndexAsync(candidate.A, candidate.B, pairId, CandidateTimeToLiveSeconds);
        return true;
    }

    /// <summary>Lookup rows of both sides; labels have no TTL so their lookup rows have none either.</summary>
    private async Task IndexAsync(VisualPairImage a, VisualPairImage b, string pairId, int? ttlSeconds)
    {
        foreach (var side in new[] { a, b }.DistinctBy(i => (i.Platform, i.ListingId)))
        {
            var cql = "INSERT INTO visual_pairs_by_listing (platform, listing_id, pair_id) VALUES (?, ?, ?)";
            await mapper.ExecuteAsync(ttlSeconds is { } ttl
                ? Cql.New(cql + " USING TTL ?", (int)side.Platform, side.ListingId, pairId, ttl)
                : Cql.New(cql, (int)side.Platform, side.ListingId, pairId));
        }
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

    public async Task<bool> UpgradeCandidateSourceAsync(string pairId, string source)
    {
        var existing = await GetCandidateAsync(pairId);
        if (existing == null || existing.Source == source)
            return false;
        await mapper.ExecuteAsync("UPDATE visual_pair_candidates USING TTL ? SET source = ? WHERE day = ? AND shard = ? AND pair_id = ?",
            CandidateTimeToLiveSeconds, source, existing.Day, existing.Shard, pairId);
        return true;
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
        return (await mapper.FetchAsync<VisualPairLabel>("SELECT * FROM visual_pair_labels WHERE pair_id = ?", pairId))
            .Select(VisualPairLabel.NormalizePendingReviewMode).ToList();
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
        var requestedMode = label.ReviewMode;
        // A first blind vote stays unknown until its exposure check succeeds, including lost acknowledgements.
        if (requestedMode == "blind") label.ReviewMode = VisualPairLabel.PendingReviewMode;
        var result = await mapper.InsertIfNotExistsAsync(label, insertNulls: true);
        var firstBlind = result.Applied && requestedMode == "blind";
        for (var attempt = 0; !result.Applied && attempt < 4; attempt++)
        {
            var observedMode = result.Existing.ReviewMode;
            label.ReviewMode = VisualPairLabel.PreserveReviewMode(observedMode, requestedMode);
            // Compare the exposure we merged, including null legacy/unknown modes, across API replicas.
            result = await mapper.UpdateIfAsync<VisualPairLabel>(
                "SET label = ?, review_mode = ?, model_id = ?, capture_metadata_json = ?, similarity = ?, group_key = ?, created_at = ?, " +
                "a_platform = ?, a_listing_id = ?, a_image_index = ?, a_image_url = ?, b_platform = ?, b_listing_id = ?, b_image_index = ?, b_image_url = ? " +
                "WHERE pair_id = ? AND user_id = ? IF review_mode = ?",
                label.Label, label.ReviewMode, label.ModelId, label.CaptureMetadataJson, label.Similarity, label.GroupKey, label.CreatedAt,
                label.APlatformValue, label.AListingId, label.AImageIndex, label.AImageUrl,
                label.BPlatformValue, label.BListingId, label.BImageIndex, label.BImageUrl,
                label.PairId, label.UserId, observedMode);
        }
        if (!result.Applied)
            throw new InvalidOperationException("Label changed concurrently; retry the submission.");
        await IndexAsync(label.A, label.B, label.PairId, null);
        // The marker check follows the vote write. MarkDiagnosticExposureAsync also upgrades existing
        // votes, covering either ordering of an exposure and an in-flight blind submission.
        var exposure = await mapper.FetchAsync<VisualPairDiagnosticExposure>(Cql.New(
            "SELECT * FROM visual_pair_diagnostic_exposures WHERE pair_id = ? AND user_id = ?", label.PairId, label.UserId)
            .WithOptions(o => o.SetConsistencyLevel(global::Cassandra.ConsistencyLevel.Quorum)));
        if (exposure.Any())
            await UpgradeLabelExposureAsync(label.PairId, label.UserId);
        else if (firstBlind)
            await mapper.UpdateIfAsync<VisualPairLabel>("SET review_mode = ? WHERE pair_id = ? AND user_id = ? IF review_mode = ?",
                "blind", label.PairId, label.UserId, VisualPairLabel.PendingReviewMode);
    }

    public async Task MarkDiagnosticExposureAsync(VisualPairCandidate candidate, string userId)
    {
        await InitializeAsync();
        // Retain both lookup entries even if the candidate expires without receiving a vote.
        await IndexAsync(candidate.A, candidate.B, candidate.PairId, null);
        await mapper.ExecuteAsync(Cql.New(
            "INSERT INTO visual_pair_diagnostic_exposures (pair_id, user_id, a_platform, a_listing_id, b_platform, b_listing_id) VALUES (?, ?, ?, ?, ?, ?)",
            candidate.PairId, userId, candidate.APlatformValue, candidate.AListingId, candidate.BPlatformValue, candidate.BListingId)
            .WithOptions(o => o.SetConsistencyLevel(global::Cassandra.ConsistencyLevel.Quorum)));
        await UpgradeLabelExposureAsync(candidate.PairId, userId);
    }

    private Task<AppliedInfo<VisualPairLabel>> UpgradeLabelExposureAsync(string pairId, string userId) =>
        mapper.UpdateIfAsync<VisualPairLabel>("SET review_mode = ? WHERE pair_id = ? AND user_id = ? IF EXISTS",
            "diagnostic", pairId, userId);

    public async Task<(int Candidates, int Labels)> DeleteForListingAsync(Platform platform, string listingId)
    {
        await InitializeAsync();
        var platformValue = (int)platform;
        var pairIds = (await byListing.Where(l => l.PlatformValue == platformValue && l.ListingId == listingId).ExecuteAsync())
            .Select(l => l.PairId).ToHashSet();

        // rows from before the lookup table: the candidates are still there for 90 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var chunk in Enumerable.Range(0, 90).Chunk(10))
        {
            var scanned = await Task.WhenAll(chunk.Select(i => GetCandidatesAsync(today.AddDays(-i), 100000)));
            foreach (var c in scanned.SelectMany(x => x))
                if ((c.APlatformValue == platformValue && c.AListingId == listingId) || (c.BPlatformValue == platformValue && c.BListingId == listingId))
                    pairIds.Add(c.PairId);
        }

        int removedCandidates = 0, removedLabels = 0;
        foreach (var pairId in pairIds)
        {
            var pairLabels = (await labels.Where(l => l.PairId == pairId).ExecuteAsync()).ToList();
            var candidate = await GetCandidateAsync(pairId);
            var pairExposures = (await mapper.FetchAsync<VisualPairDiagnosticExposure>(
                "SELECT * FROM visual_pair_diagnostic_exposures WHERE pair_id = ?", pairId)).ToList();
            var sides = candidate != null ? new[] { candidate.A, candidate.B }
                : pairLabels.Count > 0 ? new[] { pairLabels[0].A, pairLabels[0].B }
                : pairExposures.Count > 0 ? new[] {
                    new VisualPairImage((Platform)pairExposures[0].APlatformValue, pairExposures[0].AListingId, 0, ""),
                    new VisualPairImage((Platform)pairExposures[0].BPlatformValue, pairExposures[0].BListingId, 0, "") } : [];
            if (candidate != null)
            {
                await mapper.ExecuteAsync("DELETE FROM visual_pair_candidates WHERE day = ? AND shard = ? AND pair_id = ?",
                    candidate.Day, candidate.Shard, pairId);
                removedCandidates++;
            }
            await mapper.ExecuteAsync("DELETE FROM visual_pair_candidate_days WHERE pair_id = ?", pairId);
            if (pairLabels.Count > 0)
                await mapper.ExecuteAsync("DELETE FROM visual_pair_labels WHERE pair_id = ?", pairId);
            await mapper.ExecuteAsync("DELETE FROM visual_pair_diagnostic_exposures WHERE pair_id = ?", pairId);
            removedLabels += pairLabels.Count;
            foreach (var side in sides)
            {
                var sidePlatform = (int)side.Platform;
                await mapper.ExecuteAsync("DELETE FROM visual_pairs_by_listing WHERE platform = ? AND listing_id = ? AND pair_id = ?",
                    sidePlatform, side.ListingId, pairId);
            }
        }
        // rows of this listing whose pair is gone already (expired candidate, no label)
        await mapper.ExecuteAsync("DELETE FROM visual_pairs_by_listing WHERE platform = ? AND listing_id = ?", platformValue, listingId);
        return (removedCandidates, removedLabels);
    }

    public async Task<(IReadOnlyList<VisualPairLabel> Labels, byte[]? NextPagingState)> GetLabelsAsync(int pageSize, byte[]? pagingState)
    {
        await InitializeAsync();
        var query = Cql.New("SELECT * FROM visual_pair_labels").WithOptions(o => {
            o.SetPageSize(pageSize);
            if (pagingState != null) o.SetPagingState(pagingState);
        });
        var page = await mapper.FetchPageAsync<VisualPairLabel>(query);
        return (page.Select(VisualPairLabel.NormalizePendingReviewMode).ToList(), page.PagingState);
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
    private readonly Dictionary<(string PairId, string UserId), VisualPairDiagnosticExposure> exposures = new();

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

    public Task<bool> UpgradeCandidateSourceAsync(string pairId, string source)
    {
        lock (gate)
        {
            if (!candidates.TryGetValue(pairId, out var c) || c.Source == source)
                return Task.FromResult(false);
            c.Source = source;
            return Task.FromResult(true);
        }
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
            var key = (label.PairId, label.UserId);
            if (labels.TryGetValue(key, out var previous))
                label.ReviewMode = VisualPairLabel.PreserveReviewMode(previous.ReviewMode, label.ReviewMode);
            if (exposures.ContainsKey(key))
                label.ReviewMode = "diagnostic";
            labels[key] = label;
        }
        return Task.CompletedTask;
    }

    public Task MarkDiagnosticExposureAsync(VisualPairCandidate candidate, string userId)
    {
        lock (gate)
        {
            var key = (candidate.PairId, userId);
            exposures[key] = new VisualPairDiagnosticExposure {
                PairId = candidate.PairId, UserId = userId,
                APlatformValue = candidate.APlatformValue, AListingId = candidate.AListingId,
                BPlatformValue = candidate.BPlatformValue, BListingId = candidate.BListingId };
            if (labels.TryGetValue(key, out var label))
                label.ReviewMode = "diagnostic";
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

    public Task<(int Candidates, int Labels)> DeleteForListingAsync(Platform platform, string listingId)
    {
        bool Refs(VisualPairImage i) => i.Platform == platform && i.ListingId == listingId;
        lock (gate)
        {
            var candidateIds = candidates.Values.Where(c => Refs(c.A) || Refs(c.B)).Select(c => c.PairId).ToList();
            var labelKeys = labels.Where(kv => Refs(kv.Value.A) || Refs(kv.Value.B)).Select(kv => kv.Key).ToList();
            var exposedPairs = exposures.Values.Where(e =>
                ((Platform)e.APlatformValue == platform && e.AListingId == listingId)
                || ((Platform)e.BPlatformValue == platform && e.BListingId == listingId)).Select(e => e.PairId).ToHashSet();
            foreach (var key in exposures.Keys.Where(k => exposedPairs.Contains(k.PairId)).ToList()) exposures.Remove(key);
            foreach (var id in candidateIds) candidates.Remove(id);
            foreach (var key in labelKeys) labels.Remove(key);
            return Task.FromResult((candidateIds.Count, labelKeys.Count));
        }
    }

    public Task<IReadOnlyDictionary<string, long>> CountLabelsAsync()
    {
        lock (gate)
            return Task.FromResult<IReadOnlyDictionary<string, long>>(
                labels.Values.GroupBy(l => l.Label).ToDictionary(g => g.Key, g => (long)g.Count()));
    }
}
