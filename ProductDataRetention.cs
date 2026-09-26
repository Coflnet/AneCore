namespace Coflnet.Ane;

/// <summary>
/// Maximum Cassandra lifetimes for derived ANE product data.
/// </summary>
public static class ProductDataRetention
{
    public const int ActiveProductListingTtlSeconds = 90 * 24 * 60 * 60;
    public const int InactiveProductListingTtlSeconds = 30 * 24 * 60 * 60;
    public const int ProductTtlSeconds = 365 * 24 * 60 * 60;
    public const int PriceHistoryTtlSeconds = 3 * 365 * 24 * 60 * 60;

    public static int GetProductListingTtlSeconds(
        ProductListing listing,
        DateTime? now = null)
    {
        if (listing.IsActive)
            return ActiveProductListingTtlSeconds;

        return listing.InactiveSince is { } inactiveSince
            ? GetRemainingTtlSeconds(
                inactiveSince,
                InactiveProductListingTtlSeconds,
                now ?? DateTime.UtcNow)
            : InactiveProductListingTtlSeconds;
    }

    public static int GetLegacyProductListingTtlSeconds(
        ProductListing listing,
        DateTime? now = null)
    {
        if (!listing.IsActive)
            return GetProductListingTtlSeconds(listing, now);

        return GetRemainingTtlSeconds(
            listing.FoundAt,
            ActiveProductListingTtlSeconds,
            now ?? DateTime.UtcNow);
    }

    public static int GetProductTtlSeconds(Product product, DateTime? now = null) =>
        GetRemainingTtlSeconds(
            product.LastUpdated,
            ProductTtlSeconds,
            now ?? DateTime.UtcNow);

    public static int GetPriceHistoryTtlSeconds(PricePoint pricePoint, DateTime? now = null) =>
        GetRemainingTtlSeconds(
            pricePoint.Date,
            PriceHistoryTtlSeconds,
            now ?? DateTime.UtcNow);

    private static int GetRemainingTtlSeconds(
        DateTime start,
        int maximumTtlSeconds,
        DateTime now)
    {
        if (start == default)
            return maximumTtlSeconds;

        var remaining = start.AddSeconds(maximumTtlSeconds) - now;
        return Math.Clamp(
            (int)Math.Ceiling(remaining.TotalSeconds),
            1,
            maximumTtlSeconds);
    }
}
