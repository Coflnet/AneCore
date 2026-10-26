using System.Text.RegularExpressions;

namespace Coflnet.Ane;

/// <summary>Whole IKEA furniture offers in the observed furniture categories; no exact catalogue or variant claim.</summary>
public static class FurnitureOfferRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Furniture = @"(?:schrank|kleiderschrank|kommode|nachttisch|regal|bett|bettgestell|sofa|couch|sessel|esstisch|schreibtisch|tisch|stuhl)";

    public static bool CanExtract(Listing listing) =>
        listing.Platform == Platform.Kleinanzeigen && !string.IsNullOrWhiteSpace(listing.Id) && Regex.IsMatch(listing.Id, @"^[1-9][0-9]*$", Options)
        && CanExtract(listing.Title, listing.Category, listing.Description);

    public static bool CanExtract(string? title, string? category, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(category)) return false;
        var leaf = category.Split(" / ").Last().Trim();
        if (!new[] { "Schlafzimmer", "Wohnzimmer", "Küche & Esszimmer" }.Contains(leaf, StringComparer.OrdinalIgnoreCase)) return false;
        if (!Regex.IsMatch(title, @"\bikea\b", Options) || !Regex.IsMatch(title, @"\b" + Furniture + @"\b", Options)) return false;
        // Components, imitations, wanted ads and several pieces must never become a whole-furniture offer.
        return !Regex.IsMatch(title,
            @"\b(?:suche|gesucht|ankauf|wanted|tausche|mieten|vermiet\p{L}*|reparatur\p{L}*|service|ersatz\p{L}*|zubeh[oö]r"
            + @"|beschl[aä]ge|schrauben|griffe|fronten|t[uü]r(?:en)?|schublade|schubladenfront\p{L}*|einlegeb[oö]den|bettvorhang|vorhang|matratze|auflage|bezug|husse|anleitung|handbuch|verpackung"
            + @"|miniatur\p{L}*|puppen\p{L}*|spielzeug|nachbau|[aä]hnlich|konvolut|sammlung|bundle|paket|set|essgruppe)\b"
            + @"|\b(?:f[uü]r|passend|wie)\s+(?:das\s+|den\s+|ein\s+)?(?:ikea|" + Furniture + @")\b"
            + @"|\b(?:[2-9]|[1-9]\d+)\s*(?:x(?!\s*\d)|st[uü]ck)\b"
            + @"|(?:\+|\bund\b|\bmit\b|&)(?:\s+\p{L}+){0,2}\s*" + Furniture + @"\b", Options)
            && !Regex.IsMatch(description ?? "", @"^\s*(?:suche|gesucht|ankauf|wanted|tausche)\b|^(?:(?:ich\s+)?verkaufe|biete|selling)\b[^.\n]{0,80}\b(?:konvolut|bundle|paket|set|" + Furniture + @"\s+und\s+(?:ikea\s+)?(?:\p{L}+\s+)?" + Furniture + @")\b", Options);
    }
}
