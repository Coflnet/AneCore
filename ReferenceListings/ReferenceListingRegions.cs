using HtmlAgilityPack;

namespace Coflnet.Ane.ReferenceListings;

/// <summary>Decides whether a node belongs to navigation/footer/recommendation areas rather than the listing itself.</summary>
internal sealed class ListingRegionFilter
{
    private static readonly string[] StrongTokenPrefixes = ["related", "similar", "recommend", "sponsored", "crosssell", "upsell"];
    // Whole-token phrases ("aditem", "ad-listitem"); substrings such as "price-head-item" must not match.
    private static readonly string[][] StrongPhrases =
    [
        ["aditem"], ["adlistitem"], ["ad", "listitem"], ["smlrads"], ["otherads"], ["other", "ads"], ["more", "from"],
        ["also", "viewed"], ["also", "bought"], ["you", "may", "like"], ["you", "may", "also", "like"], ["recently", "viewed"],
    ];
    private static readonly string[] RrpTokens = ["old", "was", "rrp", "uvp", "msrp", "strike", "strikethrough", "regular", "original", "compare", "list"];
    private static readonly string[] WeakTokens = ["carousel", "slider", "swiper", "slick"];
    private static readonly string[] FooterNames = ["footer", "sitefooter", "pagefooter"];
    private static readonly string[] Attributes = ["id", "class", "data-testid", "data-gaevent"];

    private readonly HashSet<HtmlNode> anchorAncestors = new();
    private readonly Dictionary<HtmlNode, bool> cache = new();

    public ListingRegionFilter(HtmlDocument document)
    {
        // Only the first price markup and the first h1 identify the listing itself; later itemprops belong to cards.
        var anchors = new[] { document.DocumentNode.SelectSingleNode("//*[@itemprop='price']"), document.DocumentNode.SelectSingleNode("//h1") };
        foreach (var anchor in anchors.Where(a => a != null))
        {
            foreach (var ancestor in anchor.Ancestors())
                anchorAncestors.Add(ancestor);
        }
    }

    /// <summary>True when the node or one of its ancestors (up to main/body) is a non-listing region.</summary>
    public bool IsExcluded(HtmlNode node)
    {
        for (var current = node; current is { NodeType: HtmlNodeType.Element }; current = current.ParentNode)
        {
            if (current.Name is "main" or "body" or "html")
                return false;
            if (IsNonListing(current))
                return true;
        }
        return false;
    }

    /// <summary>True when the node wraps a recommendation card, which would otherwise repeat its price.</summary>
    public bool WrapsExcluded(HtmlNode node)
        => node.Descendants().Any(d => d.NodeType == HtmlNodeType.Element && IsNonListing(d));

    private bool IsNonListing(HtmlNode element)
    {
        if (cache.TryGetValue(element, out var cached))
            return cached;
        return cache[element] = ComputeNonListing(element);
    }

    private bool ComputeNonListing(HtmlNode element)
    {
        if (anchorAncestors.Contains(element))
            return false;
        if (element.Name == "nav")
            return true;
        if (IsPageFooter(element))
            return true;
        return MatchKind(element) switch
        {
            RegionMatch.Strong => true,
            RegionMatch.Weak => CountListedPrices(element) > 1,
            _ => false,
        };
    }

    private static bool IsPageFooter(HtmlNode element)
    {
        var named = element.Name == "footer" || ClassNames(element).Append(element.GetAttributeValue("id", "")).Any(n => FooterNames.Contains(Compact(n)));
        return named && !element.Ancestors().Any(a => a.Name is "article" or "main");
    }

    private static RegionMatch MatchKind(HtmlNode element)
    {
        var weak = false;
        foreach (var attribute in Attributes)
        {
            var value = element.GetAttributeValue(attribute, string.Empty).ToLowerInvariant();
            if (value.Length == 0)
                continue;
            if (IsStrong(value))
                return RegionMatch.Strong;
            weak |= Tokens(value).Any(t => WeakTokens.Contains(t));
        }
        return weak ? RegionMatch.Weak : RegionMatch.None;
    }

    private static bool IsStrong(string value)
        => Tokens(value).Any(t => StrongTokenPrefixes.Any(t.StartsWith))
            || value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(name => HasPhrase(Tokens(name).ToArray()));

    private static bool HasPhrase(string[] tokens)
        => StrongPhrases.Any(phrase => Enumerable.Range(0, Math.Max(0, tokens.Length - phrase.Length + 1))
            .Any(i => phrase.SequenceEqual(tokens.Skip(i).Take(phrase.Length))));

    /// <summary>Prices of distinct items in a slider; an RRP/old price next to the real price does not make a second item.</summary>
    private static int CountListedPrices(HtmlNode element)
    {
        var leaves = element.Descendants()
            .Where(d => d.NodeType == HtmlNodeType.Element && !d.ChildNodes.Any(c => c.NodeType == HtmlNodeType.Element))
            .Where(d => ReferencePriceText.CountPriceTokens(d.InnerText) > 0)
            .ToList();
        if (leaves.Count == 0)
            return ReferencePriceText.CountPriceTokens(element.InnerText);
        return leaves.Count(leaf => !IsReducedOrRrpPrice(leaf));
    }

    private static bool IsReducedOrRrpPrice(HtmlNode leaf)
    {
        for (var current = leaf; current is { NodeType: HtmlNodeType.Element } && current.Name is not ("body" or "main"); current = current.ParentNode)
        {
            if (current.Name is "del" or "s" or "strike" || Tokens(current.GetAttributeValue("class", "").ToLowerInvariant()).Any(RrpTokens.Contains))
                return true;
        }
        return System.Text.RegularExpressions.Regex.IsMatch(leaf.InnerText, @"\b(UVP|RRP|MSRP|statt|instead of)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static IEnumerable<string> Tokens(string value)
        => System.Text.RegularExpressions.Regex.Split(value, "[^a-z0-9]+").Where(t => t.Length > 0);

    private static IEnumerable<string> ClassNames(HtmlNode element)
        => element.GetAttributeValue("class", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string Compact(string name)
        => new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private enum RegionMatch { None, Weak, Strong }
}
