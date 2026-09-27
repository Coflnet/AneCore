using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Matches a listing title (plus optionally already-extracted brand/model) against the verified
/// known-products catalog. Pure and I/O-free: callers pass the catalog snapshot in, this class never
/// touches the store.
/// </summary>
/// <remarks>
/// Accessory detection here is deliberately a small, self-contained word list rather than a call into
/// AneNotifier's <c>ElectronicsExtractor</c> - AneCore is a dependency of AneNotifier, not the other way
/// around, so it cannot reuse that extractor's regexes directly. This keeps a single source of truth for
/// the *known-products veto* (this list); it does not duplicate ElectronicsExtractor's own, larger,
/// classification list, which serves a different purpose (naming the accessory type) and keeps doing its
/// own thing for the rule-extraction path.
/// </remarks>
public class KnownProductMatcher
{
    private static readonly RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>Words that mark a listing as an accessory/part rather than the device itself.</summary>
    private static readonly string[] AccessoryWords =
    {
        "hulle", "huelle", "schutzhulle", "case", "cover", "etui", "coque", "bumper", "sleeve",
        "custodia", "custodie", "panzerglas", "schutzfolie", "displayschutz", "folie", "screenprotector",
        "kabel", "ladekabel", "ladegerat", "ladegeraet", "netzteil", "charger", "chargeur", "cavo", "adapter",
        "armband", "zubehor", "accessory", "accessoire", "accessorio",
    };

    /// <summary>"für iPhone", "for iPhone", "per iPhone", "pour iPhone" - accessory context even without a named part.</summary>
    private static readonly Regex AccessoryPreposition = new(
        @"\b(?:fur|fuer|for|per|pour)\s+(?:das\s+|den\s+|die\s+|ein\s+|apple\s+)?(?:iphone|ipad|handy|smartphone|telefon|tablet)\b",
        Opts);

    private readonly IReadOnlyList<KnownProduct> products;

    public KnownProductMatcher(IReadOnlyList<KnownProduct> products)
    {
        this.products = products;
    }

    /// <summary>
    /// Returns the matching known product, or null when there is no match, the title looks like an
    /// accessory rather than the device, the match is ambiguous between two equally-specific products,
    /// or a given brand contradicts the candidate's brand.
    /// </summary>
    public KnownProduct? Match(string? title, string? brand = null, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalizedTitle = Normalize(title);
        if (normalizedTitle.Length == 0 || IsAccessory(normalizedTitle))
            return null;

        var titleTokens = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matches = new List<(KnownProduct Product, int TokenCount, int StartIndex)>();
        foreach (var product in products)
        {
            foreach (var alias in product.Aliases)
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length == 0)
                    continue;
                var aliasTokens = normalizedAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var start = FindTokenSequence(titleTokens, aliasTokens);
                if (start < 0)
                    continue;
                if (IsExcluded(product, titleTokens, start, aliasTokens.Length))
                    continue;
                matches.Add((product, aliasTokens.Length, start));
            }
        }

        if (matches.Count == 0)
            return null;

        var maxTokens = matches.Max(m => m.TokenCount);
        var best = matches.Where(m => m.TokenCount == maxTokens).ToList();
        var distinctProducts = best.Select(m => m.Product.Id).Distinct().ToList();
        if (distinctProducts.Count > 1)
            return null; // ambiguous: two different products matched with equally-specific aliases

        var candidate = best[0].Product;

        if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(candidate.Brand)
            && !string.Equals(Normalize(brand), Normalize(candidate.Brand), StringComparison.Ordinal))
        {
            return null; // extracted brand contradicts the candidate's brand
        }

        return candidate;
    }

    /// <summary>True when a token immediately after the matched alias is one of the product's exclude terms.</summary>
    private static bool IsExcluded(KnownProduct product, string[] titleTokens, int aliasStart, int aliasLength)
    {
        if (product.ExcludeTerms.Count == 0)
            return false;
        var nextIndex = aliasStart + aliasLength;
        if (nextIndex >= titleTokens.Length)
            return false;
        return product.ExcludeTerms.Contains(titleTokens[nextIndex]);
    }

    /// <summary>Index of the first token of <paramref name="needle"/> in <paramref name="haystack"/>, or -1.</summary>
    private static int FindTokenSequence(string[] haystack, string[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
            return -1;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    private static bool IsAccessory(string normalizedTitle)
    {
        foreach (var word in AccessoryWords)
        {
            if (normalizedTitle.Contains(word, StringComparison.Ordinal))
                return true;
        }
        return AccessoryPreposition.IsMatch(normalizedTitle);
    }

    /// <summary>
    /// Lowercases, strips diacritics, unifies separators to spaces and splits letter/digit joins
    /// ("iPhone14" / "iphone14pro" -&gt; "iphone 14 pro") so alias matching works on plain tokens.
    /// </summary>
    /// <summary>
    /// string.Normalize throws on lone surrogates (truncated emoji in scraped titles); replace them
    /// with a space so a broken character never drops the listing. Valid pairs are kept.
    /// </summary>
    internal static string ReplaceLoneSurrogates(string input)
    {
        StringBuilder? sb = null;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (!char.IsSurrogate(c))
                continue;
            if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
            {
                i++;
                continue;
            }
            sb ??= new StringBuilder(input);
            sb[i] = ' ';
        }
        return sb?.ToString() ?? input;
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var decomposed = ReplaceLoneSurrogates(input.ToLowerInvariant()).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c);
        }
        var s = sb.ToString().Replace("ß", "ss");

        s = Regex.Replace(s, @"[-_/]+", " ", Opts);
        s = Regex.Replace(s, @"(?<=[a-z])(?=[0-9])", " ", Opts);
        s = Regex.Replace(s, @"(?<=[0-9])(?=[a-z])", " ", Opts);
        s = Regex.Replace(s, @"[^a-z0-9 ]", " ", Opts);
        s = Regex.Replace(s, @"\s+", " ", Opts).Trim();
        return s;
    }
}
