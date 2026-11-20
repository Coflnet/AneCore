using System.Text.Json;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Display and search metadata of a LEGO set, imported from Rebrickable by
/// tools/catalog-import/enrich_lego_titles.py into the embedded KnownProducts/LegoSetInfo.json. It covers every
/// unambiguous 4-5 digit set number, whether or not the set is a catalogue (seed) entry. It is deliberately separate
/// from the seed rows: it never feeds ids, names, aliases or matching, and does not change
/// <see cref="KnownProductSeed.Version"/>. Read it with <see cref="KnownProductSetInfo.ForSetNumber"/> or
/// <see cref="KnownProductSetInfo.SetInfo"/>.
/// </summary>
public record LegoSetInfo(
    string Title,
    string? Theme,
    string? ParentTheme,
    int? Year,
    int? Pieces,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Terms)
{
    /// <summary>Translated title and theme words for a language code (de, fr, nl, it, es, pl); empty when none.</summary>
    public IReadOnlyList<string> TermsFor(string language) =>
        Terms.TryGetValue(language, out var terms) ? terms : Array.Empty<string>();

    /// <summary>English title, themes and every translated word, space separated, for search indexing.</summary>
    public string SearchText() =>
        string.Join(' ', new[] { Title, Theme, ParentTheme }.Concat(Terms.Values.SelectMany(t => t)).Where(s => !string.IsNullOrEmpty(s)));
}

public static class KnownProductSetInfo
{
    private const string ResourceName = "Coflnet.Ane.KnownProducts.LegoSetInfo.json";
    private static readonly Lazy<IReadOnlyDictionary<string, LegoSetInfo>> all = new(Load);

    /// <summary>Set info by printed set number (e.g. "60198").</summary>
    public static IReadOnlyDictionary<string, LegoSetInfo> All => all.Value;

    /// <summary>Set info for a printed set number ("10874"), or null when unknown. Accepts an optional "-1" suffix.</summary>
    public static LegoSetInfo? ForSetNumber(string setNumber)
    {
        var number = setNumber.Trim();
        if (number.EndsWith("-1", StringComparison.Ordinal))
            number = number[..^2];
        return All.GetValueOrDefault(number);
    }

    /// <summary>Set info of a catalogue product, found through its model or <c>set_number</c> attribute; null when none.</summary>
    public static LegoSetInfo? SetInfo(this KnownProduct product)
    {
        if (ForSetNumber(product.Model) is { } byModel)
            return byModel;
        var numbers = product.PossibleAttributes.GetValueOrDefault("set_number");
        return numbers is { Count: 1 } ? ForSetNumber(numbers.Single()) : null;
    }

    private static IReadOnlyDictionary<string, LegoSetInfo> Load()
    {
        using var stream = typeof(KnownProductSetInfo).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found");
        using var doc = JsonDocument.Parse(stream);
        var themeTerms = ParseTermMap(doc.RootElement.GetProperty("themes"), p => p.Value);
        return doc.RootElement.GetProperty("sets").EnumerateObject()
            .ToDictionary(p => p.Name, p => Parse(p.Value, themeTerms), StringComparer.Ordinal);
    }

    private static LegoSetInfo Parse(JsonElement item, Dictionary<string, Dictionary<string, List<string>>> themeTerms)
    {
        var theme = OptionalString(item, "theme");
        var parent = OptionalString(item, "parentTheme");
        var terms = item.TryGetProperty("terms", out var own) ? ParseLanguages(own) : new Dictionary<string, List<string>>();
        foreach (var name in new[] { theme, parent })
            if (name != null && themeTerms.TryGetValue(name, out var extra))
                MergeTerms(terms, extra);
        return new(item.GetProperty("title").GetString() ?? "", theme, parent,
            OptionalInt(item, "year"), OptionalInt(item, "pieces"),
            terms.ToDictionary(t => t.Key, t => (IReadOnlyList<string>)t.Value));
    }

    private static void MergeTerms(Dictionary<string, List<string>> target, Dictionary<string, List<string>> extra)
    {
        foreach (var (language, words) in extra)
        {
            if (!target.TryGetValue(language, out var list))
                target[language] = list = new List<string>();
            list.AddRange(words.Where(w => !list.Contains(w)));
        }
    }

    private static Dictionary<string, Dictionary<string, List<string>>> ParseTermMap(JsonElement element, Func<JsonProperty, JsonElement> select) =>
        element.EnumerateObject().ToDictionary(p => p.Name, p => ParseLanguages(select(p)));

    private static Dictionary<string, List<string>> ParseLanguages(JsonElement element) =>
        element.EnumerateObject().ToDictionary(l => l.Name, l => l.Value.EnumerateArray().Select(t => t.GetString() ?? "").ToList());

    private static string? OptionalString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static int? OptionalInt(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.GetInt32() : null;
}
