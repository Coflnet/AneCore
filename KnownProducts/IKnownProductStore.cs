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

    /// <summary>
    /// The <see cref="KnownProductSeed.Version"/> that was last successfully applied (upserted in full)
    /// to this store, or null if never seeded. Lets a caller like AneNotifier's startup seeding skip
    /// re-upserting tens of thousands of unchanged rows on every restart:
    /// <code>
    /// var applied = await store.GetAppliedSeedVersionAsync();
    /// if (applied != KnownProductSeed.Version)
    /// {
    ///     foreach (var product in KnownProductSeed.LoadAll())
    ///         await store.UpsertAsync(product);
    ///     await store.SetAppliedSeedVersionAsync(KnownProductSeed.Version);
    /// }
    /// </code>
    /// Default-implemented (always "never seeded" / no-op) so pre-existing implementers - e.g. small
    /// test doubles in AneNotifier/AneApi that predate this member - keep compiling unmodified; the real
    /// stores below override both with working persistence.
    /// </summary>
    Task<string?> GetAppliedSeedVersionAsync() => Task.FromResult<string?>(null);

    /// <summary>Records that <paramref name="version"/> (see <see cref="KnownProductSeed.Version"/>) has been fully applied. No-op by default - see <see cref="GetAppliedSeedVersionAsync"/>.</summary>
    Task SetAppliedSeedVersionAsync(string version) => Task.CompletedTask;
}

/// <summary>
/// In-memory <see cref="IKnownProductStore"/> for tests and any other non-Cassandra-backed usage.
/// Thread-safe; no persistence beyond process lifetime.
/// </summary>
public class InMemoryKnownProductStore : IKnownProductStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, KnownProduct> products
        = new(StringComparer.OrdinalIgnoreCase);
    private string? appliedSeedVersion;

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

    public Task<string?> GetAppliedSeedVersionAsync() => Task.FromResult(appliedSeedVersion);

    public Task SetAppliedSeedVersionAsync(string version)
    {
        appliedSeedVersion = version;
        return Task.CompletedTask;
    }
}
