using System.Text.RegularExpressions;
using Coflnet.Ane.KnownProducts;

namespace Coflnet.Ane;

/// <summary>Explicit physical figure offers; title evidence establishes no manufacturer, model or contents.</summary>
public static class FigureOfferRules
{
    public const string OfferKind = "collectible_figure_offer";
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    public static bool IsFigureTitle(string? title)
    {
        var text = KnownProductMatcher.Normalize(title ?? "");
        if (!Regex.IsMatch(text, @"^(?:figurka|figurki|figur(?:en)?|figurines?)\b", Options)
            || Regex.IsMatch(text, @"\blego\b", Options)) return false;
        // An included manual is not the item sold; a manual-only title remains paper.
        text = Regex.Replace(text, @"\b(?:mit|with|inkl(?:usive)?)\s+(?:bauanleitung|anleitung|manuals?|instructions?)\b", " ", Options);
        return !Regex.IsMatch(text, @"\b(?:suche|gesucht|wanted|ankauf|szukam|kupie|rental|vermiet\w*|service|reparatur\w*|repair|pdf|epub|stl|obj|downloads?|digital|e\s*books?|books?|buch|handbuch|bauanleitung|anleitung|manuals?|instructions?|posters?|katalog|nur\s+(?:box|karton|verpackung)|empty\s+box|leerkarton)\b", Options);
    }

    public static bool CanExtract(string? title, Platform? platform, string? id) =>
        platform is { } native && native != Platform.Unknown && Enum.IsDefined(native)
        && !string.IsNullOrWhiteSpace(id) && id != "0" && !string.Equals(id, "Unknown", StringComparison.OrdinalIgnoreCase)
        && IsFigureTitle(title);
}
