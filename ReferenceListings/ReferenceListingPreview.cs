using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Coflnet.Ane.ReferenceListings;

public class ReferenceListingPreviewService
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SearchNoiseRegex = new(@"\b(eBay\.?de|eBay|Cardmarket|Pokémon TCG|Pokemon TCG)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SearchPriceNoiseRegex = new(
        @"(?:(?:€|EUR|CHF|US\$|\$|£|GBP)\s*\d{1,3}(?:[.\s]\d{3})*(?:[,\.]\d{1,2})?|\d{1,3}(?:[.\s]\d{3})*(?:[,\.]\d{1,2})?\s*(?:€|EUR|CHF|US\$|\$|£|GBP))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PriceRegex = new(
        @"(?:(?<currency>€|EUR|CHF|US\$|\$|£|GBP)\s*)?(?<amount>\d{1,3}(?:[.\s]\d{3})*(?:[,\.]\d{1,2})?|\d+(?:[,\.]\d{1,2})?)(?:\s*(?<currency2>€|EUR|CHF|US\$|\$|£|GBP))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ReferenceListingPreviewResponse Build(Uri uri, string html, string? selectedSelector)
    {
        var document = LoadDocument(html);
        var listing = ExtractListingSummary(document, uri);
        var marketplace = ReferenceListingUrlPolicy.DetectMarketplace(uri.Host);
        var candidates = ExtractRankedPriceCandidates(document);
        var selected = SelectReferencePrice(candidates, selectedSelector);
        var previewElements = BuildPreviewElements(document, candidates, listing.Title, listing.Description)
            .Take(48)
            .ToList();

        MarkSelectedPreviewElement(previewElements, selected);

        return new ReferenceListingPreviewResponse
        {
            Url = uri.ToString(),
            Host = uri.Host,
            Marketplace = marketplace,
            Listing = listing,
            PriceCandidates = candidates,
            PreviewElements = previewElements,
            SelectedReference = selected,
            RequiresReferencePriceSelection = selected == null && candidates.Count > 0,
            SuggestedFilter = BuildSuggestedFilter(uri, marketplace, listing, selected),
        };
    }

    public ReferenceListingPreviewResponse BuildFromRender(Uri uri, RenderedReferenceListing render, string? selectedSelector, List<string>? additionalSelectors)
    {
        var html = render.Html ?? string.Empty;
        var document = LoadDocument(html);
        var listing = ExtractListingSummary(document, uri);
        var marketplace = ReferenceListingUrlPolicy.DetectMarketplace(uri.Host);
        var htmlCandidates = ExtractRankedPriceCandidates(document);

        // Merge in rendered-node candidates (XPath is the canonical selector key).
        var merged = new List<ReferencePriceCandidate>(htmlCandidates);
        var seenSelectors = new HashSet<string>(htmlCandidates.Select(c => c.Selector), StringComparer.Ordinal);
        foreach (var node in render.Nodes)
        {
            if (node.Price == null)
                continue;
            var key = node.XPath;
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!seenSelectors.Add(key))
                continue;
            merged.Add(new ReferencePriceCandidate
            {
                Selector = key,
                Text = node.Text,
                Price = node.Price.Value,
                Currency = string.IsNullOrWhiteSpace(node.Currency) ? "EUR" : node.Currency!,
                Confidence = (int)Math.Round(node.Confidence),
                Source = "render",
            });
        }

        merged = merged
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Price)
            .Take(64)
            .ToList();

        var selected = SelectReferencePrice(merged, selectedSelector);

        // Apply summed-additional logic if extra selectors were supplied.
        if (selected != null && additionalSelectors != null && additionalSelectors.Count > 0)
        {
            var lookup = merged.ToDictionary(c => c.Selector, StringComparer.Ordinal);
            double sum = selected.Price;
            var texts = new List<string> { selected.Text };
            foreach (var extra in additionalSelectors)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;
                if (lookup.TryGetValue(extra, out var extraCandidate))
                {
                    sum += extraCandidate.Price;
                    texts.Add(extraCandidate.Text);
                }
                else
                {
                    // Try render nodes directly
                    var node = render.Nodes.FirstOrDefault(n => n.XPath == extra && n.Price != null);
                    if (node != null && node.Price != null)
                    {
                        sum += node.Price.Value;
                        texts.Add(node.Text);
                    }
                }
            }
            selected = new ReferencePriceCandidate
            {
                Selector = selected.Selector,
                Text = string.Join(" + ", texts),
                Price = Math.Round(sum, 2),
                Currency = selected.Currency,
                Confidence = selected.Confidence,
                Source = "summed",
                IsAutoSelected = false,
            };
        }

        var previewElements = BuildPreviewElements(document, merged, listing.Title, listing.Description)
            .Take(48)
            .ToList();
        MarkSelectedPreviewElement(previewElements, selected);

        // Determine PrimarySelectorXPath: if selected key looks like an XPath, use it directly,
        // otherwise look it up in render.Nodes.
        string? primaryXPath = null;
        if (selected != null)
        {
            if (selected.Selector.StartsWith("/"))
                primaryXPath = selected.Selector;
            else
            {
                var node = render.Nodes.FirstOrDefault(n => n.Css == selected.Selector);
                primaryXPath = node?.XPath;
            }
        }

        return new ReferenceListingPreviewResponse
        {
            Url = uri.ToString(),
            Host = uri.Host,
            Marketplace = marketplace,
            Listing = listing,
            PriceCandidates = merged,
            PreviewElements = previewElements,
            SelectedReference = selected,
            RequiresReferencePriceSelection = selected == null && merged.Count > 0,
            SuggestedFilter = BuildSuggestedFilter(uri, marketplace, listing, selected),
            ScreenshotPngBase64 = string.IsNullOrEmpty(render.ScreenshotPngBase64) ? null : render.ScreenshotPngBase64,
            ViewportWidth = render.ViewportWidth,
            ViewportHeight = render.ViewportHeight,
            FullPageHeight = render.FullPageHeight,
            SandboxHtml = BuildSandboxHtml(uri, html),
            RenderedNodes = render.Nodes,
            PrimarySelectorXPath = primaryXPath,
        };
    }

    private static string? BuildSandboxHtml(Uri uri, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var doc = new HtmlDocument { OptionWriteEmptyNodes = true };
        doc.LoadHtml(html);

        // Remove scripts.
        var scripts = doc.DocumentNode.SelectNodes("//script") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in scripts.ToList())
            node.Remove();

        // Strip inline event handlers and dangerous attributes; neutralize form actions.
        var all = doc.DocumentNode.SelectNodes("//*") ?? Enumerable.Empty<HtmlNode>();
        foreach (var el in all)
        {
            var attrs = el.Attributes.ToList();
            foreach (var attr in attrs)
            {
                if (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    el.Attributes.Remove(attr);
            }
            if (string.Equals(el.Name, "form", StringComparison.OrdinalIgnoreCase))
            {
                if (el.Attributes["action"] != null)
                    el.Attributes["action"].Value = "javascript:void(0)";
            }
        }

        var baseHref = uri.GetLeftPart(UriPartial.Authority) + "/";
        var head = doc.DocumentNode.SelectSingleNode("//head");
        if (head == null)
        {
            var html2 = doc.DocumentNode.SelectSingleNode("//html");
            if (html2 != null)
            {
                head = doc.CreateElement("head");
                html2.PrependChild(head);
            }
        }

        var injectHead =
            "<base href=\"" + System.Net.WebUtility.HtmlEncode(baseHref) + "\">" +
            "<style id=\"ane-banner-strip\">" +
            "[class*='cookie' i], [class*='consent' i], [id*='cookie' i], [id*='consent' i], " +
            "[class*='banner' i][style*='fixed'], dialog[open] { display: none !important; } " +
            "html, body { overflow: auto !important; }" +
            "</style>";

        if (head != null)
        {
            var injected = HtmlNode.CreateNode("<div>" + injectHead + "</div>");
            // Insert each child of injected into head
            foreach (var child in injected.ChildNodes.ToList())
            {
                child.ParentNode?.RemoveChild(child);
                head.PrependChild(child);
            }
        }

        return doc.DocumentNode.OuterHtml;
    }


    private static HtmlDocument LoadDocument(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        RemoveNoisyNodes(document);
        return document;
    }

    private static ReferenceListingSummary ExtractListingSummary(HtmlDocument document, Uri uri)
    {
        var title = FirstNonEmpty(
            GetMetaContent(document, "og:title"),
            GetMetaContent(document, "twitter:title"),
            NormalizeText(document.DocumentNode.SelectSingleNode("//h1")?.InnerText),
            NormalizeText(document.DocumentNode.SelectSingleNode("//title")?.InnerText));

        var description = FirstNonEmpty(
            GetMetaContent(document, "og:description"),
            GetMetaContent(document, "description"),
            NormalizeText(document.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", string.Empty)));

        var productSearchQuery = BuildProductSearchQuery(title);
        if (string.IsNullOrWhiteSpace(productSearchQuery))
            productSearchQuery = BuildProductSearchQuery(description);

        return new ReferenceListingSummary
        {
            Title = title,
            Description = description,
            ImageUrls = ExtractImages(document, uri).Take(6).ToList(),
            ProductSearchQuery = productSearchQuery,
        };
    }

    private static List<ReferencePriceCandidate> ExtractRankedPriceCandidates(HtmlDocument document)
    {
        return ExtractPriceCandidates(document)
            .GroupBy(c => $"{c.Selector}|{c.Price.ToString(CultureInfo.InvariantCulture)}|{c.Text}")
            .Select(g => g.OrderByDescending(c => c.Confidence).First())
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Price)
            .Take(18)
            .ToList();
    }

    private static ReferencePriceCandidate? SelectReferencePrice(IReadOnlyList<ReferencePriceCandidate> candidates, string? selectedSelector)
    {
        ReferencePriceCandidate? selected = null;
        if (!string.IsNullOrWhiteSpace(selectedSelector))
            selected = candidates.FirstOrDefault(c => string.Equals(c.Selector, selectedSelector, StringComparison.Ordinal));

        selected ??= SelectClearReferencePrice(candidates);
        if (selected != null)
            selected.IsAutoSelected = string.IsNullOrWhiteSpace(selectedSelector);

        return selected;
    }

    private static ReferenceListingSuggestedFilter BuildSuggestedFilter(Uri uri, string marketplace, ReferenceListingSummary listing, ReferencePriceCandidate? selected)
    {
        return new ReferenceListingSuggestedFilter
        {
            SearchTerm = listing.ProductSearchQuery,
            Marketplace = "all",
            MinPrice = 0,
            MaxPrice = selected?.Price,
            ReferenceUrl = uri.ToString(),
            ReferenceTitle = listing.Title,
            ReferenceMarketplace = marketplace,
            ReferencePrice = selected?.Price,
            ReferenceCurrency = selected?.Currency,
            ReferenceSelector = selected?.Selector,
        };
    }

    private static void MarkSelectedPreviewElement(List<ReferencePreviewElement> previewElements, ReferencePriceCandidate? selected)
    {
        if (selected?.Selector == null)
            return;

        foreach (var element in previewElements)
        {
            if (element.Selector == selected.Selector)
                element.IsAutoSelected = selected.IsAutoSelected;
        }
    }

    private static ReferencePriceCandidate? SelectClearReferencePrice(IReadOnlyList<ReferencePriceCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        var best = candidates[0];
        if (best.Confidence < 7)
            return null;

        if (candidates.Count == 1 || best.Confidence >= candidates[1].Confidence + 2)
            return best;

        var samePriceTopCandidates = candidates.TakeWhile(c => c.Confidence >= best.Confidence - 1).ToList();
        if (samePriceTopCandidates.Count > 1 && samePriceTopCandidates.All(c => Math.Abs(c.Price - best.Price) < 0.01))
            return best;

        return null;
    }

    private static List<ReferencePreviewElement> BuildPreviewElements(HtmlDocument document, IReadOnlyList<ReferencePriceCandidate> candidates, string title, string description)
    {
        var elements = new List<ReferencePreviewElement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddPreviewElement(elements, seen, new ReferencePreviewElement
        {
            Id = "title",
            Selector = "title",
            Role = "title",
            Text = title,
            Confidence = string.IsNullOrWhiteSpace(title) ? 0 : 6,
        });

        AddPreviewElement(elements, seen, new ReferencePreviewElement
        {
            Id = "description",
            Selector = "meta[name='description']",
            Role = "description",
            Text = description,
            Confidence = string.IsNullOrWhiteSpace(description) ? 0 : 3,
        });

        var candidateSelectors = candidates.ToDictionary(c => c.Selector, StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            AddPreviewElement(elements, seen, new ReferencePreviewElement
            {
                Id = $"price-{elements.Count + 1}",
                Selector = candidate.Selector,
                Role = "price",
                Text = candidate.Text,
                Price = candidate.Price,
                Currency = candidate.Currency,
                Confidence = candidate.Confidence,
                IsReferenceCandidate = true,
                IsAutoSelected = candidate.IsAutoSelected,
            });
        }

        foreach (var node in FindPreviewTextNodes(document))
        {
            var text = NormalizeText(node.InnerText);
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3 || text.Length > 220)
                continue;

            var selector = BuildSelector(node);
            var role = node.Name.StartsWith('h') ? "heading" : "text";
            var isCandidate = candidateSelectors.TryGetValue(selector, out var candidate);
            AddPreviewElement(elements, seen, new ReferencePreviewElement
            {
                Id = $"element-{elements.Count + 1}",
                Selector = selector,
                Role = isCandidate ? "price" : role,
                Text = text,
                Price = candidate?.Price,
                Currency = candidate?.Currency,
                Confidence = candidate?.Confidence ?? (role == "heading" ? 5 : 1),
                IsReferenceCandidate = isCandidate,
                IsAutoSelected = candidate?.IsAutoSelected == true,
            });
        }

        return elements.Where(e => !string.IsNullOrWhiteSpace(e.Text)).ToList();
    }

    private static IEnumerable<HtmlNode> FindPreviewTextNodes(HtmlDocument document)
    {
        return document.DocumentNode.SelectNodes("//h1|//h2|//h3|//*[@itemprop='name']|//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'title')]|//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'name')]|//p|//li")
            ?? Enumerable.Empty<HtmlNode>();
    }

    private static void AddPreviewElement(List<ReferencePreviewElement> elements, HashSet<string> seen, ReferencePreviewElement element)
    {
        if (string.IsNullOrWhiteSpace(element.Text))
            return;

        var key = $"{element.Selector}|{element.Text}";
        if (!seen.Add(key))
            return;

        if (string.IsNullOrWhiteSpace(element.Id))
            element.Id = $"element-{elements.Count + 1}";
        elements.Add(element);
    }

    private static List<ReferencePriceCandidate> ExtractPriceCandidates(HtmlDocument document)
    {
        var candidates = new List<ReferencePriceCandidate>();
        AddMetaPriceCandidates(document, candidates);

        foreach (var node in FindPriceCandidateNodes(document))
        {
            var text = NormalizeText(node.InnerText);
            if (string.IsNullOrWhiteSpace(text) || text.Length > 180)
                continue;

            var looksLikePriceField = LooksLikePriceNode(node);
            if (!looksLikePriceField && !ContainsCurrency(text))
                continue;

            if (node.ChildNodes.Count(n => n.NodeType == HtmlNodeType.Element) > 5 && !looksLikePriceField)
                continue;

            if (!TryParsePrice(text, looksLikePriceField, out var parsed))
                continue;

            var confidence = ScorePriceCandidate(node, text, parsed.Currency, looksLikePriceField);
            if (confidence <= 0)
                continue;

            candidates.Add(new ReferencePriceCandidate
            {
                Selector = BuildSelector(node),
                Text = text,
                Price = parsed.Amount,
                Currency = parsed.Currency,
                Confidence = confidence,
                Source = "markup",
            });
        }

        return candidates;
    }

    private static IEnumerable<HtmlNode> FindPriceCandidateNodes(HtmlDocument document)
    {
        return document.DocumentNode.SelectNodes("//body//*[not(self::script) and not(self::style) and not(self::noscript)]")
            ?? Enumerable.Empty<HtmlNode>();
    }

    private static void AddMetaPriceCandidates(HtmlDocument document, List<ReferencePriceCandidate> candidates)
    {
        var nodes = document.DocumentNode.SelectNodes("//meta[contains(translate(@property,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'price') or contains(translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'price') or contains(translate(@itemprop,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'price')]")
            ?? Enumerable.Empty<HtmlNode>();
        var currency = FirstNonEmpty(
            GetMetaContent(document, "product:price:currency"),
            GetMetaContent(document, "og:price:currency"),
            "EUR");

        foreach (var node in nodes)
        {
            var content = NormalizeText(node.GetAttributeValue("content", string.Empty));
            if (string.IsNullOrWhiteSpace(content) || string.Equals(content, currency, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryParsePrice($"{currency} {content}", true, out var parsed))
                continue;

            candidates.Add(new ReferencePriceCandidate
            {
                Selector = BuildSelector(node),
                Text = $"{content} {currency}".Trim(),
                Price = parsed.Amount,
                Currency = parsed.Currency,
                Confidence = 9,
                Source = "meta",
            });
        }
    }

    private static int ScorePriceCandidate(HtmlNode node, string text, string currency, bool looksLikePriceField)
    {
        var score = ContainsCurrency(text) || currency != "EUR" ? 4 : 1;
        if (looksLikePriceField)
            score += 4;

        var lowerText = text.ToLowerInvariant();
        if (lowerText.Contains("shipping") || lowerText.Contains("versand") || lowerText.Contains("liefer") || lowerText.Contains("vat") || lowerText.Contains("mwst"))
            score -= 3;

        if (lowerText.Contains("from") || lowerText.Contains("ab ") || lowerText.Contains("starting"))
            score += 1;

        if (node.GetAttributeValue("itemprop", string.Empty).Contains("price", StringComparison.OrdinalIgnoreCase))
            score += 3;

        return score;
    }

    private static bool LooksLikePriceNode(HtmlNode node)
    {
        var haystack = string.Join(' ',
            node.Name,
            node.GetAttributeValue("id", string.Empty),
            node.GetAttributeValue("class", string.Empty),
            node.GetAttributeValue("itemprop", string.Empty),
            node.GetAttributeValue("data-testid", string.Empty),
            node.GetAttributeValue("aria-label", string.Empty));

        return haystack.Contains("price", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("preis", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("amount", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("currentbid", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("buyitnow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePrice(string text, bool allowCurrencyMissing, out ParsedPrice parsed)
    {
        parsed = default;
        ParsedPrice? fallback = null;
        foreach (Match match in PriceRegex.Matches(text))
        {
            var currency = FirstNonEmpty(match.Groups["currency"].Value, match.Groups["currency2"].Value);
            var hasCurrency = !string.IsNullOrWhiteSpace(currency);

            if (!TryParseAmount(match.Groups["amount"].Value, out var amount) || amount <= 0 || amount > 100_000)
                continue;

            var candidate = new ParsedPrice(amount, NormalizeCurrency(currency));
            if (hasCurrency)
            {
                parsed = candidate;
                return true;
            }

            if (allowCurrencyMissing && fallback == null)
                fallback = candidate;
        }

        if (fallback != null)
        {
            parsed = fallback.Value;
            return true;
        }

        return false;
    }

    private static bool TryParseAmount(string value, out double amount)
    {
        var normalized = value.Replace("\u00a0", string.Empty).Replace(" ", string.Empty);
        var commaIndex = normalized.LastIndexOf(',');
        var dotIndex = normalized.LastIndexOf('.');

        if (commaIndex > dotIndex)
        {
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (dotIndex > commaIndex)
        {
            normalized = normalized.Replace(",", string.Empty);
            if (normalized.Length - dotIndex - 1 == 3)
                normalized = normalized.Replace(".", string.Empty);
        }
        else if (commaIndex >= 0)
        {
            var decimals = normalized.Length - commaIndex - 1;
            normalized = decimals is > 0 and <= 2 ? normalized.Replace(',', '.') : normalized.Replace(",", string.Empty);
        }

        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Equals("€", StringComparison.OrdinalIgnoreCase) || currency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            return "EUR";
        if (currency.Equals("$", StringComparison.OrdinalIgnoreCase) || currency.Equals("US$", StringComparison.OrdinalIgnoreCase))
            return "USD";
        if (currency.Equals("£", StringComparison.OrdinalIgnoreCase) || currency.Equals("GBP", StringComparison.OrdinalIgnoreCase))
            return "GBP";
        if (currency.Equals("CHF", StringComparison.OrdinalIgnoreCase))
            return "CHF";
        return currency.ToUpperInvariant();
    }

    private static bool ContainsCurrency(string text)
    {
        return text.Contains('€')
            || text.Contains('$')
            || text.Contains('£')
            || text.Contains("EUR", StringComparison.OrdinalIgnoreCase)
            || text.Contains("CHF", StringComparison.OrdinalIgnoreCase)
            || text.Contains("GBP", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProductSearchQuery(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var query = WebUtility.HtmlDecode(value);
        query = query.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? query;
        query = SearchNoiseRegex.Replace(query, string.Empty);
        query = SearchPriceNoiseRegex.Replace(query, string.Empty);
        query = NormalizeText(query).Trim('-', '|', ':', ',', '.', ' ');

        if (query.Length > 140)
            query = query[..140].Trim();

        return query;
    }

    private static List<string> ExtractImages(HtmlDocument document, Uri pageUri)
    {
        var urls = new List<string>();
        AddImage(urls, ResolveUrl(pageUri, GetMetaContent(document, "og:image")));
        AddImage(urls, ResolveUrl(pageUri, GetMetaContent(document, "twitter:image")));

        var imageNodes = document.DocumentNode.SelectNodes("//img[@src or @data-src]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in imageNodes)
        {
            var raw = FirstNonEmpty(node.GetAttributeValue("src", string.Empty), node.GetAttributeValue("data-src", string.Empty));
            AddImage(urls, ResolveUrl(pageUri, raw));
            if (urls.Count >= 6)
                break;
        }

        return urls;
    }

    private static void AddImage(List<string> urls, string url)
    {
        if (!string.IsNullOrWhiteSpace(url) && !urls.Contains(url, StringComparer.OrdinalIgnoreCase))
            urls.Add(url);
    }

    private static string ResolveUrl(Uri pageUri, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return Uri.TryCreate(pageUri, value, out var uri) ? uri.ToString() : string.Empty;
    }

    private static string GetMetaContent(HtmlDocument document, string name)
    {
        var escaped = name.Replace("'", "&apos;");
        var node = document.DocumentNode.SelectSingleNode($"//meta[translate(@property,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{escaped.ToLowerInvariant()}' or translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{escaped.ToLowerInvariant()}' or translate(@itemprop,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{escaped.ToLowerInvariant()}']");
        return NormalizeText(node?.GetAttributeValue("content", string.Empty));
    }

    private static void RemoveNoisyNodes(HtmlDocument document)
    {
        var nodes = document.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//template") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in nodes)
            node.Remove();
    }

    private static string BuildSelector(HtmlNode node)
    {
        var parts = new Stack<string>();
        var current = node;
        while (current is { NodeType: HtmlNodeType.Element } && current.Name != "#document")
        {
            var name = current.Name.ToLowerInvariant();
            if (name == "html")
            {
                parts.Push(name);
                break;
            }

            var id = current.GetAttributeValue("id", string.Empty);
            if (!string.IsNullOrWhiteSpace(id) && id.Length <= 80)
            {
                parts.Push($"{name}#{CssIdentifier(id)}");
                break;
            }

            var index = 1;
            var sibling = current.PreviousSibling;
            while (sibling != null)
            {
                if (sibling.NodeType == HtmlNodeType.Element && sibling.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
                    index++;
                sibling = sibling.PreviousSibling;
            }

            parts.Push($"{name}:nth-of-type({index})");
            current = current.ParentNode;
        }

        return string.Join(" > ", parts);
    }

    private static string CssIdentifier(string value)
    {
        return Regex.Replace(value, @"[^a-zA-Z0-9_-]", "_");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return WhitespaceRegex.Replace(WebUtility.HtmlDecode(HtmlEntity.DeEntitize(value)).Trim(), " ");
    }

    private readonly record struct ParsedPrice(double Amount, string Currency);
}

public static class ReferenceListingUrlPolicy
{
    public static readonly string[] SupportedRootDomains =
    [
        "ebay.de", "ebay.com", "ebay.at", "ebay.ch", "ebay.co.uk", "ebay.fr", "ebay.it", "ebay.es", "ebay.nl", "ebay.be", "ebay.pl", "ebay.ie", "ebay.ca", "ebay.com.au",
        "cardmarket.com",
        "kleinanzeigen.de",
        "vinted.de", "vinted.com", "vinted.fr", "vinted.it",
        "willhaben.at",
        "marktplaats.nl",
        "shpock.com",
    ];

    public static bool TryCreateSupportedUri(string? url, out Uri uri)
    {
        return TryCreateWebUri(url, out uri);
    }

    public static bool TryCreateWebUri(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
            return false;

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return false;

        uri = parsed;
        return true;
    }

    public static string DetectMarketplace(string host)
    {
        host = host.ToLowerInvariant();
        if (HostMatches(host, "cardmarket.com"))
            return "Cardmarket";
        if (SupportedRootDomains.Where(d => d.StartsWith("ebay", StringComparison.OrdinalIgnoreCase)).Any(d => HostMatches(host, d)))
            return "Ebay";
        if (HostMatches(host, "kleinanzeigen.de"))
            return "Kleinanzeigen";
        if (SupportedRootDomains.Where(d => d.StartsWith("vinted", StringComparison.OrdinalIgnoreCase)).Any(d => HostMatches(host, d)))
            return "Vinted";
        if (HostMatches(host, "willhaben.at"))
            return "Willhaben";
        if (HostMatches(host, "marktplaats.nl"))
            return "Marktplaats";
        if (HostMatches(host, "shpock.com"))
            return "Shpock";
        return "External";
    }

    public static bool IsSupportedMarketplace(string host)
    {
        return SupportedRootDomains.Any(root => HostMatches(host, root));
    }

    public static async Task<bool> HostResolvesToPublicAddress(Uri uri, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        return addresses.Length > 0 && addresses.All(address => !IsPrivateAddress(address));
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || bytes[0] == 0xfc || bytes[0] == 0xfd;
        }

        return true;
    }

    private static bool HostMatches(string host, string rootDomain)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        rootDomain = rootDomain.TrimEnd('.').ToLowerInvariant();
        return host == rootDomain || host.EndsWith($".{rootDomain}", StringComparison.OrdinalIgnoreCase);
    }
}

public class ReferenceListingPreviewRequest
{
    public string Url { get; set; } = string.Empty;
    public string? Locale { get; set; }
    public string? SelectedSelector { get; set; }
    public List<string>? AdditionalSelectors { get; set; }
}

public class ReferenceListingPreviewResponse
{
    public string Url { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Marketplace { get; set; } = string.Empty;
    public ReferenceListingSummary Listing { get; set; } = new();
    public List<ReferencePriceCandidate> PriceCandidates { get; set; } = [];
    public List<ReferencePreviewElement> PreviewElements { get; set; } = [];
    public ReferencePriceCandidate? SelectedReference { get; set; }
    public bool RequiresReferencePriceSelection { get; set; }
    public ReferenceListingSuggestedFilter SuggestedFilter { get; set; } = new();
    public string? ScreenshotPngBase64 { get; set; }
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
    public int FullPageHeight { get; set; }
    public string? SandboxHtml { get; set; }
    public List<RenderedNode> RenderedNodes { get; set; } = [];
    public string? PrimarySelectorXPath { get; set; }
}

public class RenderedNode
{
    public string XPath { get; set; } = string.Empty;
    public string Css { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double? Price { get; set; }
    public string? Currency { get; set; }
    public double Confidence { get; set; }
}

public class RenderedReferenceListing
{
    public string Html { get; set; } = string.Empty;
    public string ScreenshotPngBase64 { get; set; } = string.Empty;
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
    public int FullPageHeight { get; set; }
    public List<RenderedNode> Nodes { get; set; } = [];
    public bool IsPartial { get; set; }
}

public class ReferenceListingSummary
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public string ProductSearchQuery { get; set; } = string.Empty;
}

public class ReferencePriceCandidate
{
    public string Selector { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public int Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsAutoSelected { get; set; }
}

public class ReferencePreviewElement
{
    public string Id { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double? Price { get; set; }
    public string? Currency { get; set; }
    public int Confidence { get; set; }
    public bool IsReferenceCandidate { get; set; }
    public bool IsAutoSelected { get; set; }
}

public class ReferenceListingSuggestedFilter
{
    public string SearchTerm { get; set; } = string.Empty;
    public string Marketplace { get; set; } = "all";
    public double MinPrice { get; set; }
    public double? MaxPrice { get; set; }
    public string ReferenceUrl { get; set; } = string.Empty;
    public string ReferenceTitle { get; set; } = string.Empty;
    public string ReferenceMarketplace { get; set; } = string.Empty;
    public double? ReferencePrice { get; set; }
    public string? ReferenceCurrency { get; set; }
    public string? ReferenceSelector { get; set; }
}
