using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Coflnet.Ane;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Matches a listing title (plus optionally already-extracted brand/model/category) against the
/// verified known-products catalog. Pure and I/O-free: callers pass the catalog snapshot in, this class
/// never touches the store.
/// </summary>
/// <remarks>
/// <para>
/// Accessory detection here is deliberately a small, self-contained word list rather than a call into
/// AneNotifier's <c>ElectronicsExtractor</c> - AneCore is a dependency of AneNotifier, not the other way
/// around, so it cannot reuse that extractor's regexes directly. This keeps a single source of truth for
/// the *known-products veto* (this list); it does not duplicate ElectronicsExtractor's own, larger,
/// classification list, which serves a different purpose (naming the accessory type) and keeps doing its
/// own thing for the rule-extraction path.
/// </para>
/// <para>
/// Performance: the constructor builds a first-token index once per catalogue snapshot (see
/// <see cref="BuildIndex"/>) instead of the naive "scan every product's every alias on every call"
/// approach. <see cref="Match"/> then only ever looks at aliases that start with a token actually present
/// in the title, which keeps it fast even with tens of thousands of products. Build one instance per
/// snapshot and reuse it - see <see cref="KnownProductCatalog.Matcher"/>, which does exactly that.
/// </para>
/// </remarks>
public class KnownProductMatcher
{
    private static readonly RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>Words/phrases that unconditionally mark a listing as an accessory/part rather than the device itself.</summary>
    private static readonly string[] AccessoryWords =
    {
        "hulle", "huelle", "schutzhulle", "case", "cover", "etui", "coque", "bumper", "sleeve",
        "custodia", "custodie", "panzerglas", "schutzfolie", "displayschutz", "folie", "screenprotector",
        "kabel", "ladekabel", "ladegerat", "ladegeraet", "netzteil", "charger", "chargeur", "cavo", "adapter",
        "armband", "zubehor", "accessory", "accessoire", "accessorio",
        // Extended for the wider catalogue (consoles, GPUs/CPUs, cameras, watches, headphones, ...).
        "ersatzteil", "displayglas", "leerkarton", "ovp leer", "nur ovp", "ovp ohne inhalt",
        // Found via the real-listing-title evaluation (tools/catalog-import/eval): "Nintendo Switch
        // Tasche"/"Tragetasche" (carry bag), "Skin ps5" (cosmetic vinyl overlay) and "lüfter"/"kühler"
        // (replacement cooling fan) were all matching the console/GPU itself. "headset" is safe as an
        // unconditional accessory word - no seeded headphone product's own alias contains that word (they
        // are all specific brand+model names, e.g. "Bose QuietComfort 45"), so this cannot veto a
        // legitimate headphone listing, only a "<device> headset/Gaming-Headset" accessory-for-device one.
        "tasche", "tragetasche", "skin", "lufter", "kuhler", "headset",
    };

    /// <summary>"für iPhone", "for iPhone", "per iPhone", "pour iPhone" - accessory context even without a named part.</summary>
    private static readonly Regex AccessoryPreposition = new(
        @"\b(?:fur|fuer|for|per|pour)\s+(?:das\s+|den\s+|die\s+|ein\s+|apple\s+)?" +
        @"(?:iphone|ipad|handy|smartphone|telefon|tablet|ps5|ps4|ps3|playstation|xbox|switch|konsole|spielekonsole|" +
        @"laptop|notebook|macbook|kamera|uhr|watch|kopfhorer|airpods|monitor|controller)\b",
        Opts);

    /// <summary>"<accessory noun> für/fuer ..." - the accessory word comes before the preposition (e.g. "Tasche für Kamera").</summary>
    private static readonly Regex AccessoryBeforePreposition = new(
        @"\b(gehause|tasche|akku|controller|displayschutzglas|panzerglasfolie|hulle|huelle|schutzhulle)\s+(?:fur|fuer)\b",
        Opts);

    /// <summary>Titles containing one of these indicate a complete system, not a single listed part - see <see cref="IsCompleteSystemListing"/>.</summary>
    private static readonly string[] CompleteSystemWords =
    {
        "gaming pc", "gamingpc", "komplett pc", "komplettpc", "rechner", "desktop pc", "desktoppc",
    };

    /// <summary>
    /// Category leaf labels (last segment of <see cref="KnownProduct.Categories"/>) treated as a PC
    /// "component" for the container veto - a whole-system listing merely containing the part must not
    /// be matched to the component product itself. Kept narrow and explicit (see AneCore/tools/catalog-import
    /// README for the exact UnifiedCategories.json paths used) rather than inferring from
    /// <see cref="KnownProduct.Vertical"/>, which is informational/free text.
    /// </summary>
    private static readonly HashSet<string> ComponentCategoryLeaves = new(StringComparer.OrdinalIgnoreCase)
    {
        "Grafikkarten & Videoadapter", "Prozessoren", "RAM", "Motherboards", "Festplatten",
    };

    private readonly IReadOnlyList<KnownProduct> products;
    private readonly Dictionary<string, List<AliasEntry>> aliasIndex;

    private readonly record struct AliasEntry(KnownProduct Product, string[] Tokens);

    private readonly record struct TaxonomyLabelSets(HashSet<string> Roots, HashSet<string> AllLabels);

    /// <summary>
    /// Top-level taxonomy root labels (e.g. "Elektronik", "Bekleidung &amp; Accessoires") and every label
    /// appearing anywhere in <c>Categories/UnifiedCategories.json</c>, loaded once per process via
    /// <see cref="UnifiedCategoryService"/> and reused by <see cref="IsCategoryCompatible"/> - see that
    /// method's doc for why both sets exist. A process-wide <see cref="Lazy{T}"/> (rather than an
    /// instance/constructor field) so refreshing the known-products snapshot every few minutes (see
    /// <see cref="KnownProductCatalog"/>) never re-reads or re-parses the taxonomy file; it is computed
    /// once, not per call and not per matcher construction.
    /// </summary>
    private static readonly Lazy<TaxonomyLabelSets> Taxonomy = new(LoadTaxonomyLabelSets);

    private static TaxonomyLabelSets LoadTaxonomyLabelSets()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var categoryService = new UnifiedCategoryService();

            void Walk(UnifiedCategory category, bool isRoot)
            {
                allLabels.Add(category.Label);
                if (isRoot)
                    roots.Add(category.Label);
                foreach (var sub in category.SubCategories ?? new List<UnifiedCategory>())
                    Walk(sub, false);
            }

            foreach (var top in categoryService.GetTopLevelCategories())
                Walk(top, true);
        }
        catch
        {
            // Taxonomy file missing/unreadable in this environment - fall back to empty sets. That
            // reproduces the pre-fix behaviour (no generic root stripped) instead of throwing out of
            // every Match() call; see IsCategoryCompatible, which treats an unknown category as always
            // compatible, so this is the conservative direction to fail in.
        }
        return new TaxonomyLabelSets(roots, allLabels);
    }

    public KnownProductMatcher(IReadOnlyList<KnownProduct> products)
    {
        this.products = products;
        aliasIndex = BuildIndex(products);
    }

    /// <summary>
    /// Builds the first-token -> candidate-aliases index once. Products/aliases with an empty normalized
    /// alias are skipped (nothing to index).
    /// </summary>
    private static Dictionary<string, List<AliasEntry>> BuildIndex(IReadOnlyList<KnownProduct> products)
    {
        var index = new Dictionary<string, List<AliasEntry>>(StringComparer.Ordinal);
        foreach (var product in products)
        {
            foreach (var alias in product.Aliases)
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length == 0)
                    continue;
                var aliasTokens = normalizedAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (aliasTokens.Length == 0)
                    continue;
                if (!index.TryGetValue(aliasTokens[0], out var list))
                {
                    list = new List<AliasEntry>();
                    index[aliasTokens[0]] = list;
                }
                list.Add(new AliasEntry(product, aliasTokens));
            }
        }
        return index;
    }

    /// <summary>
    /// Returns the matching known product, or null when there is no match, the title looks like an
    /// accessory rather than the device, the match is ambiguous between two equally-specific products,
    /// a given brand contradicts the candidate's brand, the listing's category is known and incompatible
    /// with the candidate's category, or the title looks like a whole system that merely contains the
    /// matched component.
    /// </summary>
    /// <param name="title">Listing title.</param>
    /// <param name="brand">Already-extracted brand, if any - contradicting brands veto the match.</param>
    /// <param name="model">Currently unused for filtering; accepted for call-site symmetry with brand.</param>
    /// <param name="categoryPath">
    /// The listing's own category path (e.g. from platform category mapping), if known - callers may pass
    /// the raw/unmodified path, including the generic taxonomy root and any marketplace-only labels; see
    /// <see cref="IsCategoryCompatible"/> for how those are handled. When given, a candidate whose
    /// <see cref="KnownProduct.Categories"/> shares no (non-generic) label with this path is rejected. An
    /// unknown/empty category path (the default) never rejects anything.
    /// </param>
    public KnownProduct? Match(string? title, string? brand = null, string? model = null, IReadOnlyList<string>? categoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalizedTitle = Normalize(title);
        if (normalizedTitle.Length == 0 || IsAccessory(normalizedTitle))
            return null;

        var titleTokens = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matches = new List<(KnownProduct Product, int TokenCount, int StartIndex)>();
        for (var i = 0; i < titleTokens.Length; i++)
        {
            if (!aliasIndex.TryGetValue(titleTokens[i], out var candidates))
                continue;

            foreach (var entry in candidates)
            {
                var aliasTokens = entry.Tokens;
                if (i + aliasTokens.Length > titleTokens.Length)
                    continue;
                var ok = true;
                for (var j = 1; j < aliasTokens.Length; j++)
                {
                    if (titleTokens[i + j] != aliasTokens[j])
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok)
                    continue;
                if (IsExcluded(entry.Product, titleTokens, i, aliasTokens.Length))
                    continue;
                if (!IsCategoryCompatible(entry.Product, categoryPath))
                    continue;
                matches.Add((entry.Product, aliasTokens.Length, i));
            }
        }

        if (matches.Count == 0)
            return null;

        matches = ApplyContainerVeto(matches, titleTokens, normalizedTitle);
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

    /// <summary>
    /// Drops component-category matches (GPU/CPU/RAM/motherboard/storage) when the title both reads as a
    /// whole system ("Gaming PC ...", "Komplett PC ...", "Rechner ...") AND matched two or more distinct
    /// component products - e.g. "Gaming PC Ryzen 5 5600 RTX 3060 16GB" matching both the CPU and the GPU.
    /// Both signals are required: a single component alias in a system-worded title (e.g. a legitimate
    /// "Rechner mit RTX 3060" upgrade-part listing) is not enough on its own to veto, and multiple
    /// component matches without system wording (e.g. a bundle listing) is left to the normal
    /// longest-alias/ambiguity logic instead of being silently dropped here.
    /// </summary>
    private static List<(KnownProduct Product, int TokenCount, int StartIndex)> ApplyContainerVeto(
        List<(KnownProduct Product, int TokenCount, int StartIndex)> matches, string[] titleTokens, string normalizedTitle)
    {
        var componentMatches = matches.Where(m => IsComponentCategory(m.Product)).ToList();
        var distinctComponentProducts = componentMatches.Select(m => m.Product.Id).Distinct().Count();
        if (distinctComponentProducts < 2)
            return matches;
        if (!IsCompleteSystemListing(normalizedTitle))
            return matches;

        return matches.Where(m => !IsComponentCategory(m.Product)).ToList();
    }

    private static bool IsComponentCategory(KnownProduct product) =>
        product.Categories.Count > 0 && ComponentCategoryLeaves.Contains(product.Categories[^1]);

    private static bool IsCompleteSystemListing(string normalizedTitle)
    {
        foreach (var word in CompleteSystemWords)
        {
            if (normalizedTitle.Contains(word, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when the listing's own category is known and shares a label with the candidate's, once
    /// generic taxonomy roots are ignored on both sides. Unknown category (null/empty) is always
    /// compatible - same for a product with no categories.
    /// </summary>
    /// <remarks>
    /// Real product and listing category paths carry the whole taxonomy path including its generic
    /// top-level root (e.g. a video game and a game console both start with "Elektronik"), so comparing
    /// the raw paths would never veto anything within the same vertical - see the "Lego Worlds PS4"
    /// false-match this fixes. Root labels (<see cref="TaxonomyLabelSets.Roots"/>, derived once from
    /// <c>Categories/UnifiedCategories.json</c>, not hardcoded) are dropped from both sides first. If that
    /// leaves either side empty, the category is unknown and never vetoes - same as the pre-existing
    /// null/empty behaviour. Marketplace category trees also surface labels the unified taxonomy does not
    /// know at all (e.g. "Konsolen", "Handy &amp; Telefon", "PC-Zubehör &amp; Software"); a listing path
    /// made up entirely of such labels is likewise treated as unknown rather than risking a false veto
    /// from vocabulary the taxonomy simply doesn't model.
    /// </remarks>
    private static bool IsCategoryCompatible(KnownProduct product, IReadOnlyList<string>? categoryPath)
    {
        if (categoryPath == null || categoryPath.Count == 0)
            return true;
        if (product.Categories.Count == 0)
            return true;

        var taxonomy = Taxonomy.Value;
        var listingLabels = categoryPath.Where(c => !taxonomy.Roots.Contains(c)).ToList();
        var productLabels = product.Categories.Where(c => !taxonomy.Roots.Contains(c)).ToList();

        if (listingLabels.Count == 0 || productLabels.Count == 0)
            return true; // nothing left once the generic root(s) are removed - treat as unknown

        if (listingLabels.All(c => !taxonomy.AllLabels.Contains(c)))
            return true; // listing path is entirely marketplace vocabulary the taxonomy doesn't recognize

        foreach (var productCategory in productLabels)
        {
            foreach (var listingCategory in listingLabels)
            {
                if (string.Equals(productCategory, listingCategory, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
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

    private static bool IsAccessory(string normalizedTitle)
    {
        foreach (var word in AccessoryWords)
        {
            if (normalizedTitle.Contains(word, StringComparison.Ordinal))
                return true;
        }
        return AccessoryPreposition.IsMatch(normalizedTitle) || AccessoryBeforePreposition.IsMatch(normalizedTitle);
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

        // "+" is a real distinguishing character in a lot of model names (Galaxy S21 vs S21+, Note10+,
        // iPad Pro ...) - without this it gets wiped by the [^a-z0-9 ] catch-all below just like any
        // other punctuation, making e.g. "Galaxy S21+" normalize to the exact same tokens as "Galaxy
        // S21" and become permanently ambiguous between the two products.
        s = s.Replace("+", " plus ");

        s = Regex.Replace(s, @"[-_/]+", " ", Opts);
        s = Regex.Replace(s, @"(?<=[a-z])(?=[0-9])", " ", Opts);
        s = Regex.Replace(s, @"(?<=[0-9])(?=[a-z])", " ", Opts);
        s = Regex.Replace(s, @"[^a-z0-9 ]", " ", Opts);
        s = Regex.Replace(s, @"\s+", " ", Opts).Trim();
        return s;
    }
}
