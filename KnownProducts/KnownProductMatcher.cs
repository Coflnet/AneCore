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
/// Performance: the constructor builds a first-three-token index once per catalogue snapshot (see
/// <see cref="BuildIndex"/>) instead of the naive "scan every product's every alias on every call"
/// approach. <see cref="Match"/> then only ever looks at aliases whose first tokens occur consecutively
/// in the title, which keeps it fast even with tens of thousands of products. Build one instance per
/// snapshot and reuse it - see <see cref="KnownProductCatalog.Matcher"/>, which does exactly that.
/// </para>
/// </remarks>
public class KnownProductMatcher
{
    private static readonly RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // Fixed LEGO rules must survive the listing pipeline's shared Regex-cache churn.
    private static readonly Regex LegoIncomplete = new(@"\b(?:unvollstandig|incomplete|fehlteile|konvolut|sammlung|bundle|moc|nachbau|kompatibel|nur|ohne)\b", Opts);
    private static readonly Regex LegoDisplayExtra = new(@"\b(?:mit|inkl|inklusive|with|including|avec)\s+(?:hangar\s+(?:(?:und|and)\s+)?)?(?:tisch|table|vitrine)\b", Opts);
    private static readonly Regex LegoExtraWords = new(@"\b(?:bauanleitung|anleitung|manual|instructions?|karton|verpackung|box|beleuchtung|light|led|lighting)\b", Opts);
    private static readonly Regex LegoSetNumber = new(@"^\d{3,7}$", Opts);
    private static readonly Regex LegoCompletenessPercent = new(@"(?<![\p{L}\d])(?:(?<negative>not|nicht|non|pas)\s+)?(?<percent>\d{1,3}(?:[.,]\d+)?)\s*%\s*(?:completo|complete|vollst[äa]ndig)\b", Opts | RegexOptions.IgnoreCase);
    private static readonly Regex PrimaryHandTool = new(@"^(?:(?:[1-9]\d*\s*(?:x|stuck|stueck|stk)|(?:werkzeugset|toolset|set|satz)(?:\s+mit)?)\s+)*(?<brand>[a-z]+)\s+(?<kind>seitenschneider|schraubendreher|rollgabelschlussel|pinces?|tanger|kabelsax)\b", Opts);
    private static readonly Regex CordlessToolName = new(@"\bakku\s*(?:bohrschrauber|staubsauger)\b", Opts);
    private static readonly Regex DjiPackage = new(@"\b(?:combo|fly\s+more)\b", Opts);
    private static readonly Regex DjiAccessory = new(@"\b(?:batter(?:y|ies)|propellers?|gimbal|filters?|remote(?:\s+controller)?|controller|bedienungsanleitung|manuals?)\b", Opts);

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
        "tasche", "tragetasche", "camera bag", "skin", "lufter", "kuhler", "headset",
        // "Akku" (German "battery") - a replacement/spare battery listing for a camera/laptop/phone is an
        // accessory, not the device itself (found via this task's catalogue-recall pass: "Sony A7 III
        // Akku" was matching the camera before this was added).
        "akku",
        // "Schutzglas" (German "protective glass" - tempered-glass screen protector) - found via this
        // pass's real-title evaluation re-run: "Samsung Galaxy A54 5G Schutzglas" matched the new Galaxy
        // A-series entry before this was added; "panzerglas"/"displayglas" above cover the same concept
        // under its other common German spellings but not this one.
        "schutzglas",
        // A microphone windscreen or cassette lockring is a subpart; existing included-extra semantics still allow it with the whole product.
        "windschutz", "verschlussring", "lockring",
        // Tool subparts are not the hedge trimmer; existing included-extra markers retain complete tools with spares.
        "vergaser", "ersatzmesser", "messerabdeckung",
        // Adjustable wrench replacement jaws and knurls are parts; included spare semantics still apply.
        "randelschraube", "ersatzbacke", "ersatzbacken", "knurl",
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
        "gaming pc", "gamingpc", "gaming dator", "bundle", "komplett pc", "komplettpc", "rechner", "desktop pc", "desktoppc",
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
    private readonly Dictionary<(string First, string? Second, string? Third), List<AliasEntry>> aliasIndex;
    private readonly List<(KnownProduct Product, string Brand, Regex Pattern)> handToolArticles;
    private readonly string[] knownBrands;

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
        knownBrands = products.Select(p => p.Brand).Distinct().Where(b => !string.IsNullOrWhiteSpace(b)).Select(Normalize).Distinct().ToArray();
        handToolArticles = products.Where(p => p.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
                && !string.IsNullOrWhiteSpace(p.Brand)
                && p.PossibleAttributes.GetValueOrDefault("article_number") is { Count: 1 } articles && articles.Single().Any(char.IsDigit))
            .GroupBy(p => (Normalize(p.Brand), Regex.Replace(p.PossibleAttributes["article_number"].Single(), @"\s+", "").ToLowerInvariant()))
            .Where(g => g.Select(p => p.Id).Distinct().Count() == 1)
            .Select(g => (g.First(), g.Key.Item1, new Regex(@"(?<![\p{L}\d])" + Regex.Escape(g.First().PossibleAttributes["article_number"].Single()).Replace(@"\ ", @"\s*")
                + @"(?!(?:[-_.,/]\s*)?[\p{L}\d])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))).ToList();
    }

    /// <summary>
    /// Builds the first-three-token index once (nulls for missing tokens in shorter aliases).
    /// Products/aliases with an empty normalized alias are skipped (nothing to index).
    /// </summary>
    private static Dictionary<(string First, string? Second, string? Third), List<AliasEntry>> BuildIndex(IReadOnlyList<KnownProduct> products)
    {
        var index = new Dictionary<(string First, string? Second, string? Third), List<AliasEntry>>();
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
                var key = (aliasTokens[0], aliasTokens.Length > 1 ? aliasTokens[1] : null, aliasTokens.Length > 2 ? aliasTokens[2] : null);
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<AliasEntry>();
                    index[key] = list;
                }
                list.Add(new AliasEntry(product, aliasTokens));
            }
        }
        return index;
    }

    private IEnumerable<(AliasEntry Entry, int Start)> FindAliases(string[] titleTokens)
    {
        for (var i = 0; i < titleTokens.Length; i++)
        {
            IEnumerable<AliasEntry> candidates = Enumerable.Empty<AliasEntry>();
            if (aliasIndex.TryGetValue((titleTokens[i], null, null), out var singleToken))
                candidates = singleToken;
            if (i + 1 < titleTokens.Length && aliasIndex.TryGetValue((titleTokens[i], titleTokens[i + 1], null), out var pair))
                candidates = candidates.Concat(pair);
            if (i + 2 < titleTokens.Length && aliasIndex.TryGetValue((titleTokens[i], titleTokens[i + 1], titleTokens[i + 2]), out var triple))
                candidates = candidates.Concat(triple);

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
                yield return (entry, i);
            }
        }
    }

    // A descriptor may separate the verified brand and exact article. Preserve the raw article
    // boundary before normalizing, so truncated numbers, decimal sizes and attached variants cannot match.
    private IEnumerable<(AliasEntry Entry, int Start)> FindHandToolArticles(string title, string normalizedTitle)
    {
        var padded = " " + normalizedTitle + " ";
        foreach (var article in handToolArticles)
        {
            if (!padded.Contains(" " + article.Brand + " ", StringComparison.Ordinal)) continue;
            var match = article.Pattern.Match(title);
            if (!match.Success) continue;
            if (knownBrands.Any(b => b != article.Brand && padded.Contains(" " + b + " ", StringComparison.Ordinal))) yield break;
            var tokens = Normalize(match.Value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var start = Normalize(title[..match.Index]).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            yield return (new AliasEntry(article.Product, tokens), start);
        }
    }

    /// <summary>Names a verified hand tool reference or a primary branded tool kind, without proving a single body,
    /// article variant, compatibility or quantity. Sets and related physical parts may contain a verified reference.</summary>
    public bool HasHandToolReference(string? title, IReadOnlyList<string>? categoryPath = null)
    {
        var normalized = Normalize(title ?? "");
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // A set may name its contents after "mit"; other products merely including a tool stay separate.
        var setOffer = tokens.TakeWhile(token => !IncludedMarkers.Contains(token))
            .Any(token => token is "set" or "werkzeugset" or "toolset" or "satz" or "kit" or "bundle" or "konvolut" or "sammlung" or "restposten");
        if (FindAliases(tokens).Concat(FindHandToolArticles(title ?? "", normalized)).Any(hit =>
            hit.Entry.Product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
            && IsCategoryCompatible(hit.Entry.Product, categoryPath)
            && (setOffer || !tokens.Take(hit.Start).TakeLast(3).Any(IncludedMarkers.Contains)))) return true;

        // No SKU is needed for an independent offer, but the tool must be the primary item.
        var primary = PrimaryHandTool.Match(normalized);
        return primary.Success && !IsAccessory(normalized)
            && products.Any(product => product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
                && Normalize(product.Brand) == primary.Groups["brand"].Value
                && IsCategoryCompatible(product, categoryPath));
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
        if (normalizedTitle.Length == 0)
            return null;

        var titleTokens = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var accessory = IsAccessory(normalizedTitle);
        if (accessory && !titleTokens.Contains("lego", StringComparer.Ordinal))
            return null;

        var matches = new List<(KnownProduct Product, int TokenCount, int StartIndex)>();
        foreach (var (entry, i) in FindAliases(titleTokens).Concat(FindHandToolArticles(title, normalizedTitle)))
        {
            var aliasTokens = entry.Tokens;
            // A vetted LEGO alias can name Dodge Charger; accessory words outside that span still veto it.
            if (accessory && (entry.Product.Vertical != "lego"
                || IsAccessory(string.Join(" ", titleTokens.Take(i).Concat(titleTokens.Skip(i + aliasTokens.Length))))))
                continue;
            if (IsExcluded(entry.Product, titleTokens, i, aliasTokens.Length))
                continue;
            if (entry.Product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
                && !entry.Product.PossibleAttributes.ContainsKey("socket_size_mm")
                && entry.Product.PossibleAttributes.GetValueOrDefault("article_number") is { Count: 1 } articles
                && Regex.IsMatch(title, @"(?<!\d)" + string.Join(@"[^a-z0-9]*", Normalize(articles.Single()).Split(' ').Select(Regex.Escape))
                    + @"(?:(?:[-_.,/]\s*)[\p{L}\d]|(?!(?:mm)\b)[\p{L}\d])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;
            if (entry.Product.Brand.Equals("DJI", StringComparison.OrdinalIgnoreCase)
                && entry.Product.PossibleAttributes.GetValueOrDefault("device_type")?.Any(t => t is "drone" or "camera") == true
                && (DjiPackage.IsMatch(normalizedTitle) || DjiAccessory.Matches(normalizedTitle)
                    .Any(m => !IsIncludedExtra(normalizedTitle, titleTokens, m.Index, m.Length))))
                continue;
            // Socket decimal sizes and attached article suffixes must not select a shorter catalogue SKU.
            if (entry.Product.PossibleAttributes.ContainsKey("socket_size_mm")
                && Regex.IsMatch(title, @"(?<!\d)" + string.Join(@"[^a-z0-9]*", Normalize(entry.Product.Model).Split(' ').Select(Regex.Escape))
                    + @"(?:[.,]\d|/\w|(?!(?:mm)\b)\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;
            if (!IsCategoryCompatible(entry.Product, categoryPath))
                continue;
            // A set number can also name a manual, empty box, partial set or custom build.
            // Keep those offers on the extraction path, where their scope is preserved.
            if (entry.Product.Vertical == "lego" &&
                (LegoIncomplete.IsMatch(normalizedTitle)
                 || LegoDisplayExtra.IsMatch(normalizedTitle)
                 || LegoExtraWords.Matches(normalizedTitle)
                     .Any(m => !IsIncludedExtra(normalizedTitle, titleTokens, m.Index, m.Length)
                         && (m.Index < titleTokens.Take(i).Sum(t => t.Length + 1)
                             || m.Index >= titleTokens.Take(i + aliasTokens.Length).Sum(t => t.Length + 1)))
                 // A verified full alias can explain vehicle model/year numbers inside its own span.
                 || HasOtherLegoNumber(titleTokens, i, aliasTokens.Length, entry.Product.Model, title)
                 // A model-specific pattern is cheaper to interpret than repeatedly compile after eviction.
                 || Regex.IsMatch(title, @"(?<!\d)" + Regex.Escape(entry.Product.Model) + @"-(?!1\b)\d+\b", Opts & ~RegexOptions.Compiled)))
                continue;
            matches.Add((entry.Product, aliasTokens.Length, i));
        }

        if (matches.Count == 0)
            return null;

        if (matches.Where(m => m.Product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true)
            .Select(m => m.Product.Id).Distinct().Skip(1).Any()) return null;

        matches = ApplyContainerVeto(matches, titleTokens, normalizedTitle, title);
        if (matches.Count == 0)
            return null;

        var best = matches.MaxBy(m => m.TokenCount);
        if (matches.Any(m => m.TokenCount == best.TokenCount && m.Product.Id != best.Product.Id))
            return null; // ambiguous: two different products matched with equally-specific aliases

        var candidate = best.Product;

        if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(candidate.Brand)
            && !string.Equals(Normalize(brand), Normalize(candidate.Brand), StringComparison.Ordinal))
        {
            return null; // extracted brand contradicts the candidate's brand
        }

        return candidate;
    }

    private static bool HasOtherLegoNumber(string[] tokens, int start, int length, string model, string title)
    {
        var completeHundreds = 0;
        if (title.Contains('%'))
        {
            var completeness = LegoCompletenessPercent.Matches(title);
            if (completeness.Any(m => m.Groups["negative"].Success || m.Groups["percent"].Value != "100")) return true;
            completeHundreds = completeness.Count;
        }
        for (var position = 0; position < tokens.Length; position++)
            if ((position < start || position >= start + length)
                && LegoSetNumber.IsMatch(tokens[position]) && tokens[position] != model)
            {
                if (tokens[position] == "100" && completeHundreds-- > 0) continue;
                return true;
            }
        return false;
    }

    /// <summary>
    /// Explicit whole-system or bundle phrases before a component, a motherboard/CPU bundle, or
    /// separate camera bodies describe an offer containing multiple items. Generic "Rechner" wording
    /// still needs multiple matched components so ordinary upgrade listings continue to match.
    /// </summary>
    private static List<(KnownProduct Product, int TokenCount, int StartIndex)> ApplyContainerVeto(
        List<(KnownProduct Product, int TokenCount, int StartIndex)> matches, string[] titleTokens,
        string normalizedTitle, string title)
    {
        // Separate camera names in one title describe a multi-camera offer, not either single body.
        var cameras = matches.Where(m => m.Product.Categories.LastOrDefault() == "Digitalkameras").ToList();
        if (cameras.Any(a => cameras.Any(b => a.Product.Id != b.Product.Id && a.StartIndex + a.TokenCount <= b.StartIndex)))
            matches = matches.Where(m => m.Product.Categories.LastOrDefault() != "Digitalkameras").ToList();
        var componentMatches = matches.Where(m => IsComponentCategory(m.Product)).ToList();
        if (componentMatches.Count == 0)
            return matches;
        var prefix = string.Join(" ", titleTokens.Take(componentMatches.Min(m => m.StartIndex)));
        var explicitSystem = CompleteSystemWords.Any(word => word != "rechner" && prefix.Contains(word, StringComparison.Ordinal));
        var boardCpuBundle = title.Contains(" + ", StringComparison.Ordinal)
            && (titleTokens.Contains("mainboard") || titleTokens.Contains("motherboard"))
            && componentMatches.Any(m => m.Product.Categories[^1] == "Prozessoren");
        var multipleComponents = componentMatches.Select(m => m.Product.Id).Distinct().Count() >= 2;
        if (!explicitSystem && !boardCpuBundle && !(multipleComponents && IsCompleteSystemListing(normalizedTitle)))
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

    /// <summary>Reject excluded model suffixes; scoped tool/turntable parts may occur anywhere unless included with the device.</summary>
    private static bool IsExcluded(KnownProduct product, string[] titleTokens, int aliasStart, int aliasLength)
    {
        if (product.ExcludeTerms.Count == 0)
            return false;
        if (product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("power_tool") == true
            || product.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
            || product.PossibleAttributes.GetValueOrDefault("device_type")?.Contains("turntable") == true)
        {
            var title = string.Join(' ', titleTokens);
            foreach (var exclusion in product.ExcludeTerms)
            {
                var term = Normalize(exclusion);
                if (term.Length == 0) continue;
                var terms = term.Split(' ');
                var position = 0;
                for (var i = 0; i + terms.Length <= titleTokens.Length; i++)
                {
                    if ((i < aliasStart || i + terms.Length > aliasStart + aliasLength)
                        && titleTokens.AsSpan(i, terms.Length).SequenceEqual(terms)
                        && !IsIncludedExtra(title, titleTokens, position, term.Length))
                        return true;
                    position += titleTokens[i].Length + 1;
                }
            }
            return false;
        }
        var nextIndex = aliasStart + aliasLength;
        if (nextIndex >= titleTokens.Length)
            return false;
        return product.ExcludeTerms.Contains(titleTokens[nextIndex]);
    }

    /// <summary>
    /// Words that turn a following accessory word into an extra sold WITH (or without) the device: "Nintendo Switch OLED inkl. Zubehör",
    /// "PS4 Konsole mit Controller und Kabeln", "AirPods Pro mit Ladecase", "iPhone 11 ohne Hülle". Deliberately not "und"/"plus"/"and":
    /// "Galaxy S21+ Hülle" normalises to "... plus hulle" and is still a case.
    /// </summary>
    private static readonly HashSet<string> IncludedMarkers = new(StringComparer.Ordinal)
    {
        "mit", "inkl", "inklusive", "incl", "inclusive", "including", "with", "avec", "con", "samt", "sowie", "inclusief", "met",
        "incluye", "comprend", "compris", "ohne", "sans", "senza", "sin", "without", "zonder", "sem",
    };

    /// <summary>Words right after an accessory word that say it comes with the device ("Zubehör dabei", "Ladekabel inklusive").</summary>
    private static readonly HashSet<string> IncludedAfterMarkers = new(StringComparer.Ordinal)
    {
        "dabei", "included", "inklusive", "inkl", "gratis", "inbegrepen", "inclus", "incluso", "inclusa",
    };

    private bool IsAccessory(string normalizedTitle)
    {
        var primary = PrimaryHandTool.Match(normalizedTitle);
        var kind = primary.Groups["kind"];
        // A recognized tool maker's primary Swedish cable cutter is not a cable accessory.
        if (kind.Value == "kabelsax" && handToolArticles.Any(article => article.Brand == primary.Groups["brand"].Value))
            normalizedTitle = normalizedTitle[..kind.Index] + "werkzeug" + normalizedTitle[(kind.Index + kind.Length)..];
        // These whole-machine nouns do not offer a replacement battery.
        normalizedTitle = CordlessToolName.Replace(normalizedTitle, "werkzeug");
        var tokens = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in AccessoryWords)
        {
            var from = 0;
            while (true)
            {
                var index = normalizedTitle.IndexOf(word, from, StringComparison.Ordinal);
                if (index < 0)
                    break;
                from = index + word.Length;
                if (!IsIncludedExtra(normalizedTitle, tokens, index, word.Length))
                    return true;
            }
        }
        return AccessoryPreposition.IsMatch(normalizedTitle) || AccessoryBeforePreposition.IsMatch(normalizedTitle);
    }

    /// <summary>
    /// True when the accessory word at <paramref name="index"/> is an extra that is part of the offer, not the item itself: one of
    /// <see cref="IncludedMarkers"/> within the three words in front of it, or <see cref="IncludedAfterMarkers"/> right behind it.
    /// </summary>
    private static bool IsIncludedExtra(string normalizedTitle, string[] tokens, int index, int length)
    {
        var start = 0;
        var tokenIndex = -1;
        for (var i = 0; i < tokens.Length; i++)
        {
            var end = start + tokens[i].Length;
            if (index >= start && index < end)
            {
                tokenIndex = i;
                break;
            }
            start = end + 1;
        }
        if (tokenIndex < 0)
            return false;

        for (var back = 1; back <= 3 && tokenIndex - back >= 0; back++)
        {
            if (IncludedMarkers.Contains(tokens[tokenIndex - back]))
                return true;
        }

        var afterIndex = tokenIndex + 1;
        var afterPosition = start + tokens[tokenIndex].Length + 1;
        while (afterIndex < tokens.Length && afterPosition < index + length)
        {
            afterPosition += tokens[afterIndex].Length + 1;
            afterIndex++;
        }
        var after = afterIndex < tokens.Length ? tokens[afterIndex] : null;
        return after != null && IncludedAfterMarkers.Contains(after);
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

    // Instance regexes, not static Regex.Replace calls: the static overloads go through the process-wide regex cache (15 entries), which the rest of the
    // listing pipeline evicts constantly, and a RegexOptions.Compiled pattern was then recompiled on every call (~3 ms per listing measured).
    private static readonly Regex SeparatorRuns = new(@"[-_/]+", Opts);
    private static readonly Regex LetterDigit = new(@"(?<=[a-z])(?=[0-9])", Opts);
    private static readonly Regex DigitLetter = new(@"(?<=[0-9])(?=[a-z])", Opts);
    private static readonly Regex NonAlphanumeric = new(@"[^a-z0-9 ]", Opts);
    private static readonly Regex WhitespaceRuns = new(@"\s+", Opts);

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

        s = SeparatorRuns.Replace(s, " ");
        s = LetterDigit.Replace(s, " ");
        s = DigitLetter.Replace(s, " ");
        s = NonAlphanumeric.Replace(s, " ");
        s = WhitespaceRuns.Replace(s, " ").Trim();
        s = ApplySpellingVariants(s);
        return s;
    }

    /// <summary>
    /// Canonicalizes recurring misspellings/localizations of brand-name words found in real listing
    /// titles/queries (see tools/catalog-import/eval/EVALUATION.md's "Language/spelling variants" gap) so
    /// they match the catalogue's English-spelled aliases, without loosening matching generally - every
    /// rule fires only on an exact whole-word (token) sequence via <c>\b</c>, never a substring, so it
    /// cannot match inside an unrelated word (e.g. "series" itself is untouched by the "serie" rule).
    /// </summary>
    /// <remarks>
    /// French "Série" already collapses to "serie" by the diacritic-stripping step above (the accent is a
    /// <see cref="UnicodeCategory.NonSpacingMark"/>), so a single "serie" -&gt; "series" rule covers both
    /// the German "Serie" and French "Série" gaps. "i phone"/"x box"/"play station"/"mac book"
    /// (space-separated in the source title) are real, recurring patterns the letter/digit-join step above
    /// does not merge because no digit is involved.
    /// </remarks>
    // One combined regex + lookup table rather than five separate Regex objects: constructing a Regex
    // (parsing the pattern into its matching automaton) is a real one-time cost that, at the scale of
    // this catalogue's alias index (thousands of Normalize() calls per KnownProductMatcher build), showed
    // up as a measurable index-build-time regression (task target: 10-40ms) - see
    // tools/catalog-import/eval/EVALUATION.md's performance section. RegexOptions.Compiled (used
    // elsewhere in this file via Opts) is deliberately NOT used here either, for the same reason: these
    // patterns run once per Normalize() call, never in a tight loop, so paying Compiled's extra
    // construction-time MSIL-JIT cost for faster matching is the wrong trade.
    private static readonly Regex SpellingVariantPattern =
        new(@"\b(?:i phone|x box|play station|mac book|serie|promax)\b", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> SpellingVariantReplacements = new(StringComparer.Ordinal)
    {
        ["i phone"] = "iphone",
        ["x box"] = "xbox",
        ["play station"] = "playstation",
        ["mac book"] = "macbook",
        ["serie"] = "series",
        ["promax"] = "pro max",
    };

    private static string ApplySpellingVariants(string s) =>
        SpellingVariantPattern.Replace(s, m => SpellingVariantReplacements[m.Value]);
}
