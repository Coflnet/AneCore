using System.Security.Cryptography;
using System.Text;
using MessagePack;
using KeyAttribute = MessagePack.KeyAttribute;

namespace Coflnet.Ane.TrainingListings;

/// <summary>
/// One marketplace listing as training and evaluation data (text first: title/description for a product title NER model).
/// Carries NO personal data of the seller: no user id, contact, street, coordinates or seller name, and the free text is masked
/// (see <see cref="TrainingTextSanitizer"/>). MessagePack on the Kafka topic <c>TOPICS:TRAINING_LISTING</c>; the export
/// endpoint writes it as camelCase JSON with the names of the properties.
/// </summary>
[MessagePackObject]
public class TrainingListing
{
    [Key(0)] public Platform Platform { get; set; }
    [Key(1)] public string ListingId { get; set; } = "";
    [Key(2)] public string? Url { get; set; }
    [Key(3)] public string? Title { get; set; }
    [Key(4)] public string? Description { get; set; }
    [Key(5)] public string? DescriptionShort { get; set; }
    [Key(6)] public string? Category { get; set; }
    [Key(7)] public string[]? Categories { get; set; }
    /// <summary>Product attributes of the page, without seller/contact/address keys, values masked.</summary>
    [Key(8)] public Dictionary<string, string>? Attributes { get; set; }
    [Key(9)] public double? Price { get; set; }
    [Key(10)] public string? Currency { get; set; }
    [Key(11)] public PriceKind PriceKind { get; set; }
    /// <summary><c>new</c>, <c>used</c> or <c>broken</c> when the scraper derived it, otherwise null.</summary>
    [Key(12)] public string? Condition { get; set; }
    /// <summary>All photo urls of the offer that are known (only urls, the photos themselves are not part of this message).</summary>
    [Key(13)] public string[]? ImageUrls { get; set; }
    [Key(14)] public string? Country { get; set; }
    [Key(15)] public string? Region { get; set; }
    [Key(16)] public string? Locality { get; set; }
    [Key(17)] public bool? Commercial { get; set; }
    [Key(18)] public string? Shipping { get; set; }
    [Key(19)] public DateTime? CreatedAt { get; set; }
    /// <summary>When our crawler downloaded the offer (UTC); decides the day of the export partition.</summary>
    [Key(20)] public DateTime? FirstSeenAt { get; set; }
    /// <summary>One of <see cref="TrainingListingScope"/>: what the scope filter decided for the product pipeline.</summary>
    [Key(21)] public string Scope { get; set; } = TrainingListingScope.Kept;
    /// <summary>Drop reason or vertical key of the scope decision, null when there is none.</summary>
    [Key(22)] public string? ScopeReason { get; set; }
}

/// <summary>Values of <see cref="TrainingListing.Scope"/>.</summary>
public static class TrainingListingScope
{
    public const string Kept = "kept";
    public const string Dropped = "dropped";
    public const string Sampled = "sampled";
    /// <summary>Rows copied from the <c>listings</c> table by the backfill: they were kept by definition, only kept listings are stored there.</summary>
    public const string Backfill = Kept;

    public static bool IsValid(string? scope) => scope is Kept or Dropped or Sampled;
}

/// <summary>
/// Which platforms may end up in the training data. Only a machine readable statement in the robots.txt of a marketplace counts
/// as a rights reservation: Vinted and eBay have one and are refused in code, whatever the configuration says.
/// </summary>
public static class TrainingListingPolicy
{
    public const string DefaultPlatforms = "Kleinanzeigen,Marktplaats";

    public static readonly IReadOnlySet<Platform> Forbidden = new HashSet<Platform> { Platform.Vinted, Platform.Ebay };

    public static bool IsForbidden(Platform platform) => Forbidden.Contains(platform);

    /// <summary>Parses the comma list of <c>TRAINING_LISTINGS:PLATFORMS</c> (null means the default, empty means none).</summary>
    public static TrainingPlatforms ParsePlatforms(string? setting)
    {
        var allowed = new HashSet<Platform>();
        var refused = new List<Platform>();
        var unknown = new List<string>();
        foreach (var name in (setting ?? DefaultPlatforms).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Platform>(name, true, out var platform) || platform == Platform.Unknown)
                unknown.Add(name);
            else if (IsForbidden(platform))
                refused.Add(platform);
            else
                allowed.Add(platform);
        }
        return new TrainingPlatforms(allowed, refused, unknown);
    }
}

/// <summary>Result of <see cref="TrainingListingPolicy.ParsePlatforms"/>: the usable platforms and what was configured but refused.</summary>
public sealed record TrainingPlatforms(IReadOnlySet<Platform> Allowed, IReadOnlyList<Platform> Refused, IReadOnlyList<string> Unknown)
{
    public bool Contains(Platform platform) => Allowed.Contains(platform) && !TrainingListingPolicy.IsForbidden(platform);
}

/// <summary>Partition key helpers of the export table.</summary>
public static class TrainingListingKeys
{
    public const int ShardCount = 16;

    /// <summary>The store did not exist before; walks over days start here.</summary>
    public static readonly DateOnly EarliestDay = new(2026, 9, 1);

    public static string DayBucket(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd");

    public static string DayBucket(DateOnly day) => day.ToString("yyyy-MM-dd");

    public static bool TryParseDay(string? text, out DateOnly day) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day);

    /// <summary>Stable shard 0..15 of a listing (hash of platform and id).</summary>
    public static int ShardOf(int platform, string listingId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{platform}:{listingId}"))[0] % ShardCount;
}
