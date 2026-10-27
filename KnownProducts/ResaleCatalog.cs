using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>Shared scraper/rules admission for verified models outside the original relaunch verticals.
/// The scraper has no database-backed KnownProductCatalog: reuse its seed loader and indexed matcher once per process.</summary>
public static class ResaleCatalog
{
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Lazy<KnownProductMatcher> Matcher = new(() => new(KnownProductSeed.LoadNamed("cameras.json", "cameras-expanded.json", "resale-goods.json", "power-tools.json", "bahco-hand-tools.json", "turntables.json")));
    private static readonly Regex NonSale = new(@"\b(?:suche|gesucht|wanted|ankauf|vermiete|vermietung|rental)\b|\b(?:zu\s+vermieten|repair\s+service|reparatur(?:service)?|serviceangebot)\b", Options);
    private static readonly Regex NotOffer = new(@"\b(?:konvolut|sammlung|restposten|bundle|unvollstandig|incomplete|ersatzteile?|einzelteile?|nachbau|kompatibel)\b", Options);
    private static readonly Regex Media = new(@"\b(?:anleitung|bedienungsanleitung|handbuch|manuals?|instructions?|katalog|poster|buch|leerkarton|leerbox)\b", Options);
    private static readonly Regex Parts = new(@"\b(?:glaskanne|kaffeekanne|filterhalter|sattel|lenker|pedale|objektiv|lens|gehause|zahnrad|stativ|tripod)\b", Options);
    private static readonly Regex AccessoryFor = new(@"\b(?:fur|fuer|for|pour|per)\s+(.+)$", Options);
    private static readonly Regex IncludedManual = new(@"\b(?:mit|inkl\.?|inklusive|with)\s+(?:anleitung|handbuch|manual|instructions)\b", Options);
    private static readonly Regex CameraExtras = new(@"\s*(?:\+|mit|inkl\.?|inklusive|with)\s+(?:kamera\s*)?(?:tasche|bag|stativ|tripod)(?:\s*(?:&|und|and|\+)\s*(?:tasche|bag|stativ|tripod))*\s*$", Options);

    private static readonly Regex KeyboardExtras = new(@"\s*,?\s*\b(?:mit|inkl\.?|incl\.?|inklusive|with)\s+(?:netzteil|power\s+adapter|ständer|staender|stand)(?:\s*(?:&|und|and)\s*(?:netzteil|power\s+adapter|ständer|staender|stand))*\s*$", Options);

    /// <summary>Admits a physical hand-tool-related offer naming a verified tool, including sets,
    /// article variants and parts. This title reference proves no whole model, compatibility, quantity
    /// or description contents; consumers must retain independent offer identities and scope.</summary>
    public static bool IsHandToolOffer(string? title, string? description = null, IReadOnlyList<string>? categoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var text = KnownProductMatcher.Normalize(title);
        return !NonSale.IsMatch(text) && !Media.IsMatch(IncludedManual.Replace(text, ""))
            && Matcher.Value.HasHandToolReference(title, categoryPath);
    }

    public static KnownProduct? Match(string? title, string? description = null, IReadOnlyList<string>? categoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var text = KnownProductMatcher.Normalize(title);
        if (NonSale.IsMatch(text) || NotOffer.IsMatch(text) || Media.IsMatch(IncludedManual.Replace(text, ""))) return null;
        var cameraExtras = CameraExtras.IsMatch(title);
        if (cameraExtras) title = CameraExtras.Replace(title, "");
        if (Parts.IsMatch(KnownProductMatcher.Normalize(title))) return null;
        var match = Matcher.Value.Match(title, categoryPath: categoryPath);
        if (cameraExtras && match?.Categories.FirstOrDefault() != "Kameras & Optik") return null;
        if (match?.PossibleAttributes.GetValueOrDefault("device_type")?.Contains("keyboard") == true)
            title = KeyboardExtras.Replace(title, "");
        text = KnownProductMatcher.Normalize(title);
        if (match == null) return null;
        // An accessory for a named model is not the model, across camera, bicycle and toy families.
        if (Matcher.Value.Match(AccessoryFor.Match(text).Groups[1].Value)?.Id == match.Id) return null;
        if (match.PossibleAttributes.GetValueOrDefault("specificity")?.Contains("unspecific") == true
            && match.ExcludeTerms.Any(term => (" " + text + " ").Contains(" " + term + " ", StringComparison.Ordinal))) return null;
        // Reuse the same matcher after removing this model's aliases to detect another named model.
        var remainder = " " + KnownProductMatcher.Normalize(title) + " ";
        foreach (var alias in match.Aliases.OrderByDescending(a => a.Length))
            remainder = remainder.Replace(" " + KnownProductMatcher.Normalize(alias) + " ", " ", StringComparison.Ordinal);
        if (match.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
            && Regex.IsMatch(remainder, @"\b(?:werkzeugset|toolset)\b|\b(?:[2-9]|[1-9]\d+)\s*(?:x\b(?!\s*\d)|(?:stuck|stueck|stk|pieces?|pcs|er\s*set)\b)", Options)) return null;
        if (title.Contains('+') || Regex.IsMatch(remainder, @"\b(?:und|and)\b", Options)) return null;
        if (match.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("expansion") == true
            && Regex.IsMatch(text, @"\bstarter(?:set|\s+set|\s+pack)\b", Options)) return null;
        // A listing of several numbered play sets is not the first single set's page.
        if (match.PossibleAttributes.ContainsKey("set_number")
            && Regex.Matches(text, @"\b\d{4,7}\b").Select(m => m.Value).Distinct().Count() > 1) return null;
        if (Matcher.Value.Match(remainder) is { } other && other.Id != match.Id) return null;
        // Sellers often write one manufacturer followed by several tool article numbers.
        if (match.PossibleAttributes.ContainsKey("article_number")
            && Matcher.Value.Match(match.Brand + " " + remainder) is { } article && article.Id != match.Id) return null;
        if (description != null && Regex.IsMatch(KnownProductMatcher.Normalize(description), @"\b(?:teile fehlen|missing parts|parts missing|set\s+(?:ist\s+)?nicht\s+(?:mehr\s+)?(?:ganz\s+)?vollstandig)\b", Options)) return null;
        return match;
    }
}
