using System.Security.Cryptography;
using System.Text;

namespace Coflnet.Ane.VisualPairs;

/// <summary>Deterministic identifiers for a pair of photos, shared by everything that writes or reads visual pairs.</summary>
public static class VisualPairId
{
    public const int ShardCount = 8;

    /// <summary>Key of one photo: <c>platform:listingId:imageIndex</c> with the platform enum name (e.g. <c>Vinted:123:0</c>).</summary>
    public static string ImageKey(Platform platform, string listingId, int imageIndex) =>
        $"{platform}:{listingId}:{imageIndex}";

    /// <summary>
    /// Lower-hex SHA-256 of the two image keys, sorted ordinally and joined with <c>|</c>, first 32 hex characters.
    /// The order of the two photos does not matter.
    /// </summary>
    public static string Compute(string imageKeyA, string imageKeyB)
    {
        var ordered = string.CompareOrdinal(imageKeyA, imageKeyB) <= 0
            ? imageKeyA + "|" + imageKeyB
            : imageKeyB + "|" + imageKeyA;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ordered))).ToLowerInvariant()[..32];
    }

    public static string Compute(Platform platformA, string listingA, int indexA, Platform platformB, string listingB, int indexB) =>
        Compute(ImageKey(platformA, listingA, indexA), ImageKey(platformB, listingB, indexB));

    /// <summary>Day bucket of the candidate table, UTC <c>yyyy-MM-dd</c>.</summary>
    public static string DayBucket(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd");

    public static string DayBucket(DateOnly day) => day.ToString("yyyy-MM-dd");

    /// <summary>Sub-partition of a day (keeps partitions small), stable per pair id.</summary>
    public static int ShardOf(string pairId) =>
        pairId.Length >= 2 && int.TryParse(pairId.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? b % ShardCount : 0;
}

/// <summary>The label values a labeler may give.</summary>
public static class VisualPairLabels
{
    public const string SameItem = "same_item";
    public const string SameModelOtherVariant = "same_model_other_variant";
    public const string Different = "different";
    public const string Unsure = "unsure";

    public static readonly IReadOnlyList<string> All = [SameItem, SameModelOtherVariant, Different, Unsure];

    public static bool IsValid(string? label) => label != null && All.Contains(label);
}

/// <summary>The values of <see cref="VisualPairCandidate.Decision"/>.</summary>
public static class VisualPairDecisions
{
    public const string Join = "join";
    public const string Seeded = "seeded";
    public const string NearMiss = "near_miss";
}
