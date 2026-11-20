using System.Text.Json;

namespace Coflnet.Ane.KnownProducts;

/// <summary>A plain LEGO part (mould) from Rebrickable, e.g. 3001 "Brick 2 x 4". Prints and patterns are never part of this list.</summary>
/// <param name="SharesSetNumber">The number is also the base number of a Rebrickable set (3001 is also a promotional set):
/// it names the part only when the title says it is a part.</param>
public record LegoPart(string Number, string Name, int CategoryId, string? Category, bool SharesSetNumber);

/// <summary>A Rebrickable colour with its page slug ("light-bluish-gray") and the reviewed localized phrases that name it in a title.</summary>
public record LegoColor(int Id, string Name, string Rgb, bool IsTransparent, string Slug, IReadOnlyDictionary<string, IReadOnlyList<string>> Terms)
{
    /// <summary>Reviewed phrases of a language code (en, de, fr, nl, it, es, pl); empty when none.</summary>
    public IReadOnlyList<string> TermsFor(string language) =>
        Terms.TryGetValue(language, out var terms) ? terms : Array.Empty<string>();

    /// <summary>Every reviewed phrase of every language.</summary>
    public IEnumerable<string> AllTerms => Terms.Values.SelectMany(t => t);
}

/// <summary>A LEGO element id: one part in one colour (300121 = Brick 2 x 4 in Red).</summary>
public record LegoElement(string ElementId, string PartNumber, int ColorId);

/// <summary>
/// Plain LEGO parts, colours and element ids, imported from Rebrickable by tools/catalog-import/import_lego_parts.py into the embedded
/// KnownProducts/LegoPartInfo.json. Like <see cref="KnownProductSetInfo"/> it is separate from the seed rows: it never feeds catalogue ids,
/// names, aliases or matching and does not change <see cref="KnownProductSeed.Version"/>.
/// </summary>
public static class KnownLegoParts
{
    private const string ResourceName = "Coflnet.Ane.KnownProducts.LegoPartInfo.json";
    private static readonly Lazy<Data> data = new(Load);

    /// <summary>The part of a plain part number ("3001", "3069b"), or null when the number is no imported part.</summary>
    public static LegoPart? ForPartNumber(string number) => data.Value.Parts.GetValueOrDefault(number.Trim().ToLowerInvariant());

    /// <summary>The part and colour of an element id ("300121"), or null when unknown.</summary>
    public static LegoElement? ForElementId(string elementId) => data.Value.Elements.GetValueOrDefault(elementId.Trim());

    /// <summary>A colour by Rebrickable id, or null.</summary>
    public static LegoColor? Color(int id) => data.Value.Colors.GetValueOrDefault(id);

    /// <summary>All colours by Rebrickable id.</summary>
    public static IReadOnlyDictionary<int, LegoColor> Colors => data.Value.Colors;

    /// <summary>Reviewed phrases that leave a title's colour unknown ("hellgrau", "braun", "trans", "metallic").</summary>
    public static IReadOnlyList<string> AmbiguousColorWords => data.Value.AmbiguousColorWords;

    /// <summary>Localized search words of an English part-name word ("brick" -> Stein, Baustein, brique, ...); empty when none.</summary>
    public static IReadOnlyList<string> PartWordTerms(string word) =>
        data.Value.PartWords.GetValueOrDefault(word.ToLowerInvariant()) ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>Part name, category, translated part-name words and the colour's name and phrases, space separated, for search indexing.</summary>
    public static string SearchText(LegoPart part, LegoColor? color)
    {
        var englishWords = $"{part.Name} {part.Category}".Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var translated = englishWords.SelectMany(PartWordTerms);
        var colorWords = color == null ? [] : color.AllTerms.Prepend(color.Name);
        return string.Join(' ', new[] { part.Name, part.Category ?? "" }.Concat(translated).Concat(colorWords).Where(s => s.Length > 0));
    }

    private sealed record Data(
        IReadOnlyDictionary<string, LegoPart> Parts,
        IReadOnlyDictionary<string, LegoElement> Elements,
        IReadOnlyDictionary<int, LegoColor> Colors,
        IReadOnlyList<string> AmbiguousColorWords,
        IReadOnlyDictionary<string, IReadOnlyList<string>> PartWords);

    private static Data Load()
    {
        using var stream = typeof(KnownLegoParts).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found");
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var categories = root.GetProperty("categories").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetString());
        return new Data(
            root.GetProperty("parts").EnumerateObject().ToDictionary(p => p.Name, p => ParsePart(p, categories), StringComparer.Ordinal),
            root.GetProperty("elements").EnumerateObject().ToDictionary(p => p.Name, ParseElement, StringComparer.Ordinal),
            root.GetProperty("colors").EnumerateObject().ToDictionary(p => int.Parse(p.Name), ParseColor),
            root.GetProperty("ambiguousColorWords").EnumerateArray().Select(w => w.GetString() ?? "").Where(w => w.Length > 0).ToList(),
            root.GetProperty("partWords").EnumerateObject().ToDictionary(p => p.Name, p => AllLanguages(p.Value), StringComparer.Ordinal));
    }

    private static LegoPart ParsePart(JsonProperty property, Dictionary<int, string?> categories)
    {
        var values = property.Value;
        var category = values[1].GetInt32();
        return new LegoPart(property.Name, values[0].GetString() ?? "", category, categories.GetValueOrDefault(category), values[2].GetInt32() == 1);
    }

    private static LegoElement ParseElement(JsonProperty property) =>
        new(property.Name, property.Value[0].GetString() ?? "", property.Value[1].GetInt32());

    private static LegoColor ParseColor(JsonProperty property)
    {
        var item = property.Value;
        var terms = item.TryGetProperty("terms", out var own)
            ? own.EnumerateObject().ToDictionary(l => l.Name, l => (IReadOnlyList<string>)l.Value.EnumerateArray().Select(t => t.GetString() ?? "").ToList())
            : new Dictionary<string, IReadOnlyList<string>>();
        return new LegoColor(int.Parse(property.Name), item.GetProperty("name").GetString() ?? "", item.GetProperty("rgb").GetString() ?? "",
            item.GetProperty("trans").GetBoolean(), item.GetProperty("slug").GetString() ?? "", terms);
    }

    private static IReadOnlyList<string> AllLanguages(JsonElement byLanguage) =>
        byLanguage.EnumerateObject().SelectMany(l => l.Value.EnumerateArray().Select(t => t.GetString() ?? "")).Distinct().ToList();
}
