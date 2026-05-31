using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Crowd;

/// <summary>
/// Tracks reward points users earn for contributing crowd-sourced scraping work.
/// Backed by a Cassandra counter table so that any service (AneApi, AneScrapper, ...)
/// can increment a user's balance asynchronously without read-modify-write races.
/// </summary>
public class UserPointsService
{
    private readonly Table<UserPoints> table;

    public UserPointsService(ISession session)
    {
        var mapping = new MappingConfiguration().Define(new Map<UserPoints>()
            .TableName("user_points")
            .PartitionKey(p => p.UserId)
            .Column(p => p.UserId, cm => cm.WithName("user_id"))
            .Column(p => p.Points, cm => cm.WithName("points").WithDbType<long>().AsCounter()));
        table = new Table<UserPoints>(session, mapping);
        table.CreateIfNotExists();
    }

    /// <summary>
    /// Atomically increase a user's point balance. Safe to call concurrently from
    /// multiple services because it relies on a Cassandra counter.
    /// </summary>
    public async Task AddPointsAsync(string userId, long amount)
    {
        if (string.IsNullOrWhiteSpace(userId) || amount == 0)
            return;
        await table.Where(p => p.UserId == userId)
            .Select(p => new UserPoints { Points = amount })
            .Update()
            .ExecuteAsync();
    }

    /// <summary>
    /// Read the current point balance for a user (0 when the user has none yet).
    /// </summary>
    public async Task<long> GetPointsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;
        var result = await table.Where(p => p.UserId == userId).ExecuteAsync();
        return result.FirstOrDefault()?.Points ?? 0;
    }

    public class UserPoints
    {
        public string UserId { get; set; } = string.Empty;
        public long Points { get; set; }
    }
}
