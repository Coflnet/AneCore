using Microsoft.Extensions.Logging;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// In-memory, periodically refreshed snapshot of the known-products catalog. Reads never touch the
/// store - they use <see cref="Snapshot"/>, a plain reference swap so concurrent readers always see a
/// complete, consistent list. A failed refresh (store unreachable, etc.) logs and keeps the previous
/// snapshot; it never throws into the caller, so it is safe to call from the hot listing-processing path.
/// </summary>
public class KnownProductCatalog : IDisposable
{
    private readonly IKnownProductStore store;
    private readonly ILogger<KnownProductCatalog>? logger;
    private readonly TimeSpan refreshInterval;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private volatile IReadOnlyList<KnownProduct> snapshot = Array.Empty<KnownProduct>();
    private volatile KnownProductMatcher matcher = new(Array.Empty<KnownProduct>());
    private Timer? timer;

    public KnownProductCatalog(IKnownProductStore store, ILogger<KnownProductCatalog>? logger = null, TimeSpan? refreshInterval = null)
    {
        this.store = store;
        this.logger = logger;
        this.refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>Current known products. Never null; empty until the first successful refresh.</summary>
    public IReadOnlyList<KnownProduct> Snapshot => snapshot;

    /// <summary>
    /// A <see cref="KnownProductMatcher"/> built once for the current <see cref="Snapshot"/> and swapped
    /// atomically whenever the snapshot refreshes. Callers (AneNotifier per listing, AneApi per search
    /// query) should use this instead of constructing <c>new KnownProductMatcher(catalog.Snapshot)</c>
    /// themselves - that used to rebuild the matcher's alias index on every single call.
    /// </summary>
    public KnownProductMatcher Matcher => matcher;

    /// <summary>
    /// Reloads the snapshot from the store. Swallows and logs any failure, keeping the previous
    /// snapshot in place - callers (including the periodic timer) never need to handle exceptions.
    /// </summary>
    public async Task RefreshAsync()
    {
        await refreshLock.WaitAsync();
        try
        {
            var loaded = await store.GetAllAsync();
            snapshot = loaded;
            matcher = new KnownProductMatcher(loaded);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "Known product catalog refresh failed, keeping previous snapshot of {count} products",
                snapshot.Count);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    /// <summary>Starts the periodic background refresh (no-op if already started). Does an immediate refresh first.</summary>
    public void StartPeriodicRefresh()
    {
        if (timer != null)
            return;
        timer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, refreshInterval);
    }

    public void Dispose()
    {
        timer?.Dispose();
        timer = null;
        refreshLock.Dispose();
    }
}
