using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>A value read from text with its confidence (0..1) and the quote it was read from.</summary>
public sealed record TextValue<T>(T Value, double Confidence, string Evidence) where T : struct;

/// <summary>A text value with a name (material, designation, profile).</summary>
public sealed record TextName(string Value, double Confidence, string Evidence);

/// <summary>
/// Everything the title and description of a listing say about a gear. Null means unknown: a value is never guessed. <see cref="TeethNote"/> explains an unknown tooth count that the text
/// did mention (several counts: a double gear or a set).
/// </summary>
public sealed class GearTextFacts
{
    public bool IsGear { get; init; }
    public string? GearEvidence { get; init; }
    public TextValue<int>? Teeth { get; init; }
    public string? TeethNote { get; init; }
    public TextValue<double>? Module { get; init; }
    public TextValue<double>? OuterDiameterMm { get; init; }
    public TextValue<double>? BoreMm { get; init; }
    public TextValue<double>? WidthMm { get; init; }
    public TextName? Material { get; init; }
    public IReadOnlyList<PartNumberMention> PartNumbers { get; init; } = [];
    public IReadOnlyList<FitMention> Fits { get; init; } = [];

    public static readonly GearTextFacts NotAGear = new();
}

/// <summary>
/// Reads the gear facts a seller states in a title and description. Whether the listing is a gear at all is decided from the title alone (<see cref="PartClassRegistry.Classify"/>:
/// \b-anchored gear nouns, an exclusion list for games, wearables, bikes, cameras and phones), never from the description. Then: the exact tooth count only next to a tooth word
/// ("42 Zähne", "Zähnezahl 42", "42 teeth", "40 tanden", "30 dents", "25 denti") or in the gear short forms "Z=42", "Z 42" and "42T" that are not part of a machine name ("Kawasaki Z125",
/// "Nikon Z50" stay model names); the module only as "Modul 1.5", "module 2", "Mod. 2", "m=1,5" (never "M8", a thread); outer diameter; the bore only next to a bore word (Bohrung,
/// bore, boring, alésage, foro, Innendurchmesser, ø innen; "Welle 150 mm lang" is no bore); width; every number only with its own unit ("3 cm x 5 mm" is 30 and 5); the material family;
/// part numbers (<see cref="PartNumbers"/>) and what it is for (<see cref="Fitment"/>, title and first sentence of the description only). Several different tooth counts in one text (a set,
/// a double gear "42/12 Zähne") give no tooth count and a note. Pure and side effect free.
/// </summary>
public static class GearTextExtractor
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Regex TeethWord = new(
        @"(?<![\d.,])(?<n>\d{1,3})\s*(?:zahne|zahnen|zahnig|zahn|teeth|tooth|dents|denti|dientes|tanden|zebow|zubu|fog)(?![\p{L}])", Opts);
    private static readonly Regex TeethLabel = new(
        @"(?<![\p{L}])(?:zahnezahl|zahnzahl|zahneanzahl|anzahl\s+(?:der\s+)?zahne|number\s+of\s+teeth|tooth\s+count|teeth|zahne|nombre\s+de\s+dents|numero\s+(?:di\s+)?denti)\s*[:=]\s*(?<n>\d{1,3})(?![\d])", Opts);
    private static readonly Regex TeethZ = new(@"(?<![\p{L}\p{N}\-])z\.?\s*[=:]?\s*(?<n>\d{1,3})(?![\p{N}\p{L}])", Opts);
    private static readonly Regex TeethSuffixZ = new(@"(?<![\p{L}\p{N}.,])(?<n>\d{1,3})\s*z(?![\p{L}\p{N}])", Opts);
    private static readonly Regex TeethSuffixT = new(@"(?<![\p{L}\p{N}.,])(?<n>\d{2,3})\s*t(?![\p{L}\p{N}])", Opts);
    private static readonly Regex SeveralTeeth = new(@"(?<![\d.,])(?<a>\d{1,3})\s*(?:/|x|,|\+|und|and|&|-)\s*(?<b>\d{1,3})\s*(?:zahne|teeth|tooth|z|t)(?![\p{L}\p{N}])", Opts);
    // the word before a "Z50": a brand whose model names start with Z
    private static readonly Regex ModelBrandBefore = new(@"(?:kawasaki|nikon|sony|xperia|samsung|galaxy|canon|nissan|datsun|zeiss|mz|bmw|audi|tesla|zoom|zotac)\s*$", Opts);

    private static readonly Regex ModuleWord = new(@"(?<![\p{L}])(?:modul|module|modulo|mod\.?)\s*[:=]?\s*(?<m>\d{1,2}(?:[.,]\d{1,3})?)(?![\d.,])|(?<![\p{L}\p{N}])m\s*=\s*(?<m>\d{1,2}(?:[.,]\d{1,3})?)(?![\d.,])", Opts);

    private const string Number = @"(?<v>\d{1,4}(?:[.,]\d{1,2})?)";
    /// <summary>A number with its own unit, optionally "x" a second number: "44 mm", "44 x 10 mm" (shared unit), "3 cm x 5 mm" (each its own).</summary>
    private const string NumberWithUnit = Number + @"\s*(?:(?<u1>mm|cm)\s*x\s*(?<w>\d{1,3}(?:[.,]\d{1,2})?)\s*(?<u>mm|cm)|x\s*(?<w>\d{1,3}(?:[.,]\d{1,2})?)\s*(?<u>mm|cm)|(?<u>mm|cm|millimeter|zentimeter))(?![\p{L}])";

    private static readonly Regex Bore = new(
        @"(?<![\p{L}])(?:bohrung(?:sdurchmesser)?|innendurchmesser|innen\s*-?\s*durchmesser|bore|boring|inner\s*diam\p{L}*|inside\s*diam\p{L}*|i\.d\.|alesage|foro|agujero|asgat|[oø⌀]\s*innen|innen\s*[oø⌀])" +
        @"\s*[:=]?\s*(?:ø|⌀|o|durchmesser|diameter|von|of|d)?\s*[:=]?\s*" + NumberWithUnit + "|" +
        NumberWithUnit + @"\s*(?:bohrung|bore|innen)(?![\p{L}])(?!\s*[:=]?\s*(?:ø|⌀)?\s*\d)",
        Opts);
    private static readonly Regex OuterExplicit = new(
        @"(?<![\p{L}])(?:aussendurchmesser|aussen\s*-?\s*durchmesser|kopfkreis(?:durchmesser)?|outer\s*diam\p{L}*|outside\s*diam\p{L}*|tip\s*diam\p{L}*|o\.d\.|od(?![\p{L}])|da\s*[:=]|d\s*a\s*[:=])" +
        @"\s*[:=]?\s*(?:ø|⌀|durchmesser|diameter)?\s*[:=]?\s*" + NumberWithUnit, Opts);
    private static readonly Regex OuterGeneric = new(
        @"(?:ø|⌀|(?<![\p{L}])(?:durchmesser|diameter|diametre|diametro|dia\.?|dm\.?|d\s*[:=]))\s*[:=]?\s*" + NumberWithUnit, Opts);
    private static readonly Regex Width = new(
        @"(?<![\p{L}])(?:breite|zahnbreite|dicke|starke|hohe|width|face\s*width|thickness|height|epaisseur|largeur|spessore|larghezza|dikte|breedte)\s*[:=]?\s*" + NumberWithUnit + "|" +
        NumberWithUnit + @"\s*(?:breit|dick|stark|hoch|wide|thick|high)(?![\p{L}])", Opts);

    private static readonly (Regex Pattern, string Material)[] Materials =
    {
        (new Regex(@"(?<![\p{L}])(?:messing|brass|laiton|ottone|laton)(?![\p{L}])", Opts), "brass"),
        (new Regex(@"(?<![\p{L}])(?:edelstahl|stahl|steel|acier|acciaio|acero|inox|gehartet|hardened|c45|16mncr5|42crmo4)(?![\p{L}])", Opts), "steel"),
        (new Regex(@"(?<![\p{L}])(?:kunststoff|plastik|plastic|plastique|plastica|plastico|pom|delrin|nylon|polyamid|pa6|pa66|pa12|peek|acetal|hostaform|resin|harz)(?![\p{L}])", Opts), "plastic"),
        (new Regex(@"(?<![\p{L}])(?:alu|aluminium|aluminum)(?![\p{L}])", Opts), "aluminium"),
        (new Regex(@"(?<![\p{L}])(?:bronze|bronce|rotguss|gunmetal)(?![\p{L}])", Opts), "bronze"),
        (new Regex(@"(?<![\p{L}])(?:grauguss|gusseisen|guss|cast\s*iron|ghisa|fonte)(?![\p{L}])", Opts), "cast iron"),
        (new Regex(@"(?<![\p{L}])(?:sinter\p{L}*|sintered|pulvermetall)(?![\p{L}])", Opts), "sintered metal"),
    };

    public const int MinTeeth = 5, MaxTeeth = 400;

    /// <summary>True when the title names a gear (not a watch, not a game, not a bike). Cheap pre-check for the pass over all pages. The description never takes part.</summary>
    public static bool IsGearText(string? title) => PartClassRegistry.Classify(title)?.Class.Name == PartClasses.Gear;

    public static GearTextFacts Extract(string? title, string? description)
    {
        var cls = PartClassRegistry.Classify(title);
        if (cls == null || cls.Class.Name != PartClasses.Gear)
            return GearTextFacts.NotAGear;
        return ExtractMeasures(title, description, cls.Evidence);
    }

    /// <summary>The gear measures of a text whose class was already decided (gear, pulley and sprocket share the tooth count reading).</summary>
    internal static GearTextFacts ExtractMeasures(string? title, string? description, string classEvidence)
    {
        var fits = Fitment.FindInListing(title, description);
        var foldedTitle = PartText.Fold(title);
        var foldedDescription = PartText.Fold(description);
        var folded = (foldedTitle + " \n " + foldedDescription).Trim();
        var fitSpans = FitSpans(folded, fits);

        var (teeth, teethNote) = ReadTeeth(folded, fitSpans);
        var bore = ReadMeasure(folded, Bore, 0.9, 1, 300, out var boreSpans);
        var outer = ReadOuter(folded, boreSpans, out var widthFromOuter);
        var width = ReadMeasure(folded, Width, 0.85, 0.5, 300, out _) ?? widthFromOuter;
        if (outer != null && bore != null && bore.Value >= outer.Value)
        {
            // a bore wider than the gear: one of the two is misread, neither is trusted
            outer = null;
            bore = null;
        }

        return new GearTextFacts
        {
            IsGear = true,
            GearEvidence = PartText.Evidence($"gear word \"{classEvidence}\""),
            Teeth = teeth,
            TeethNote = teethNote,
            Module = ReadModule(folded),
            OuterDiameterMm = outer,
            BoreMm = bore,
            WidthMm = width,
            Material = ReadMaterial(folded),
            PartNumbers = Parts.PartNumbers.Find((title ?? "") + " \n " + (description ?? "")),
            Fits = fits,
        };
    }

    /// <summary>Where the named machines stand in the folded text, so a "Z125" inside "für Kawasaki Z125" is never a tooth count.</summary>
    private static List<(int Index, int Length)> FitSpans(string folded, IReadOnlyList<FitMention> fits)
    {
        var spans = new List<(int, int)>();
        foreach (var fit in fits)
        {
            var needle = PartText.Fold(fit.MachineName);
            if (needle.Length == 0) continue;
            var at = 0;
            while ((at = folded.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                spans.Add((at, needle.Length));
                at += needle.Length;
            }
        }
        return spans;
    }

    private static (TextValue<int>? Teeth, string? Note) ReadTeeth(string folded, List<(int Index, int Length)> fitSpans)
    {
        var several = SeveralTeeth.Match(folded);
        if (several.Success)
            return (null, PartText.Evidence($"several tooth counts stated: \"{several.Value.Trim()}\""));

        bool InFit(Match m) => fitSpans.Any(s => m.Index < s.Index + s.Length && s.Index < m.Index + m.Length);
        bool AfterModelBrand(Match m) => ModelBrandBefore.IsMatch(folded[..m.Index]);

        var found = new List<(int N, double Conf, string Quote)>();
        void Collect(Regex regex, double confidence, bool shortForm)
        {
            foreach (Match m in regex.Matches(folded))
            {
                if (!int.TryParse(m.Groups["n"].Value, out var n) || n < MinTeeth || n > MaxTeeth)
                    continue;
                if (shortForm && (InFit(m) || AfterModelBrand(m)))
                    continue;
                found.Add((n, confidence, m.Value.Trim()));
            }
        }
        Collect(TeethLabel, 0.9, false);
        Collect(TeethWord, 0.9, false);
        if (found.Count == 0)
        {
            Collect(TeethZ, 0.8, true);
            Collect(TeethSuffixZ, 0.7, true);
            Collect(TeethSuffixT, 0.65, true);
        }
        if (found.Count == 0)
            return (null, null);
        var distinct = found.Select(f => f.N).Distinct().ToList();
        if (distinct.Count > 1)
            return (null, PartText.Evidence($"several tooth counts stated: {string.Join(", ", distinct)}"));
        var best = found.OrderByDescending(f => f.Conf).First();
        return (new TextValue<int>(best.N, best.Conf, PartText.Evidence($"teeth: \"{best.Quote}\"") ?? ""), null);
    }

    private static TextValue<double>? ReadModule(string folded)
    {
        var m = ModuleWord.Match(folded);
        if (!m.Success || !PartText.TryNum(m.Groups["m"].Value, out var value) || value < 0.3 || value > 10)
            return null;
        return new TextValue<double>(value, 0.9, PartText.Evidence($"module: \"{m.Value.Trim()}\"") ?? "");
    }

    internal static TextValue<double>? ReadMeasure(string folded, Regex regex, double confidence, double min, double max, out List<(int Index, int Length)> spans)
    {
        spans = new List<(int, int)>();
        TextValue<double>? result = null;
        foreach (Match m in regex.Matches(folded))
        {
            spans.Add((m.Index, m.Length));
            if (result != null)
                continue;
            if (!PartText.TryNum(m.Groups["v"].Value, out var value))
                continue;
            value = ToMm(value, m.Groups["u1"].Success ? m.Groups["u1"].Value : m.Groups["u"].Value);
            if (value < min || value > max)
                continue;
            result = new TextValue<double>(value, confidence, PartText.Evidence($"\"{m.Value.Trim()}\"") ?? "");
        }
        return result;
    }

    private static TextValue<double>? ReadOuter(string folded, List<(int Index, int Length)> boreSpans, out TextValue<double>? width)
    {
        width = null;
        foreach (var (regex, confidence) in new[] { (OuterExplicit, 0.9), (OuterGeneric, 0.7) })
        {
            foreach (Match m in regex.Matches(folded))
            {
                if (boreSpans.Any(s => m.Index < s.Index + s.Length && s.Index < m.Index + m.Length))
                    continue; // "Bohrung ø 8 mm" is the bore
                if (!PartText.TryNum(m.Groups["v"].Value, out var value))
                    continue;
                value = ToMm(value, m.Groups["u1"].Success ? m.Groups["u1"].Value : m.Groups["u"].Value);
                if (value < 3 || value > 1000)
                    continue;
                if (m.Groups["w"].Success && PartText.TryNum(m.Groups["w"].Value, out var w))
                {
                    w = ToMm(w, m.Groups["u"].Value);
                    if (w >= 0.5 && w <= 300)
                        width = new TextValue<double>(w, confidence - 0.2, PartText.Evidence($"width from \"{m.Value.Trim()}\"") ?? "");
                }
                return new TextValue<double>(value, confidence, PartText.Evidence($"outer diameter: \"{m.Value.Trim()}\"") ?? "");
            }
        }
        return null;
    }

    internal static double ToMm(double value, string unit) => unit.StartsWith('c') || unit.StartsWith("zent", StringComparison.Ordinal) ? Math.Round(value * 10, 2) : value;

    private static TextName? ReadMaterial(string folded)
    {
        var hits = new List<(int Index, string Material, string Quote)>();
        foreach (var (pattern, material) in Materials)
        {
            var m = pattern.Match(folded);
            if (m.Success)
                hits.Add((m.Index, material, m.Value));
        }
        if (hits.Count == 0 || hits.Select(h => h.Material).Distinct().Count() > 1)
            return null;
        var first = hits.OrderBy(h => h.Index).First();
        return new TextName(first.Material, 0.8, PartText.Evidence($"material: \"{first.Quote}\"") ?? "");
    }
}
