using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>
/// Everything the text of a listing states about a part of any class: the class (decided from the title only), its primary key value when known, the numbers and names it measures,
/// its part numbers and what it fits. A class without an extractor (<see cref="PartClassDefinition.Implemented"/> false) gets its part numbers and fits only.
/// </summary>
public sealed class PartTextFacts
{
    public required PartClassDefinition Class { get; init; }
    public required string ClassEvidence { get; init; }
    /// <summary>Tooth count of a gear, sprocket or pulley; see <see cref="GearTextFacts.Teeth"/>.</summary>
    public TextValue<int>? Teeth { get; init; }
    public string? TeethNote { get; init; }
    /// <summary>Bearing designation ("6203-2RS") as the comparison key (<see cref="BearingTextExtractor.DesignationKey"/>).</summary>
    public TextName? Designation { get; init; }
    public TextValue<double>? Module { get; init; }
    public TextValue<double>? OuterDiameterMm { get; init; }
    public TextValue<double>? BoreMm { get; init; }
    public TextValue<double>? InnerDiameterMm { get; init; }
    public TextValue<double>? WidthMm { get; init; }
    public TextName? Material { get; init; }
    public IReadOnlyList<PartNumberMention> PartNumbers { get; init; } = [];
    public IReadOnlyList<FitMention> Fits { get; init; } = [];

    /// <summary>The numeric measures as (attribute key, value, unit).</summary>
    public IEnumerable<(string Key, TextValue<double> Value, string Unit)> Numbers()
    {
        if (Module != null) yield return (PartAttributeKeys.GearModule, Module, "module");
        if (OuterDiameterMm != null) yield return (PartAttributeKeys.OuterDiameterMm, OuterDiameterMm, "mm");
        if (BoreMm != null) yield return (PartAttributeKeys.BoreMm, BoreMm, "mm");
        if (InnerDiameterMm != null) yield return (PartAttributeKeys.InnerDiameterMm, InnerDiameterMm, "mm");
        if (WidthMm != null) yield return (PartAttributeKeys.WidthMm, WidthMm, "mm");
    }

    /// <summary>The named values as (attribute key, value).</summary>
    public IEnumerable<(string Key, TextName Value)> Names()
    {
        if (Material != null) yield return (PartAttributeKeys.Material, Material);
        if (Designation != null) yield return (PartAttributeKeys.BearingDesignation, Designation);
    }

    /// <summary>The first labelled part number, or null (a bare code never becomes the page's part number).</summary>
    public PartNumberMention? LabelledPartNumber => PartNumbers.FirstOrDefault(p => p.Labelled);
}

/// <summary>
/// Bearings: the designation ("6203-2RS", "608ZZ", "6002 2RS", "NU205") is the primary key, the inner x outer x width in millimetres ("17x40x12 mm") the measures. The dimensions of the
/// common 6xxx / 6xx series are known (<see cref="KnownDimensions"/>) and filled in when the text states only the designation.
/// </summary>
public static class BearingTextExtractor
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>A deep groove ball bearing designation: 6[0-9]{3} or 6[0-9]{2} with an optional seal suffix (-2RS, -ZZ, -2Z, RS, Z) and clearance (C3); also 3xxx angular contact and NU cylindrical roller.</summary>
    public static readonly Regex Designation = new(
        @"(?<![\p{L}\p{N}\-])(?<d>(?:6[0-9]{3}|6[0-9]{2}|3[02][0-9]{2}|nu\s?[0-9]{3,4}))\s?(?<s>-?\s?(?:2rs[r1]?|2z|zz|rs|z))?(?:\s?-?\s?(?<c>c[3-5]))?(?![\p{L}\p{N}])", Opts);
    private static readonly Regex Dimensions = new(@"(?<![\d.,])(?<a>\d{1,3}(?:[.,]\d)?)\s*(?:mm)?\s*x\s*(?<b>\d{1,3}(?:[.,]\d)?)\s*(?:mm)?\s*x\s*(?<c>\d{1,3}(?:[.,]\d)?)\s*mm(?![\p{L}])", Opts);

    /// <summary>Inner, outer, width in millimetres of the common designations.</summary>
    public static readonly IReadOnlyDictionary<string, (double Inner, double Outer, double Width)> KnownDimensions = new Dictionary<string, (double, double, double)>(StringComparer.Ordinal)
    {
        ["608"] = (8, 22, 7), ["625"] = (5, 16, 5), ["626"] = (6, 19, 6), ["627"] = (7, 22, 7), ["629"] = (9, 26, 8),
        ["6000"] = (10, 26, 8), ["6001"] = (12, 28, 8), ["6002"] = (15, 32, 9), ["6003"] = (17, 35, 10), ["6004"] = (20, 42, 12), ["6005"] = (25, 47, 12), ["6006"] = (30, 55, 13), ["6007"] = (35, 62, 14), ["6008"] = (40, 68, 15),
        ["6200"] = (10, 30, 9), ["6201"] = (12, 32, 10), ["6202"] = (15, 35, 11), ["6203"] = (17, 40, 12), ["6204"] = (20, 47, 14), ["6205"] = (25, 52, 15), ["6206"] = (30, 62, 16), ["6207"] = (35, 72, 17), ["6208"] = (40, 80, 18),
        ["6300"] = (10, 35, 11), ["6301"] = (12, 37, 12), ["6302"] = (15, 42, 13), ["6303"] = (17, 47, 14), ["6304"] = (20, 52, 15), ["6305"] = (25, 62, 17), ["6306"] = (30, 72, 19),
    };

    /// <summary>The comparison key of a designation: upper case, no spaces, one hyphen before the suffix ("6203 2RS" and "6203-2rs" are "6203-2RS"; the clearance class is dropped).</summary>
    public static string DesignationKey(Match m)
    {
        var number = m.Groups["d"].Value.Replace(" ", "").ToUpperInvariant();
        var suffix = m.Groups["s"].Success ? m.Groups["s"].Value.Replace("-", "").Replace(" ", "").ToUpperInvariant() : "";
        if (suffix == "ZZ") suffix = "2Z";
        return suffix.Length == 0 ? number : number + "-" + suffix;
    }

    public static PartTextFacts Extract(string? title, string? description, PartClassMatch cls)
    {
        var folded = (PartText.Fold(title) + " \n " + PartText.Fold(description)).Trim();
        TextName? designation = null;
        var m = Designation.Match(PartText.Fold(title));
        var fromTitle = m.Success;
        if (!m.Success) m = Designation.Match(folded);
        if (m.Success)
            designation = new TextName(DesignationKey(m), fromTitle ? 0.9 : 0.75, PartText.Evidence($"designation: \"{m.Value.Trim()}\"") ?? "");

        TextValue<double>? inner = null, outer = null, width = null;
        var dims = Dimensions.Match(folded);
        if (dims.Success && PartText.TryNum(dims.Groups["a"].Value, out var a) && PartText.TryNum(dims.Groups["b"].Value, out var b) && PartText.TryNum(dims.Groups["c"].Value, out var c) && a < b && a >= 1 && b <= 500)
        {
            var quote = PartText.Evidence($"dimensions: \"{dims.Value.Trim()}\"") ?? "";
            inner = new TextValue<double>(a, 0.85, quote);
            outer = new TextValue<double>(b, 0.85, quote);
            width = new TextValue<double>(c, 0.85, quote);
        }
        else if (designation != null && KnownDimensions.TryGetValue(designation.Value.Split('-')[0], out var known))
        {
            var quote = $"from designation {designation.Value}";
            inner = new TextValue<double>(known.Inner, 0.7, quote);
            outer = new TextValue<double>(known.Outer, 0.7, quote);
            width = new TextValue<double>(known.Width, 0.7, quote);
        }

        return new PartTextFacts
        {
            Class = cls.Class, ClassEvidence = cls.Evidence, Designation = designation, InnerDiameterMm = inner, OuterDiameterMm = outer, WidthMm = width,
            PartNumbers = PartNumbers.Find((title ?? "") + " \n " + (description ?? "")),
            Fits = Fitment.FindInListing(title, description),
        };
    }
}

/// <summary>
/// The one entry point of the text side: decides the class from the title (<see cref="PartClassRegistry.Classify"/>) and runs the class's extractor. Gear, sprocket and pulley share the
/// tooth count reading of <see cref="GearTextExtractor"/>; bearings have <see cref="BearingTextExtractor"/>; every other class, and the generic one, get part numbers and fits only.
/// To add a class with its own measures: a <see cref="PartClassDefinition"/> in the registry, an extractor, and a case here.
/// </summary>
public static class PartTextExtractors
{
    /// <summary>The facts of a listing, or null when the title names no part class (or an excluded context).</summary>
    public static PartTextFacts? Extract(string? title, string? description)
    {
        var cls = PartClassRegistry.Classify(title);
        if (cls == null)
            return null;
        switch (cls.Class.Name)
        {
            case PartClasses.Gear:
            case PartClasses.Sprocket:
            case PartClasses.Pulley:
                var g = GearTextExtractor.ExtractMeasures(title, description, cls.Evidence);
                return new PartTextFacts
                {
                    Class = cls.Class, ClassEvidence = cls.Evidence, Teeth = g.Teeth, TeethNote = g.TeethNote, Module = cls.Class.Name == PartClasses.Gear ? g.Module : null,
                    OuterDiameterMm = g.OuterDiameterMm, BoreMm = g.BoreMm, WidthMm = g.WidthMm, Material = g.Material, PartNumbers = g.PartNumbers, Fits = g.Fits,
                };
            case PartClasses.Bearing:
                return BearingTextExtractor.Extract(title, description, cls);
            default:
                // TODO belt (profile + length), seal, filter, brush, blade, nozzle: part numbers and fits only until an extractor exists
                var generic = new PartTextFacts
                {
                    Class = cls.Class, ClassEvidence = cls.Evidence,
                    PartNumbers = PartNumbers.Find((title ?? "") + " \n " + (description ?? "")),
                    Fits = Fitment.FindInListing(title, description),
                };
                // a generic spare part is only a part page when it states a labelled part number: "Ersatzteil" alone names nothing searchable
                if (cls.Class.Name == PartClasses.Generic && generic.LabelledPartNumber == null)
                    return null;
                return generic;
        }
    }

    /// <summary>The primary key value of the class from the facts, with its confidence; null when unknown. Gears, sprockets and pulleys: the tooth count (decided elsewhere, so passed in); bearings: the designation; the rest: the labelled part number key.</summary>
    public static (string Value, double Confidence)? PrimaryKey(PartTextFacts facts, int? decidedTeeth, double teethConfidence)
    {
        switch (facts.Class.PrimaryKey)
        {
            case PartAttributeKeys.GearTeeth:
                return decidedTeeth == null ? null : (decidedTeeth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), teethConfidence);
            case PartAttributeKeys.BearingDesignation:
                return facts.Designation == null ? null : (facts.Designation.Value, facts.Designation.Confidence);
            default:
                var pn = facts.LabelledPartNumber;
                return pn == null ? null : (pn.Key, pn.Confidence);
        }
    }
}
