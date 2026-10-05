using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>A machine, device or model a part is said to be for: the name as written, its key and how sure the reading is.</summary>
public sealed record FitMention(string MachineName, double Confidence, string Evidence)
{
    public string MachineKey => Fitment.MachineKey(MachineName);
}

/// <summary>
/// What a part is for, read from "für ...", "passend für ...", "for ...", "fits ...", "kompatibel mit ...", "pour ...", "per ...", "para ...", "voor ..." phrases of a title and of the
/// first sentence of a description (<see cref="PartText.FirstSentence"/>; the rest of a description is the seller's terms: "für 5 Euro abzugeben", "nur für Selbstabholer", "per PayPal"),
/// and from the product page the part sits on (an accessory page's model is "Spare part for &lt;device&gt;"). The name after the phrase word must begin with a brand or model token: a
/// capitalised word or a token with a digit in the original text, never a money, shipping, pickup or payment word. It is cut at punctuation, at a measure ("42 Zähne", "35 mm") and at
/// joining words, must contain a letter, and must not itself be a part word. <see cref="MachineKey"/> is the comparison form (folded, letters, digits and single spaces).
/// </summary>
public static class Fitment
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private const string Name =
        @"(?<m>[\p{L}\p{N}][\p{L}\p{N}\-./+ ]{1,50}?)" +
        @"(?=\s*(?:[,;:()\[\]!?|/&]|$|\s+(?:und|and|mit|with|oder|or|inkl|inklusive|aus|von|neu|new|gebraucht|used|defekt|top|ca|zähne|zahne|zahn|teeth|tooth|zahnrad|gear|ritzel|pinion|ersatzteil|spare|mm|cm|modul|module|z\d|\d+\s*(?:zähne|zahne|teeth|mm|t\b|z\b))(?![\p{L}])))";

    private static readonly Regex Phrase = new(
        @"(?<![\p{L}\p{N}])(?:passend\s+(?:für|fur|zu|auf)|geeignet\s+(?:für|fur)|für|fur|for|fits|kompatibel\s+(?:mit|zu)|compatible\s+(?:with|avec)|pour|per|para|voor|adatto\s+(?:a|per))\s+" + Name,
        Opts);

    /// <summary>"für Bosch GSR und Makita DHP": a second machine after a joining word, continued right where the previous name ended.</summary>
    private static readonly Regex Continued = new(@"\G\s*(?:,|/|&|und|and|oder|or|\+)\s+" + Name, Opts);

    private static readonly Regex LeadingArticle = new(@"^(?:den|die|das|der|dem|des|the|a|an|ein|eine|einen|einem|einer|meine?|my|alle|all|einige|some|diverse|verschiedene|various)\s+", Opts);

    // part words that are no machine ("Zahnrad für Zahnrad"), generic words that name no machine, and the seller's terms words
    private static readonly Regex NotAMachine = new(
        @"^(?:zahnrad\p{L}*|ritzel|gear\p{L}*|pinion|sprocket|stirnrad\p{L}*|kegelrad\p{L}*|schneckenrad\p{L}*|ersatzteil\p{L}*|spare\s*parts?|parts?|teile?|reparatur|repair|bastler|sammler|" +
        @"alle|all|viele|many|diverse|verschiedene|various|modellbau|model\s*making|hobby|antrieb|motor|getriebe|gearbox|transmission|maschine|machine|bohrmaschine|" +
        @"den|die|das|der|dem|the|a|an|ein|eine|einen|einem|einer|mich|dich|sie|ihn|es|you|me|him|her|them|it|sale|verkauf|" +
        @"selbstabholer|abholer|abholung|versand|post|paypal|überweisung|uberweisung|nachnahme|kleinanzeigen|ebay|vinted|kinder|erwachsene|anfänger|anfanger|profis?|" +
        @"sofort|schnell|wenig|kleines|kleine|grosse|große|jeden|jede|jedes|immer|hier|dort)$",
        Opts);

    // a name that is money, a condition or the seller's terms
    private static readonly Regex NotANameAnywhere = new(@"(?:\d\s*(?:€|eur|euro|chf|\$)|\b(?:vb|vhb|fp|festpreis|paypal|versand|abhol\p{L}*|selbstabhol\p{L}*|nachnahme|überweisung|uberweisung|rechnung|gratis|kostenlos)\b)", Opts);

    /// <summary>Brands a lower case title may name after "für" (folded).</summary>
    public static readonly HashSet<string> KnownBrands = new(StringComparer.Ordinal)
    {
        "bosch", "makita", "dewalt", "metabo", "hilti", "festool", "einhell", "ryobi", "milwaukee", "hitachi", "hikoki", "flex", "fein", "dremel", "proxxon", "black+decker", "parkside", "worx", "scheppach", "guede", "gude",
        "kitchenaid", "kenwood", "thermomix", "vorwerk", "braun", "philips", "krups", "delonghi", "jura", "saeco", "siemens", "miele", "aeg", "bauknecht", "whirlpool", "dyson", "karcher", "kaercher", "nilfisk",
        "stihl", "husqvarna", "dolmar", "gardena", "wolf", "honda", "briggs", "al-ko", "alko", "viking",
        "lego", "marklin", "maerklin", "fleischmann", "roco", "piko", "tamiya", "carrera", "lgb", "trix", "faller", "playmobil",
        "brother", "singer", "pfaff", "bernina", "husqvarna", "janome", "juki",
        "bmw", "vw", "volkswagen", "audi", "mercedes", "opel", "ford", "fiat", "renault", "peugeot", "skoda", "seat", "toyota", "nissan", "mazda", "volvo",
        "kawasaki", "yamaha", "suzuki", "ktm", "vespa", "piaggio", "simson", "mz", "zundapp", "kreidler", "hercules", "puch", "ducati", "aprilia", "harley",
    };

    private static readonly Regex TrailingNoise =new(@"\s+(?:zahnrad\p{L}*|ritzel|gear\p{L}*|ersatzteil\p{L}*|spare|parts?|teile?)$", Opts);
    private static readonly Regex NonKey = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The comparison key of a machine name: folded, letters and digits, single spaces ("Bosch GSR 12V-15" and "bosch gsr 12 v 15" differ; "BOSCH GSR 12V-15" and "bosch gsr 12v-15" do not).</summary>
    public static string MachineKey(string name) => NonKey.Replace(PartText.Fold(name), " ").Trim();

    /// <summary>
    /// The machines named in <paramref name="text"/> (a title, or the first sentence of a description: callers pass <see cref="PartText.FirstSentence"/>), in text order, without duplicates by key;
    /// <paramref name="confidence"/> is what the source is worth (a title more than a description).
    /// </summary>
    public static IReadOnlyList<FitMention> Find(string? text, double confidence, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var result = new List<FitMention>();
        foreach (Match phrase in Phrase.Matches(text))
        {
            var m = phrase;
            var first = true;
            while (m.Success && result.Count < max)
            {
                var name = Clean(m.Groups["m"].Value, requireBrandToken: true);
                if (name == null)
                    break;
                var key = MachineKey(name);
                if (result.All(r => r.MachineKey != key))
                    result.Add(new FitMention(name, first ? confidence : confidence - 0.05, PartText.Evidence($"fits: \"{m.Value.Trim()}\"") ?? ""));
                first = false;
                m = Continued.Match(text, m.Index + m.Length);
            }
            if (result.Count >= max)
                break;
        }
        return result;
    }

    /// <summary>The machines named in a title and in the first sentence of a description, title first, without duplicates.</summary>
    public static IReadOnlyList<FitMention> FindInListing(string? title, string? description, int max = 5) =>
        Find(title, 0.75, max).Concat(Find(PartText.FirstSentence(description), 0.55, max)).GroupBy(f => f.MachineKey).Select(g => g.First()).Take(max).ToList();

    /// <summary>The device of an accessory page's model ("Spare part for iPhone 11", "Gear for Bosch GSR 12V") or null.</summary>
    public static FitMention? FromProductModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;
        var m = Regex.Match(model, @"\sfor\s+(?<m>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;
        var name = Clean(m.Groups["m"].Value, requireBrandToken: false);
        return name == null ? null : new FitMention(name, 0.6, PartText.Evidence($"fits: product page \"{model.Trim()}\"") ?? "");
    }

    private static string? Clean(string raw, bool requireBrandToken)
    {
        var name = TrailingNoise.Replace(LeadingArticle.Replace(raw.Trim().Trim('-', '.', '/', '+'), ""), "").Trim();
        if (name.Length < 2 || !name.Any(char.IsLetter) || NotAMachine.IsMatch(name) || NotANameAnywhere.IsMatch(name))
            return null;
        if (MachineKey(name).Length < 2)
            return null;
        if (requireBrandToken)
        {
            var firstToken = name.Split(' ', 2)[0];
            if (NotAMachine.IsMatch(firstToken))
                return null;
            // a brand or model: capitalised in the original, a model token with a digit, or a known brand (titles are often written in lower case)
            if (!char.IsUpper(firstToken[0]) && !firstToken.Any(char.IsDigit) && !KnownBrands.Contains(PartText.Fold(firstToken)))
                return null;
        }
        return name;
    }
}
