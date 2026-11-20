using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>Shape rules of single LEGO part pages (<c>lego-part-3001-red-used</c>) shared by the notifier's flips and the API's catch-all rule.</summary>
public static partial class LegoPartPageRules
{
    public const string Kind = "lego_part";
    public const string SeoPrefix = "lego-part-";

    /// <summary>
    /// The colour-less part page (<c>lego-part-3001-used</c>) pools every offer of the part whose colour is unknown or ambiguous, so its
    /// prices compare different colours: a catch-all. A coloured part page (<c>lego-part-3001-red-used</c>) stays comparable.
    /// </summary>
    public static bool IsColourlessPartPage(string? seoId, IReadOnlyDictionary<string, string>? attributes, string? identifierType = null)
    {
        if (!string.IsNullOrEmpty(seoId) && ColourlessId().IsMatch(seoId))
            return true;
        var isPart = identifierType == Kind || attributes?.GetValueOrDefault("product_kind") == Kind
                     || seoId?.StartsWith(SeoPrefix, StringComparison.Ordinal) == true;
        return isPart && string.IsNullOrWhiteSpace(attributes?.GetValueOrDefault("lego_color_id"))
                      && !(seoId != null && ColouredId().IsMatch(seoId));
    }

    // lego-part-<part number>-<condition>; a coloured id has the colour slug between them
    [GeneratedRegex(@"^lego-part-[0-9a-z]+-(?:new|used)$")]
    private static partial Regex ColourlessId();

    [GeneratedRegex(@"^lego-part-[0-9a-z]+-[0-9a-z-]+-(?:new|used)$")]
    private static partial Regex ColouredId();
}
