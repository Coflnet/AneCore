namespace Coflnet.Ane.KnownProducts;

/// <summary>Persistence for the known-products catalog. Implemented by Cassandra (production) and in-memory (tests).</summary>
public interface IKnownProductStore
{
    /// <summary>Creates backing storage if needed. Safe to call repeatedly; no-op for in-memory stores.</summary>
    Task InitializeAsync();

    Task<IReadOnlyList<KnownProduct>> GetAllAsync();

    Task<KnownProduct?> GetAsync(string id);

    /// <summary>Insert or fully replace a known product.</summary>
    Task UpsertAsync(KnownProduct product);

    Task DeleteAsync(string id);
}

/// <summary>
/// In-memory <see cref="IKnownProductStore"/> for tests and any other non-Cassandra-backed usage.
/// Thread-safe; no persistence beyond process lifetime.
/// </summary>
public class InMemoryKnownProductStore : IKnownProductStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, KnownProduct> products
        = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryKnownProductStore()
    {
    }

    public InMemoryKnownProductStore(IEnumerable<KnownProduct> seed)
    {
        foreach (var product in seed)
            products[product.Id] = product;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<KnownProduct>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<KnownProduct>>(products.Values.ToList());

    public Task<KnownProduct?> GetAsync(string id) =>
        Task.FromResult(products.TryGetValue(id, out var product) ? product : null);

    public Task UpsertAsync(KnownProduct product)
    {
        products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        products.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
