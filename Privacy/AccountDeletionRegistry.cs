using System.Collections.Concurrent;
using System.Text;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Privacy;

/// <summary>
/// Durable suppression marker shared by AneApi and AneNotifier. The pseudonymous user id is
/// retained after erasure solely to prevent asynchronous writers from recreating deleted data.
/// Pseudonymous listener ids are retained with it so queued notifications can still be purged
/// after either service restarts.
/// </summary>
public sealed class AccountDeletionRegistry
{
    private readonly Table<AccountDeletionMarker> markers;
    private readonly Table<AccountDeletionListenerMarker> listenerMarkers;
    private readonly ConcurrentDictionary<string, byte> knownDeleted = new();

    public AccountDeletionRegistry(ISession session)
    {
        var mapping = new MappingConfiguration().Define(new Map<AccountDeletionMarker>()
            .PartitionKey(marker => marker.UserId)
            .Column(marker => marker.RequestedAt, cm => cm.WithDbType<DateTime>()));
        markers = new Table<AccountDeletionMarker>(
            session, mapping, "ane_account_deletions");
        markers.CreateIfNotExists();

        var listenerMapping = new MappingConfiguration().Define(
            new Map<AccountDeletionListenerMarker>()
                .PartitionKey(marker => marker.UserId)
                .ClusteringKey(marker => marker.ListenerId));
        listenerMarkers = new Table<AccountDeletionListenerMarker>(
            session, listenerMapping, "ane_account_deletion_listeners");
        listenerMarkers.CreateIfNotExists();
    }

    public async Task MarkDeletedAsync(string userId)
    {
        userId = CanonicalizeUserId(userId);
        await markers.Insert(new AccountDeletionMarker
        {
            UserId = userId,
            RequestedAt = DateTime.UtcNow
        }).ExecuteAsync();
        knownDeleted[userId] = 0;
    }

    public async Task<bool> IsDeletedAsync(string userId)
    {
        userId = CanonicalizeUserId(userId);
        if (knownDeleted.ContainsKey(userId))
            return true;

        var result = await markers
            .Where(marker => marker.UserId == userId)
            .ExecuteAsync();
        if (!result.Any())
            return false;

        knownDeleted[userId] = 0;
        return true;
    }

    public async Task<IReadOnlyCollection<string>> GetDeletedUserIdsAsync()
    {
        var result = await markers.Select(marker => marker).ExecuteAsync();
        var ids = result.Select(marker => marker.UserId).ToList();
        foreach (var id in ids)
            knownDeleted[id] = 0;
        return ids;
    }

    public async Task RecordListenerIdsAsync(string userId, IEnumerable<long> listenerIds)
    {
        userId = CanonicalizeUserId(userId);
        var ids = listenerIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var batch = listenerMarkers.GetSession().CreateBatch();
        foreach (var listenerId in ids)
            batch.Append(listenerMarkers.Insert(new AccountDeletionListenerMarker
            {
                UserId = userId,
                ListenerId = listenerId
            }));
        await batch.ExecuteAsync();
    }

    public async Task<IReadOnlyCollection<long>> GetListenerIdsAsync(string userId)
    {
        userId = CanonicalizeUserId(userId);
        var result = await listenerMarkers
            .Where(marker => marker.UserId == userId)
            .ExecuteAsync();
        return result.Select(marker => marker.ListenerId).ToList();
    }

    public static string CanonicalizeUserId(string userId) =>
        Guid.TryParse(userId, out var parsed)
            ? parsed.ToString()
            : userId.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    private sealed class AccountDeletionMarker
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }

    private sealed class AccountDeletionListenerMarker
    {
        public string UserId { get; set; } = string.Empty;
        public long ListenerId { get; set; }
    }
}
