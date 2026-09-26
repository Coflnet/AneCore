namespace Coflnet.Ane;

/// <summary>
/// Maximum lifetime of raw listing data (OpenSearch listing/sample indices and the Scylla <c>listings</c> table).
/// Derived product data has its own limits in <see cref="ProductDataRetention"/>.
/// </summary>
public static class ListingRetention
{
    /// <summary>Listings older than this are removed from every listing store.</summary>
    public const int Days = 14;

    public static readonly TimeSpan Period = TimeSpan.FromDays(Days);

    /// <summary>Scylla <c>listings</c> table default TTL: safety net for writes without an explicit TTL.</summary>
    public const int TableDefaultTtlSeconds = Days * 24 * 60 * 60;

    /// <summary>
    /// TTL of the listing snapshot the notifier writes before extraction (needed by extraction callbacks,
    /// filter backtests and product stats). Shorter than <see cref="Days"/>.
    /// </summary>
    public const int SnapshotTtlSeconds = 7 * 24 * 60 * 60;

    /// <summary>Documents found before this instant must be gone.</summary>
    public static DateTime Cutoff(DateTime now, int days = Days) => now.AddDays(-days);
}
