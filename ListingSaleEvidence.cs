using Cassandra;
using System.Text.Json;

namespace Coflnet.Ane;

/// <summary>Observed status history. SoldObservedAt is when a declaration was seen, not the transaction date.</summary>
public sealed record ListingSaleEvidence
{
    public string Status { get; init; } = "unknown";
    public string? Evidence { get; init; }
    public string? AgeBasis { get; init; }
    public DateTime CheckedAt { get; init; }
    public DateTime? LastAvailableAt { get; init; }
    public DateTime? FirstUnavailableAt { get; init; }
    public DateTime? SoldObservedAt { get; init; }

    public static ListingSaleEvidence Apply(ListingSaleEvidence? previous, RecheckResult result)
    {
        previous ??= new();
        if (result.Status == RecheckStatus.Unknown || result.CheckedAt <= previous.CheckedAt) return previous;
        var signal = result.Status != RecheckStatus.Gone ? null : previous.Status == "reported_sold" && result.SaleEvidence == SaleSignalPolicy.Inferred ? previous.Evidence : result.SaleEvidence;
        var inferred = signal == SaleSignalPolicy.Inferred;
        var sold = signal is "seller_declared_sold" or "sold_out_marker" or "ebay_sold_search_card"
            || (result.Status == RecheckStatus.Gone && previous.Status is "reported_sold" or "inferred_sold");
        return previous with
        {
            Status = inferred || (sold && previous.Status == "inferred_sold" && signal == null) ? "inferred_sold" : sold ? "reported_sold" : result.Status == RecheckStatus.Available ? "available" : "withdrawn",
            Evidence = sold || inferred ? signal ?? previous.Evidence : null,
            AgeBasis = inferred ? result.AgeBasis : sold ? previous.AgeBasis : null,
            CheckedAt = result.CheckedAt,
            LastAvailableAt = result.Status == RecheckStatus.Available && !sold ? result.CheckedAt : previous.LastAvailableAt,
            FirstUnavailableAt = result.Status == RecheckStatus.Available && !sold ? null : previous.FirstUnavailableAt ?? result.CheckedAt,
            SoldObservedAt = sold || inferred ? previous.SoldObservedAt ?? result.CheckedAt : null
        };
    }
}

public interface IListingSaleEvidenceStore
{
    Task<ListingSaleEvidence?> GetAsync(Platform platform, string id, CancellationToken ct);
    Task RecordAsync(RecheckResult observation, CancellationToken ct);
}

/// <summary>One bounded row per native identity; CAS prevents older observations replacing newer history across API replicas.</summary>
public sealed class CassandraListingSaleEvidenceStore(ISession session) : IListingSaleEvidenceStore
{
    private readonly object gate = new();
    private Task? initialize;
    private Task Initialize()
    {
        lock (gate)
        {
            if (initialize?.IsFaulted == true || initialize?.IsCanceled == true) initialize = null;
            return initialize ??= session.ExecuteAsync(new SimpleStatement(
                "CREATE TABLE IF NOT EXISTS listing_sale_evidence (platform int, id text, payload text, PRIMARY KEY ((platform, id))) WITH default_time_to_live = 15552000"));
        }
    }
    public async Task<ListingSaleEvidence?> GetAsync(Platform platform, string id, CancellationToken ct)
    {
        await Initialize().WaitAsync(ct);
        var rows = await session.ExecuteAsync(new SimpleStatement("SELECT payload FROM listing_sale_evidence WHERE platform = ? AND id = ?", (int)platform, id)).WaitAsync(ct);
        return rows.FirstOrDefault() is { } row ? JsonSerializer.Deserialize<ListingSaleEvidence>(row.GetValue<string>("payload")) : null;
    }
    public async Task RecordAsync(RecheckResult observation, CancellationToken ct)
    {
        if (observation.CheckedAt > DateTime.UtcNow.AddMinutes(5) || observation.CheckedAt < DateTime.UtcNow.AddDays(-180)
            || observation.Status is not (RecheckStatus.Available or RecheckStatus.Gone) || observation.Platform == Platform.Unknown || string.IsNullOrWhiteSpace(observation.ListingId)) return;
        await Initialize().WaitAsync(ct);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var rows = await session.ExecuteAsync(new SimpleStatement("SELECT payload FROM listing_sale_evidence WHERE platform = ? AND id = ?", observation.PlatformValue, observation.ListingId)).WaitAsync(ct);
            var oldPayload = rows.FirstOrDefault()?.GetValue<string>("payload");
            var previous = oldPayload == null ? null : JsonSerializer.Deserialize<ListingSaleEvidence>(oldPayload);
            var updated = ListingSaleEvidence.Apply(previous, observation);
            if (updated == previous) return;
            var payload = JsonSerializer.Serialize(updated);
            var write = oldPayload == null
                ? new SimpleStatement("INSERT INTO listing_sale_evidence (platform, id, payload) VALUES (?, ?, ?) IF NOT EXISTS", observation.PlatformValue, observation.ListingId, payload)
                : new SimpleStatement("UPDATE listing_sale_evidence USING TTL 15552000 SET payload = ? WHERE platform = ? AND id = ? IF payload = ?", payload, observation.PlatformValue, observation.ListingId, oldPayload);
            var applied = await session.ExecuteAsync(write).WaitAsync(ct);
            if (applied.First().GetValue<bool>("[applied]")) return;
        }
        throw new InvalidOperationException("Concurrent availability observations could not be persisted; retry the uncommitted batch.");
    }
}
