using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>The part classes the mechanical part index knows; see <see cref="PartClassRegistry"/> for what each one measures.</summary>
public static class PartClasses
{
    public const string Gear = "gear";
    public const string Bearing = "bearing";
    public const string Belt = "belt";
    public const string Pulley = "pulley";
    public const string Sprocket = "sprocket";
    public const string Seal = "seal";
    public const string Filter = "filter";
    public const string Brush = "brush";
    public const string Blade = "blade";
    public const string Nozzle = "nozzle";
    /// <summary>A spare part of no known class that states a labelled part number.</summary>
    public const string Generic = "generic";

    public static IReadOnlyList<string> All => PartClassRegistry.All.Select(c => c.Name).ToList();

    public static bool IsKnown(string? partClass) => partClass != null && PartClassRegistry.Find(partClass) != null;
}

/// <summary>
/// One part class: its name, the attribute key whose value is the partition of <c>parts_by_signature</c> (<see cref="PrimaryKey"/>), the attribute keys it may measure, the
/// \b-anchored nouns (folded, multilingual) that name it in a product name or listing title, and whether a text extractor exists for it (<see cref="Implemented"/>; a class without
/// one is a registry entry with a TODO: its pages are classified, get a part number when labelled, and nothing else).
/// </summary>
public sealed record PartClassDefinition(string Name, string PrimaryKey, IReadOnlyList<string> Keys, string Nouns, bool Implemented)
{
    private Regex? nounRegex;
    /// <summary>The nouns as a regex over folded text with word boundaries on both sides.</summary>
    public Regex NounRegex => nounRegex ??= new Regex(@"(?<![\p{L}\p{N}])(?:" + Nouns + @")(?![\p{L}\p{N}])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}

/// <summary>What the part class decision of a title found: the class and the noun it was decided on.</summary>
public sealed record PartClassMatch(PartClassDefinition Class, string Evidence);

/// <summary>
/// The registry of part classes. The class of a page is decided from the product name or listing title only, never from the description (a description that mentions
/// "Bluetooth", "transparent" or a gear icon must not make a phone a gear), with \b-anchored nouns and an exclusion list for the game, media, wearable, bike, camera and phone
/// contexts in which those nouns mean something else ("Metal Gear Solid", "Gears of War", "Top Gear", "Galaxy Gear", a mountain bike's gears, a "Nikon Z50"). Adding a class
/// is one entry here plus, when it should measure anything, an extractor registered in <see cref="PartTextExtractors"/>.
/// </summary>
public static class PartClassRegistry
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    public static readonly PartClassDefinition Gear = new(PartClasses.Gear, PartAttributeKeys.GearTeeth,
        [PartAttributeKeys.GearTeeth, PartAttributeKeys.GearModule, PartAttributeKeys.OuterDiameterMm, PartAttributeKeys.BoreMm, PartAttributeKeys.WidthMm, PartAttributeKeys.Material, PartAttributeKeys.PartNumber, PartAttributeKeys.BoreToOuterRatio],
        @"\p{L}*zahnrad(?:er|ern|chen|satz|satze|paar|s)?|ritzel|stirnrad(?:er)?|kegelrad(?:er)?|schneckenrad(?:er)?|schraubrad(?:er)?|planetenrad(?:er)?|sonnenrad|hohlrad|zahnkranz|zahnriemenrad(?:er)?|" +
        @"gear\s?wheels?|cog\s?wheels?|cogs?|pinions?|spur\s?gears?|bevel\s?gears?|worm\s?(?:gear|wheel)s?|helical\s?gears?|ring\s?gears?|planet(?:ary)?\s?gears?|crown\s?gears?|replacement\s?gears?|spare\s?gears?|gears?\s?(?:for|fur)|" +
        @"engrenages?|pignons?|roue\s?dentee|ingranagg(?:io|i)|pignone|engranajes?|pinon(?:es)?|tandwiel(?:en)?|rondsel|zebatk[ai]|ozuben[ea]\s?kol[oa]|fogaskerek|kugghjul",
        true);

    public static readonly PartClassDefinition Bearing = new(PartClasses.Bearing, PartAttributeKeys.BearingDesignation,
        [PartAttributeKeys.BearingDesignation, PartAttributeKeys.InnerDiameterMm, PartAttributeKeys.OuterDiameterMm, PartAttributeKeys.WidthMm, PartAttributeKeys.PartNumber],
        @"kugellager|rillenkugellager|walzlager|rollenlager|nadellager|kegelrollenlager|pendellager|lager(?:satz)?|ball\s?bearings?|roller\s?bearings?|bearings?|roulements?|cuscinett[oi]|rodamientos?|kogellager|lozysk[oa]",
        true);

    public static readonly PartClassDefinition Belt = new(PartClasses.Belt, PartAttributeKeys.BeltProfile,
        [PartAttributeKeys.BeltProfile, PartAttributeKeys.BeltLengthMm, PartAttributeKeys.WidthMm, PartAttributeKeys.PartNumber],
        @"keilriemen|zahnriemen|flachriemen|antriebsriemen|rippenriemen|riemen|v-?belts?|timing\s?belts?|drive\s?belts?|toothed\s?belts?|courroies?|cinghi[ae]|correas?|aandrijfriem|snaar", false);

    public static readonly PartClassDefinition Pulley = new(PartClasses.Pulley, PartAttributeKeys.GearTeeth,
        [PartAttributeKeys.GearTeeth, PartAttributeKeys.BeltProfile, PartAttributeKeys.BoreMm, PartAttributeKeys.OuterDiameterMm, PartAttributeKeys.PartNumber],
        @"riemenscheiben?|zahnriemenscheiben?|keilriemenscheiben?|pulleys?|timing\s?pulleys?|poulies?|puleggi[ae]|poleas?|riemschij(?:f|ven)", false);

    public static readonly PartClassDefinition Sprocket = new(PartClasses.Sprocket, PartAttributeKeys.GearTeeth,
        [PartAttributeKeys.GearTeeth, PartAttributeKeys.BoreMm, PartAttributeKeys.PartNumber],
        @"kettenrad(?:er)?|kettenritzel|sprockets?|chain\s?wheels?|pignons?\s?de\s?chaine|kettingwiel(?:en)?", false);

    public static readonly PartClassDefinition Seal = new(PartClasses.Seal, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.InnerDiameterMm, PartAttributeKeys.OuterDiameterMm, PartAttributeKeys.WidthMm, PartAttributeKeys.PartNumber],
        @"wellendichtring(?:e)?|simmerring(?:e)?|dichtring(?:e)?|o-?ring(?:e|s)?|oil\s?seals?|shaft\s?seals?|joints?\s?spi|paraoli[oa]|reten(?:es)?|keerring(?:en)?", false);

    public static readonly PartClassDefinition Filter = new(PartClasses.Filter, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.PartNumber],
        @"luftfilter|olfilter|kraftstofffilter|hepa-?filter|staubfilter|filter(?:einsatz|patrone)?|air\s?filters?|oil\s?filters?|fuel\s?filters?|filtres?|filtr[oi]", false);

    public static readonly PartClassDefinition Brush = new(PartClasses.Brush, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.PartNumber, PartAttributeKeys.WidthMm],
        @"kohleburste[n]?|kohlen|schleifkohle[n]?|carbon\s?brush(?:es)?|motor\s?brush(?:es)?|balais\s?(?:de\s?)?charbon|spazzol[ae]\s?(?:di\s?)?carbone|koolborstels?", false);

    public static readonly PartClassDefinition Blade = new(PartClasses.Blade, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.PartNumber, PartAttributeKeys.OuterDiameterMm, PartAttributeKeys.BoreMm],
        @"sageblatt(?:er)?|messer(?:satz)?|ersatzmesser|rasenmahermesser|klingen?|saw\s?blades?|mower\s?blades?|blades?|lames?\s?de\s?scie|lam[ae]\s?(?:per\s?)?sega|hojas?\s?de\s?sierra|zaagblad(?:en)?", false);

    public static readonly PartClassDefinition Nozzle = new(PartClasses.Nozzle, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.PartNumber, PartAttributeKeys.InnerDiameterMm],
        @"dusen?|einspritzdusen?|spruhdusen?|nozzles?|buses?\s?(?:d'injection)?|ugell[oi]|boquillas?|sproeiers?", false);

    public static readonly PartClassDefinition Generic = new(PartClasses.Generic, PartAttributeKeys.PartNumber,
        [PartAttributeKeys.PartNumber],
        @"ersatzteil(?:e)?|spare\s?parts?|replacement\s?parts?|pieces?\s?(?:detachees?|de\s?rechange)|ricambi[o]?|repuestos?|onderdeel|onderdelen", true);

    public static readonly IReadOnlyList<PartClassDefinition> All = [Gear, Bearing, Pulley, Sprocket, Belt, Seal, Filter, Brush, Blade, Nozzle, Generic];

    // contexts in which the part nouns do not name a part: games and media, wearables, bicycles, cameras, phones, vehicles with the noun in their model name
    private static readonly Regex Excluded = new(
        @"(?<![\p{L}\p{N}])(?:metal\s?gear|gear\s?solid|gears?\s?of\s?war|gears?\s?tactics|top\s?gear|galaxy\s?gear|samsung\s?gear|gear\s?(?:vr|fit|sport|live|iconx|360|up|s[234]|icon)|gaming\s?gear|" +
        @"head\s?gear|fishing\s?gear|camping\s?gear|outdoor\s?gear|protective\s?gear|riding\s?gear|landing\s?gear|gear\s?(?:shift|lever|knob|stick|cable|oil|box)|gearbox|getriebe\p{L}*|" +
        @"zahnradbahn|zahnradpumpe|zahnradmotor|" +
        @"mountain\s?bike|mountainbike|mtb|e-?bike|fahrrad\p{L}*|bicycle|bike|velo|rennrad|trekkingrad|kinderrad|schaltwerk|schalthebel|derailleur|shifter|shimano|sram|kassette|" +
        @"kamera|camera|canon|nikon|sony\s?alpha|fujifilm|olympus|panasonic\s?lumix|objektiv|lens|" +
        @"iphone|samsung\s?galaxy|galaxy\s?[as]\d+|xperia|huawei|xiaomi|smartphone|handy|phone|smartwatch|tablet|ipad|" +
        @"playstation|ps[2345]|xbox|nintendo|switch|dvd|blu-?ray|bluray|spiel|game|games|dvds?|cd|vinyl|lp|poster|t-?shirt|hoodie|pullover|jacke|hose|schuhe|" +
        @"motorrad|roller|auto|pkw|lkw|kawasaki|yamaha|honda|suzuki|bmw|audi|vw|mercedes|ford|opel|toyota|skoda)(?![\p{L}\p{N}])",
        Opts);

    public static PartClassDefinition? Find(string? name) => name == null ? null : All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// The class a product name or listing title names, or null. The first class (in registry order) whose noun occurs with word boundaries in the folded title wins; a title in an
    /// excluded context never gets a class, whatever else it says. <see cref="PartClasses.Generic"/> needs a part noun and is decided last.
    /// </summary>
    public static PartClassMatch? Classify(string? title)
    {
        var folded = PartText.Fold(title);
        if (folded.Length == 0 || Excluded.IsMatch(folded))
            return null;
        foreach (var cls in All)
        {
            var m = cls.NounRegex.Match(folded);
            if (m.Success)
                return new PartClassMatch(cls, m.Value.Trim());
        }
        return null;
    }
}

/// <summary>
/// The attribute keys of the index. Only <see cref="Searchable"/> (<c>part_class</c>, <c>part_key</c>, <c>part_number</c>) are written to the product and so to the search index;
/// every measurement lives in <c>part_measurements</c> only. Values are plain invariant numbers without a unit; the unit is in the key. An unknown value is a missing key, never "unknown".
/// </summary>
public static class PartAttributeKeys
{
    /// <summary>One of <see cref="PartClasses"/>.</summary>
    public const string PartClass = "part_class";
    /// <summary>The class and primary key value of the page, e.g. <c>gear:24</c>, <c>bearing:6203</c>; absent when the primary key is unknown.</summary>
    public const string PartKey = "part_key";
    /// <summary>The first labelled manufacturer part number as written.</summary>
    public const string PartNumber = "part_number";

    /// <summary>The exact tooth count of a gear, sprocket or pulley; written only when decided (see <see cref="GearToothVerdict"/> and <see cref="PartVote"/>).</summary>
    public const string GearTeeth = "gear_teeth";
    /// <summary>Where the tooth count came from: <see cref="PartValueSources"/>.</summary>
    public const string GearTeethSource = "gear_teeth_source";
    /// <summary>Gear module (mm per tooth of pitch diameter), e.g. 1.5.</summary>
    public const string GearModule = "gear_module";
    /// <summary>Outer (tip) diameter in millimetres.</summary>
    public const string OuterDiameterMm = "part_outer_diameter_mm";
    /// <summary>Bore (shaft hole) diameter in millimetres.</summary>
    public const string BoreMm = "part_bore_mm";
    /// <summary>Inner diameter of a bearing or seal in millimetres.</summary>
    public const string InnerDiameterMm = "part_inner_mm";
    /// <summary>Width (face width / thickness) in millimetres.</summary>
    public const string WidthMm = "part_width_mm";
    /// <summary>Material family: brass, steel, plastic, aluminium, bronze, cast iron, sintered metal.</summary>
    public const string Material = "part_material";
    /// <summary>The machines the part is listed for, "; " separated display names. Measurement only, never a product attribute.</summary>
    public const string FitsFor = "part_fits";
    /// <summary>Bore to outer diameter ratio measured in the photo (scale free, 0..1); only from images.</summary>
    public const string BoreToOuterRatio = "part_bore_ratio";
    /// <summary>Bearing designation, e.g. 6203-2RS.</summary>
    public const string BearingDesignation = "bearing_designation";
    /// <summary>Belt profile (A, B, SPZ, HTD 5M, GT2, ...).</summary>
    public const string BeltProfile = "belt_profile";
    /// <summary>Belt length in millimetres.</summary>
    public const string BeltLengthMm = "belt_length_mm";

    /// <summary>The keys that are written to the product (the search index): at most three attribute pairs per part product.</summary>
    public static readonly IReadOnlyList<string> Searchable = [PartClass, PartKey, PartNumber];

    /// <summary>Every key the index may write to <c>part_measurements</c> or remove from a product.</summary>
    public static readonly IReadOnlyList<string> All =
        [PartClass, PartKey, PartNumber, GearTeeth, GearTeethSource, GearModule, OuterDiameterMm, BoreMm, InnerDiameterMm, WidthMm, Material, FitsFor, BoreToOuterRatio, BearingDesignation, BeltProfile, BeltLengthMm];

    /// <summary>True for a key the index owns (the search gate of AneApi skips these attribute values).</summary>
    public static bool IsIndexKey(string? key) => key != null && (key.StartsWith("part_", StringComparison.Ordinal) || key.StartsWith("gear_", StringComparison.Ordinal) || key.StartsWith("bearing_", StringComparison.Ordinal) || key.StartsWith("belt_", StringComparison.Ordinal));

    /// <summary>The <c>part_key</c> value of a class and a primary key value.</summary>
    public static string Key(string partClass, string primaryValue) => partClass + ":" + primaryValue;
}

/// <summary>Where a measured value came from.</summary>
public static class PartValueSources
{
    /// <summary>Stated in the title or description of a listing.</summary>
    public const string Text = "text";
    /// <summary>Decided from a photo of the listing (verified count, enforce mode).</summary>
    public const string Image = "image";
    /// <summary>The raw photo measurement as the image service returned it, stored for review in shadow and enforce mode; never votes.</summary>
    public const string Photo = "photo";
    /// <summary>Text and photo agree.</summary>
    public const string TextAndImage = "text+image";
    /// <summary>Derived from another value (a bore computed from a stated outer diameter and the photo's bore ratio).</summary>
    public const string Derived = "derived";
    /// <summary>The product page the listing is attached to (its model names the device the part is for).</summary>
    public const string Product = "product";
    /// <summary>The value the page shows after the vote over its listings (<see cref="PartVote"/>).</summary>
    public const string Vote = "vote";
    /// <summary>Not a value: the stored reason why a value is unknown (conflicting statements, a refused photo count), for a reviewer. Its <see cref="PartMeasurement.Value"/> is <c>unknown</c>.</summary>
    public const string Review = "review";
}

/// <summary>
/// One measured or stated value of a part page: row of <c>part_measurements</c>, partition <c>seo_id</c>, clustering <c>(attribute_key, source, listing_key)</c>, so every listing's own
/// reading of an attribute is kept (the vote over a page's listings reads them), and the text and the photo value of one attribute stand side by side for a reviewer.
/// <see cref="ListingKey"/> is <c>platform:id</c> of the listing, the empty string for the backfill over a page and for the page level vote and review rows. <see cref="Confidence"/> is
/// 0..1; <see cref="Evidence"/> one short sanitized line (never seller contact data). An unknown value has no row. Rows expire with the page (TTL, refreshed on every write).
/// </summary>
public class PartMeasurement
{
    public string SeoId { get; set; } = "";
    public string AttributeKey { get; set; } = "";
    /// <summary>One of <see cref="PartValueSources"/>.</summary>
    public string Source { get; set; } = "";
    public string ListingKey { get; set; } = "";
    public string Value { get; set; } = "";
    public double? Numeric { get; set; }
    public string? Unit { get; set; }
    public double Confidence { get; set; }
    public string? Evidence { get; set; }
    /// <summary>The marketplace of the listing the value was read from (<see cref="Platform"/> as int) and its id; 0 and null for the backfill over a page.</summary>
    public int ListingPlatform { get; set; }
    public string? ListingId { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static string KeyOf(int platform, string? listingId) => listingId == null ? "" : platform + ":" + listingId;
}

/// <summary>
/// The searchable signature of a part page: row of <c>parts_by_signature</c>, partition <c>(part_class, part_key)</c>, clustering <c>seo_id</c>. The partition is the class's primary
/// key value (a gear's tooth count, a bearing's designation, a generic part's part number key), so a replacement part search reads one partition per asked value and filters the
/// approximate measures in memory (<see cref="PartSignatureMatch"/>). Pages whose primary key is unknown sit in the partition <see cref="UnknownKey"/> and are found by part number or
/// machine only. Null measures are unknown.
/// </summary>
public class PartSignatureRow
{
    public const string UnknownKey = "?";

    public string PartClass { get; set; } = "";
    public string PartKey { get; set; } = UnknownKey;
    public string SeoId { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Tooth count (gear, sprocket, pulley), null when unknown.</summary>
    public int? Teeth { get; set; }
    public double? OuterDiameterMm { get; set; }
    public double? BoreMm { get; set; }
    public double? InnerDiameterMm { get; set; }
    public double? WidthMm { get; set; }
    public double? Module { get; set; }
    public double? LengthMm { get; set; }
    public string? Profile { get; set; }
    public double? BoreToOuterRatio { get; set; }
    public string? Material { get; set; }
    /// <summary>The normalized key of the first part number (<see cref="PartNumbers.Key"/>), null when none is known.</summary>
    public string? PartNumberKey { get; set; }
    public string? PartNumber { get; set; }
    /// <summary>True when the first part number was read from an explicit label ("Teile-Nr. ...") rather than a bare code.</summary>
    public bool PartNumberLabelled { get; set; }
    /// <summary>Machine keys (<see cref="Fitment.MachineKey"/>) the part is listed for.</summary>
    public List<string>? Fits { get; set; }
    /// <summary>Confidence of the primary key value (0 when unknown).</summary>
    public double KeyConfidence { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool HasKey => PartKey != UnknownKey && PartKey.Length > 0;
}

/// <summary>
/// What the index last wrote for a page: row of <c>part_pages</c>, partition <c>seo_id</c>. <see cref="Signature"/> is the hash of everything the last indexing produced; an indexing whose
/// hash is unchanged writes nothing (unless the row is older than the refresh interval, to renew the TTL). The lists say which rows of the other tables belong to the page, so stale
/// rows (a part number no longer stated, a machine no longer named, an old signature partition, old edges) are deleted when the page changes or stops being a part.
/// </summary>
public class PartPageState
{
    public string SeoId { get; set; } = "";
    public string PartClass { get; set; } = "";
    public string PartKey { get; set; } = PartSignatureRow.UnknownKey;
    public string Signature { get; set; } = "";
    public List<string>? PartNumberKeys { get; set; }
    public List<string>? FitKeys { get; set; }
    public List<string>? EdgeIds { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Row of <c>part_numbers</c>: partition the normalized part number, clustering the page, so every page that states one part number is one partition (the same-part evidence of the highest kind).</summary>
public class PartNumberRow
{
    public string PartNumberKey { get; set; } = "";
    public string SeoId { get; set; } = "";
    public string PartClass { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public double Confidence { get; set; }
    /// <summary>Read from an explicit label (a fact) rather than a bare code (a hint).</summary>
    public bool Labelled { get; set; }
    /// <summary>The first (primary) part number of the page; a secondary one only makes candidate edges.</summary>
    public bool Primary { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Row of <c>part_fits</c> (partition <c>machine_key</c>, clustering <c>seo_id</c>: the parts listed for one machine) and of its reverse <c>part_fits_by_part</c> (partition <c>seo_id</c>, clustering <c>machine_key</c>).
/// <see cref="MachineKey"/> is <see cref="Fitment.MachineKey"/> of the name; <see cref="MachineName"/> the name as a seller wrote it, for display.
/// </summary>
public class PartFitRow
{
    public string MachineKey { get; set; } = "";
    public string SeoId { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string PartClass { get; set; } = "";
    public string PartName { get; set; } = "";
    /// <summary>One of <see cref="PartValueSources"/>: text (a "für ..." phrase) or product (the page's own model names the device).</summary>
    public string Source { get; set; } = "";
    public double Confidence { get; set; }
    public string? Evidence { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Kinds and statuses of a same-part edge.</summary>
public static class PartEdgeKinds
{
    /// <summary>Both pages state the same manufacturer part number.</summary>
    public const string PartNumber = "part_number";
    /// <summary>The measured signatures agree.</summary>
    public const string Signature = "signature";

    /// <summary>Shown as "may be the same part", never as a fact.</summary>
    public const string Candidate = "candidate";
    /// <summary>A labelled part number shared by both pages, an identical (teeth, module, bore) triple, or a candidate a reviewer confirmed.</summary>
    public const string Confirmed = "confirmed";
}

/// <summary>
/// One edge of the same-part graph: row of <c>part_same_edges</c>, partition <c>seo_id</c>, clustering <c>other_seo_id</c>; every edge is written in both directions so the neighbours of a page are one partition.
/// <see cref="Kind"/> and <see cref="Status"/> are <see cref="PartEdgeKinds"/>; <see cref="Confidence"/> 0..1 and <see cref="Evidence"/> say why (<see cref="SamePartRules"/>).
/// </summary>
public class PartSameEdge
{
    public string SeoId { get; set; } = "";
    public string OtherSeoId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "";
    public double Confidence { get; set; }
    public string? Evidence { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>The same edge seen from the other page.</summary>
    public PartSameEdge Reverse() => new()
    {
        SeoId = OtherSeoId, OtherSeoId = SeoId, Kind = Kind, Status = Status, Confidence = Confidence, Evidence = Evidence, UpdatedAt = UpdatedAt,
    };
}
