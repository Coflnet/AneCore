namespace Coflnet.Ane;

/// <summary>Approximate disappearance signal requested for fast local-marketplace flips, never a verified transaction.</summary>
public static class SaleSignalPolicy
{
    public const string Inferred = "inferred_disappearance_2_48h";
    public static bool IsLegacyEuroCurrency(Platform platform, string? currency) => string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(currency) && platform is Platform.Kleinanzeigen or Platform.Marktplaats or Platform.Willhaben;
    public static string? AgeBasis(Platform platform, DateTime? createdAt, DateTime? firstSeenAt, DateTime goneAt)
    {
        if (platform is not (Platform.Kleinanzeigen or Platform.Marktplaats or Platform.Willhaben or Platform.OLX)) return null;
        var origin = createdAt ?? firstSeenAt;
        if (!origin.HasValue) return null;
        var hours = (goneAt - origin.Value).TotalHours;
        return hours is >= 2 and <= 48 ? createdAt.HasValue ? "listing_created_at" : "first_seen" : null;
    }
}
