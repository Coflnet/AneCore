using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.Phones;

public enum PhoneSpecConfidence
{
    Medium,
    High,
}

/// <summary>One row of the catalogue (<c>Phones/PhoneSpecs.tsv</c>): a phone, or one generation of a model that exists in several. Empty cells are null (unknown).</summary>
public sealed record PhoneSpecRow(
    string Brand,
    string Model,
    IReadOnlyList<string> Aliases,
    int? ReleaseYear,
    string? Os,
    int? OsVersionLaunch,
    int? OsVersionMax,
    double? ScreenInches,
    PhoneSpecConfidence Confidence);

/// <summary>
/// What the catalogue knows about a (brand, model). When the model matches several rows (a model written without its year, or a code several phones share) only the cells all rows agree on are set,
/// so an ambiguous lookup never claims a version or a size that belongs to one generation only. Null cells are unknown.
/// </summary>
public sealed record PhoneSpec(
    string? Os,
    int? OsVersionLaunch,
    int? OsVersionMax,
    int? ReleaseYear,
    double? ScreenInches,
    PhoneSpecConfidence Confidence,
    int Matches)
{
    public bool IsAmbiguous => Matches > 1;

    /// <summary>The screen size in the format of the <c>screen_size</c> attribute (<see cref="PhoneSpecCatalog.FormatScreenSize"/>), null when unknown.</summary>
    public string? ScreenSizeAttribute => ScreenInches is { } inches ? PhoneSpecCatalog.FormatScreenSize(inches) : null;
}

/// <summary>
/// Hand-curated phone specs (operating system family and versions, release year, screen size) keyed by brand and model the way <c>ElectronicsExtractor</c> names them,
/// for the attributes <c>os</c>, <c>os_version</c>, <c>os_version_launch</c>, <c>release_year</c> and <c>screen_size</c> of phone pages. The data is the embedded <c>Phones/PhoneSpecs.tsv</c>.
/// </summary>
public sealed class PhoneSpecCatalog
{
    private const string ResourceName = "Coflnet.Ane.Phones.PhoneSpecs.tsv";

    private static readonly Lazy<PhoneSpecCatalog> SharedCatalog = new(LoadEmbedded);

    /// <summary>The catalogue shipped with the library, loaded once.</summary>
    public static PhoneSpecCatalog Shared => SharedCatalog.Value;

    /// <summary>Brand spellings that name another brand's rows: Sony Ericsson phones are extracted as brand Sony, Redmi and Poco are Xiaomi lines.</summary>
    private static readonly Dictionary<string, string> BrandAliases = new(StringComparer.Ordinal)
    {
        ["sonyericsson"] = "sony",
        ["redmi"] = "xiaomi",
        ["poco"] = "xiaomi",
    };

    private static readonly HashSet<string> LineBrands = new(StringComparer.Ordinal) { "redmi", "poco" };

    private static readonly Regex TrailingYear = new(@"(?:\s|\()(?<year>199[5-9]|20[0-2]\d|2030)\)?\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<(string Brand, string Model), List<PhoneSpecRow>> index = new();

    public IReadOnlyList<PhoneSpecRow> Rows { get; }

    public PhoneSpecCatalog(IEnumerable<PhoneSpecRow> rows)
    {
        Rows = rows.ToList();
        foreach (var row in Rows)
        {
            var brand = BrandKey(row.Brand);
            foreach (var name in new[] { row.Model }.Concat(row.Aliases))
            {
                var key = (brand, ModelKey(row.Brand, name, out _));
                if (!index.TryGetValue(key, out var list))
                    index[key] = list = new List<PhoneSpecRow>();
                if (!list.Contains(row))
                    list.Add(row);
            }
        }
    }

    /// <summary>Parses the TSV (lines starting with # are comments, the first other line is the column header).</summary>
    public static PhoneSpecCatalog Parse(string tsv)
    {
        var rows = new List<PhoneSpecRow>();
        var headerSeen = false;
        foreach (var raw in tsv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }
            var cells = line.Split('\t');
            if (cells.Length != 9)
                throw new FormatException($"Phone spec row needs 9 columns but has {cells.Length}: {line}");
            rows.Add(new PhoneSpecRow(
                cells[0].Trim(),
                cells[1].Trim(),
                cells[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                ParseInt(cells[3]),
                Blank(cells[4]),
                ParseInt(cells[5]),
                ParseInt(cells[6]),
                double.TryParse(cells[7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var inches) ? inches : null,
                cells[8].Trim().ToLowerInvariant() switch
                {
                    "high" => PhoneSpecConfidence.High,
                    "medium" => PhoneSpecConfidence.Medium,
                    var other => throw new FormatException($"Unknown phone spec confidence '{other}': {line}"),
                }));
        }
        return new PhoneSpecCatalog(rows);
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static PhoneSpecCatalog LoadEmbedded()
    {
        using var stream = typeof(PhoneSpecCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Lowercase letters and digits of the brand, with <see cref="BrandAliases"/> applied.</summary>
    public static string BrandKey(string? brand)
    {
        var key = AlphaNumeric(brand);
        return BrandAliases.TryGetValue(key, out var canonical) ? canonical : key;
    }

    /// <summary>
    /// The key a model is looked up by: lowercase letters and digits only ("Galaxy S21 Fe" = "Galaxy S21 FE" = "galaxys21fe"), "+" read as "plus" ("S10+" = "S10 Plus"), without a leading brand word
    /// ("Xiaomi 13 Lite" = "13 Lite", "Huawei Mate20 Pro" = "Mate 20 Pro") and without a trailing release year ("iPhone SE 2020", "Galaxy A5 (2017)"), which is returned in <paramref name="year"/>.
    /// </summary>
    public static string ModelKey(string? brand, string? model, out int? year)
    {
        year = null;
        var text = (model ?? "").Trim().Replace("+", " plus ");
        var match = TrailingYear.Match(text);
        if (match.Success)
        {
            year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            text = text[..match.Index];
        }
        var key = AlphaNumeric(text);
        var rawBrand = AlphaNumeric(brand);
        // longest brand word first; Redmi and Poco are product lines inside the model name ("Redmi 12" is not "Xiaomi 12"), never a prefix to drop
        var prefixes = new[] { LineBrands.Contains(rawBrand) ? "" : rawBrand, BrandKey(brand) }.Where(p => p.Length > 0).Distinct().OrderByDescending(p => p.Length);
        foreach (var prefix in prefixes)
        {
            if (key.Length > prefix.Length && key.StartsWith(prefix, StringComparison.Ordinal))
            {
                key = key[prefix.Length..];
                break;
            }
        }
        return key;
    }

    private static string AlphaNumeric(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    /// <summary>The attribute format of a screen size: 6.1 becomes <c>6.1"</c> and 5.0 becomes <c>5"</c>, the same text <c>Product.NormalizeAttributeClassification</c> gives "6.1 Zoll".</summary>
    public static string FormatScreenSize(double inches) =>
        (Math.Abs(inches - Math.Round(inches)) < 0.001
            ? ((long)Math.Round(inches)).ToString(CultureInfo.InvariantCulture)
            : inches.ToString("0.#", CultureInfo.InvariantCulture)) + "\"";

    /// <summary>Looks a phone up by brand and model; <paramref name="releaseYear"/> (from a title or an attribute) picks one generation when the model has several.</summary>
    public bool TryGet(string? brand, string? model, out PhoneSpec spec) => TryGet(brand, model, null, out spec);

    public bool TryGet(string? brand, string? model, int? releaseYear, out PhoneSpec spec)
    {
        spec = null!;
        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            return false;
        var brandKey = BrandKey(brand);
        var modelKey = ModelKey(brand, model, out var modelYear);
        if (modelKey.Length == 0)
            return false;

        if (!index.TryGetValue((brandKey, modelKey), out var candidates)
            && !(modelKey.Length > 2 && (modelKey.EndsWith("5g", StringComparison.Ordinal) || modelKey.EndsWith("4g", StringComparison.Ordinal))
                 && index.TryGetValue((brandKey, modelKey[..^2]), out candidates)))
            return false;

        var year = modelYear ?? releaseYear;
        if (year != null && candidates.Where(row => row.ReleaseYear == year).ToList() is { Count: > 0 } narrowed)
            candidates = narrowed;

        spec = Merge(candidates);
        return true;
    }

    private static PhoneSpec Merge(IReadOnlyList<PhoneSpecRow> rows)
    {
        static T? Agreed<T>(IReadOnlyList<PhoneSpecRow> all, Func<PhoneSpecRow, T?> pick) where T : struct, IEquatable<T>
        {
            var first = pick(all[0]);
            return first != null && all.All(row => pick(row) is { } value && value.Equals(first.Value)) ? first : null;
        }

        var os = rows[0].Os != null && rows.All(row => string.Equals(row.Os, rows[0].Os, StringComparison.Ordinal)) ? rows[0].Os : null;
        return new PhoneSpec(
            os,
            Agreed(rows, row => row.OsVersionLaunch),
            Agreed(rows, row => row.OsVersionMax),
            Agreed(rows, row => row.ReleaseYear),
            Agreed(rows, row => row.ScreenInches),
            rows.Min(row => row.Confidence),
            rows.Count);
    }
}
