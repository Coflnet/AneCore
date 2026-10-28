using Cassandra;
using System.Text.Json;

namespace Coflnet.Ane.ShopReferences;

/// <summary>Separate rows survive unrelated whole-product saves; payload CAS preserves newer observations.</summary>
public sealed class CassandraProductStoreReferenceStore(ISession session) : IProductStoreReferenceStore
{
    private readonly object gate = new();
    private Task? initialize;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        lock (gate)
        {
            if (initialize?.IsFaulted == true || initialize?.IsCanceled == true) initialize = null;
            initialize ??= session.ExecuteAsync(new SimpleStatement(
                "CREATE TABLE IF NOT EXISTS product_store_references (product_id text, provider text, payload text, PRIMARY KEY ((product_id), provider)) WITH default_time_to_live = 900"));
            return initialize.WaitAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ProductStoreReference>> GetAsync(string productId, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var rows = await session.ExecuteAsync(new SimpleStatement(
            "SELECT payload FROM product_store_references WHERE product_id = ?", productId).SetConsistencyLevel(ConsistencyLevel.Serial)).WaitAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return rows.Select(row => JsonSerializer.Deserialize<ProductStoreReference>(row.GetValue<string>("payload")))
            .Where(reference => reference != null && ProductStoreReferencePolicy.IsFresh(reference, now)).Cast<ProductStoreReference>().ToList();
    }

    public async Task UpsertAsync(string productId, ProductStoreReference reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productId) || !ProductStoreReferencePolicy.IsFresh(reference, DateTimeOffset.UtcNow))
            throw new ArgumentException("An existing product identity and fresh store observation are required.");
        await InitializeAsync(ct);
        var payload = JsonSerializer.Serialize(reference);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var rows = await session.ExecuteAsync(new SimpleStatement(
                "SELECT payload FROM product_store_references WHERE product_id = ? AND provider = ?", productId, reference.Provider).SetConsistencyLevel(ConsistencyLevel.Serial)).WaitAsync(ct);
            var previousPayload = rows.FirstOrDefault()?.GetValue<string>("payload");
            var previous = previousPayload == null ? null : JsonSerializer.Deserialize<ProductStoreReference>(previousPayload);
            if (previous != null && previous.ObservedAt >= reference.ObservedAt) return;
            var write = previousPayload == null
                ? new SimpleStatement("INSERT INTO product_store_references (product_id, provider, payload) VALUES (?, ?, ?) IF NOT EXISTS", productId, reference.Provider, payload)
                : new SimpleStatement("UPDATE product_store_references SET payload = ? WHERE product_id = ? AND provider = ? IF payload = ?", payload, productId, reference.Provider, previousPayload);
            var applied = await session.ExecuteAsync(write).WaitAsync(ct);
            if (applied.First().GetValue<bool>("[applied]")) return;
        }
        throw new InvalidOperationException("Concurrent store observations could not be persisted; retry after reading the accepted state.");
    }
}
