using Coflnet.Ane.ReferenceListings;

namespace Coflnet.Ane.Tests;

/// <summary>Regression tests for candidate filtering, selector resolution and parsing of the reference-listing preview.</summary>
public class ReferenceListingPreviewRegressionTests
{
    private static readonly ReferenceListingPreviewService Service = new();
    private static readonly Uri Url = new("https://shop.example.com/p/1");

    private static string Page(string body, string head = "")
        => $"<html><head><title>T</title>{head}</head><body><h1>Item</h1>{body}</body></html>";

    private static ReferenceListingPreviewResponse Preview(string body, string? selector = null)
        => Service.Build(Url, Page(body), selector);

    private static double[] Prices(string body) => Preview(body).PriceCandidates.Select(c => c.Price).ToArray();

    // Pages and the selectors the previous version offered for them; saved picks must keep resolving.
    private static readonly Dictionary<string, string> LegacyCases = new()
    {
    ["woo"] = Page("<div class='summary entry-summary'><p class='price'><span class='woocommerce-Price-amount amount'><bdi>12,00&nbsp;<span class='woocommerce-Price-currencySymbol'>&euro;</span></bdi></span></p></div>"),
    ["itemprop-wrap"] = Page("<div class='product'><div itemprop='price' class='price'><span>12,00 €</span></div></div>"),
    ["pricebox"] = Page("<div class='price-box'><span class='price'>12,00 €</span></div>"),
    ["inkl-versand-1"] = Page("<div class='info'><span class='price'>Preis inkl. Versand 30 €</span></div>"),
    ["inkl-versand-2"] = Page("<div class='info'><p class='price'>30 € inkl. Versand</p></div>"),
    ["incl-delivery"] = Page("<div class='info'><div class='product-price'>Price incl. delivery: £25.00</div></div>"),
    ["zzgl-in-price"] = Page("<div class='info'><div class='product-price'>49,99 € <small>inkl. MwSt., zzgl. Versand</small></div></div>"),
    ["versandkostenfrei"] = Page("<div class='info'><span class='price'>19,99 € versandkostenfrei</span></div>"),
    ["grundpreis"] = Page("<div class='info'><div class='price'>12,99 € <span class='base-price'>(25,98 € / 1 kg)</span></div></div>"),
    ["carousel-ancestor"] = Page("<div class='product-carousel-layout'><div class='gallery'><img src='https://img.example.com/a.jpg'/></div><div class='buybox'><span class='price'>99,00 €</span></div></div>"),
    ["body-class"] = "<html><head><title>T</title></head><body class='single-product has-related-products'><h1>Item</h1><div class='buybox'><span class='price'>99,00 €</span></div></body></html>",
    ["aria-related"] = Page("<section aria-label='Product details and related accessories'><div class='buybox'><span class='price'>99,00 €</span></div></section>"),
    ["aside-plain"] = Page("<main><aside class='buybox'><div class='box'><strong>99,00 €</strong></div></aside></main>"),
    ["comma-thousands-1"] = Page("<div class='x'><span class='price'>€ 1,299.00</span></div>"),
    ["comma-thousands-2"] = Page("<div class='x'><span class='price'>1,299.00 €</span></div>"),
    ["dot-thousands"] = Page("<div class='x'><span class='price'>1.299,00 €</span></div>"),
    ["many-prices"] = Page(string.Join("", Enumerable.Range(1, 12).Select(i => $"<div class='v{i}'><span class='price'>{i * 10},00 €</span></div>"))),
    ["dup-text"] = Page("<header><div class='sticky'><span class='price'>30 €</span></div></header><div class='buy'><span class='price'>30 €</span></div>"),
    ["nextimg"] = Page("<img src='/_next/image?url=%2Fp%2Fa.jpg&amp;w=640'/><img src='/_next/image?url=%2Fp%2Fb.jpg&amp;w=640'/><img src='https://cdn.example.com/products/logo-hoodie-black.jpg'/><img src='https://cdn.example.com/books/illustrations-of-birds.jpg'/><img src='/img/placeholder.gif' data-src='https://cdn.example.com/p/real.jpg'/>"),
    };

    private static readonly Dictionary<string, (string Selector, double Price)[]> LegacySelectors = new()
    {
        ["woo"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > p:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1) > p:nth-of-type(1) > span:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1) > p:nth-of-type(1) > span:nth-of-type(1) > bdi:nth-of-type(1)", 12)],
        ["itemprop-wrap"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 12)],
        ["pricebox"] = [("html > body:nth-of-type(1) > div:nth-of-type(1)", 12), ("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 12)],
        ["inkl-versand-1"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 30)],
        ["inkl-versand-2"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > p:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 30)],
        ["incl-delivery"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1)", 25), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 25)],
        ["zzgl-in-price"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1)", 49.99), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 49.99)],
        ["versandkostenfrei"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 19.99), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 19.99)],
        ["grundpreis"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1)", 12.99), ("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 25.98), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 12.99)],
        ["carousel-ancestor"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(2) > span:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(2)", 99)],
        ["body-class"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 99)],
        ["aria-related"] = [("html > body:nth-of-type(1) > section:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > section:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > section:nth-of-type(1) > div:nth-of-type(1)", 99)],
        ["aside-plain"] = [("html > body:nth-of-type(1) > main:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > main:nth-of-type(1) > aside:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > main:nth-of-type(1) > aside:nth-of-type(1) > div:nth-of-type(1)", 99), ("html > body:nth-of-type(1) > main:nth-of-type(1) > aside:nth-of-type(1) > div:nth-of-type(1) > strong:nth-of-type(1)", 99)],
        ["comma-thousands-1"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 1299), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 1299)],
        ["comma-thousands-2"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 1299), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 1299)],
        ["dot-thousands"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 1299), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 1299)],
        ["many-prices"] = [("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 10), ("html > body:nth-of-type(1) > div:nth-of-type(2) > span:nth-of-type(1)", 20), ("html > body:nth-of-type(1) > div:nth-of-type(3) > span:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(4) > span:nth-of-type(1)", 40), ("html > body:nth-of-type(1) > div:nth-of-type(5) > span:nth-of-type(1)", 50), ("html > body:nth-of-type(1) > div:nth-of-type(6) > span:nth-of-type(1)", 60), ("html > body:nth-of-type(1) > div:nth-of-type(7) > span:nth-of-type(1)", 70), ("html > body:nth-of-type(1) > div:nth-of-type(8) > span:nth-of-type(1)", 80), ("html > body:nth-of-type(1) > div:nth-of-type(9) > span:nth-of-type(1)", 90), ("html > body:nth-of-type(1) > div:nth-of-type(10) > span:nth-of-type(1)", 100), ("html > body:nth-of-type(1) > div:nth-of-type(11) > span:nth-of-type(1)", 110), ("html > body:nth-of-type(1) > div:nth-of-type(12) > span:nth-of-type(1)", 120), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 10), ("html > body:nth-of-type(1) > div:nth-of-type(2)", 20), ("html > body:nth-of-type(1) > div:nth-of-type(3)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(4)", 40), ("html > body:nth-of-type(1) > div:nth-of-type(5)", 50), ("html > body:nth-of-type(1) > div:nth-of-type(6)", 60)],
        ["dup-text"] = [("html > body:nth-of-type(1) > header:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(1) > span:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > header:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > header:nth-of-type(1) > div:nth-of-type(1)", 30), ("html > body:nth-of-type(1) > div:nth-of-type(1)", 30)],
    };

    public static IEnumerable<TestCaseData> LegacySelectorCases()
        => LegacySelectors.SelectMany(kv => kv.Value.Select(s => new TestCaseData(kv.Key, s.Selector, s.Price).SetName($"{kv.Key} {s.Selector[^40..]}")));

    [TestCaseSource(nameof(LegacySelectorCases))]
    public void SelectorOfferedBeforeStillResolves(string name, string selector, double expectedPrice)
        => Assert.That(Service.Build(Url, LegacyCases[name], selector).SelectedReference?.Price, Is.EqualTo(expectedPrice));

    [Test]
    public void SavedCssSelectorResolvesEvenWhenCandidateIsExcludedFromRanking()
    {
        var html = Page("<section id='similar-ads'><div><span class='price'>19 €</span></div></section><span class='price'>30 €</span>");
        var selector = "section#similar-ads > div:nth-of-type(1) > span:nth-of-type(1)";
        Assert.That(Service.Build(Url, html, null).PriceCandidates.Any(c => c.Selector == selector), Is.False);
        Assert.That(Service.Build(Url, html, selector).SelectedReference?.Price, Is.EqualTo(19));
    }

    [Test]
    public void WooCommerceNestedPriceAutoSelectsOutermostOfEqualConfidence()
    {
        var result = Service.Build(Url, LegacyCases["woo"], null);
        Assert.That(result.RequiresReferencePriceSelection, Is.False);
        Assert.That(result.SelectedReference?.Selector, Does.EndWith("div:nth-of-type(1) > p:nth-of-type(1)"));
        Assert.That(result.PriceCandidates, Has.Count.EqualTo(1));
    }

    [Test]
    public void NestedChainKeepsHighestConfidenceNode()
    {
        var result = Service.Build(Url, LegacyCases["itemprop-wrap"], null);
        Assert.That(result.RequiresReferencePriceSelection, Is.False);
        Assert.That(result.SelectedReference?.Selector, Is.EqualTo("html > body:nth-of-type(1) > div:nth-of-type(1) > div:nth-of-type(1)"));
    }

    [Test]
    public void NestedChainWithEqualConfidenceKeepsOutermost()
    {
        var result = Service.Build(Url, LegacyCases["pricebox"], null);
        Assert.That(result.PriceCandidates.Select(c => c.Selector), Is.EqualTo(new[] { "html > body:nth-of-type(1) > div:nth-of-type(1)" }));
    }

    [TestCase("<span class='price'>Preis inkl. Versand 30 €</span>", 30)]
    [TestCase("<p class='price'>30 € inkl. Versand</p>", 30)]
    [TestCase("<div class='product-price'>Price incl. delivery: £25.00</div>", 25)]
    [TestCase("<span class='price'>19,99 € versandkostenfrei</span>", 19.99)]
    [TestCase("<div class='product-price'>49,99 € <small>inkl. MwSt., zzgl. Versand</small></div>", 49.99)]
    [TestCase("<span class='price'>Free delivery 15 €</span>", 15)]
    [TestCase("<span class='price'>Auslieferung ab Lager 20 €</span>", 20)]
    [TestCase("<span class='price'>Abholung Porto 30 €</span>", 30)]
    public void ItemPriceWordingNearShippingStaysACandidate(string body, double expected)
        => Assert.That(Prices(body), Does.Contain(expected));

    [TestCase("<span class='price'>+ Versand ab 0,49 €</span>")]
    [TestCase("<span class='price'>zzgl. Versand 4,90 €</span>")]
    [TestCase("<span class='price'>plus shipping 4,90 €</span>")]
    [TestCase("<span class='price'>Shipping: €3.50</span>")]
    [TestCase("<span class='price'>4,90 € Versand</span>")]
    public void ShippingCostIsNeverACandidate(string body)
        => Assert.That(Prices(body), Is.Empty);

    [Test]
    public void ShippingClauseIsStrippedFromAPriceContainer()
    {
        var candidate = Preview("<div class='price'>30 € + Versand ab 0,49 €</div>").PriceCandidates.Single();
        Assert.That(candidate.Price, Is.EqualTo(30));
        Assert.That(candidate.Text, Is.EqualTo("30 €"));
    }

    [TestCase("carousel-ancestor", 99)]
    [TestCase("body-class", 99)]
    [TestCase("aria-related", 99)]
    public void LayoutWrappersAndLoneKeywordsDoNotHideTheMainPrice(string name, double expected)
        => Assert.That(Service.Build(Url, LegacyCases[name], null).SelectedReference?.Price, Is.EqualTo(expected));

    [Test]
    public void PlainTextPriceInsideAnAsideBuyBoxIsACandidate()
        => Assert.That(Prices("<main><aside class='buybox'><div><strong>99,00 €</strong></div></aside></main>"), Does.Contain(99));

    [Test]
    public void FooterInsideACardIsNotAPageFooter()
        => Assert.That(Prices("<article><footer class='price-row'>19,99 €</footer></article>"), Does.Contain(19.99));

    [Test]
    public void MatchedAncestorContainingTheListingHeadingIsKept()
        => Assert.That(Service.Build(Url, "<html><body><div class='related-wrapper'><h1>Real item</h1><span class='price'>30 €</span></div></body></html>", null)
            .PriceCandidates.Select(c => c.Price), Does.Contain(30));

    [TestCase("<section id='vap-smlrads'><span class='price'>5 €</span></section>")]
    [TestCase("<ul><li class='ad-listitem'><article class='aditem'><span class='price'>5 €</span></article></li></ul>")]
    [TestCase("<div data-gaevent='VIP,RelatedItemClick'><span class='price'>5 €</span></div>")]
    [TestCase("<section class='you-may-also-like'><span class='price'>5 €</span></section>")]
    [TestCase("<nav><span class='price'>5 €</span></nav>")]
    [TestCase("<footer><span class='price'>5 €</span></footer>")]
    [TestCase("<div class='product-carousel'><span class='price'>5 €</span><span class='price'>6 €</span></div>")]
    public void EachNonListingRegionRuleExcludesItsPricesOnItsOwn(string region)
        => Assert.That(Prices(region + "<span class='price'>30 €</span>"), Is.EqualTo(new[] { 30d }));

    [Test]
    public void ContainerWithTwoPricesIsDroppedAndLeavesRemain()
    {
        var candidates = Preview("<div class='sale'><span class='price'>30 €</span> <span class='old'>45 €</span></div>").PriceCandidates;
        Assert.That(candidates.Select(c => c.Price), Is.EquivalentTo(new[] { 30d, 45d }));
        Assert.That(candidates.All(c => ReferencePriceTextProbe(c.Text) <= 1), Is.True);
    }

    private static int ReferencePriceTextProbe(string text) => text.Count(c => c == '€');

    [Test]
    public void WrapperAroundARecommendationCardDoesNotRepeatItsPrice()
        => Assert.That(Prices("<div class='slot'><div class='aditem'><p class='price'>150 €</p></div></div><span class='price'>30 €</span>"), Is.EqualTo(new[] { 30d }));

    [Test]
    public void UnitPriceInsideThePriceContainerDoesNotWin()
    {
        var result = Service.Build(Url, LegacyCases["grundpreis"], null);
        Assert.That(result.PriceCandidates.Select(c => c.Price), Is.EqualTo(new[] { 12.99 }));
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(12.99));
    }

    [TestCase("<span class='base-price'>(25,98 € / 1 kg)</span>")]
    [TestCase("<span class='price'>Grundpreis: 25,98 €/kg</span>")]
    [TestCase("<span class='price'>(3,50 € / 100 g)</span>")]
    public void UnitPriceIsNeverACandidate(string body)
        => Assert.That(Prices(body), Is.Empty);

    [Test]
    public void PerPieceItemPriceIsKept()
        => Assert.That(Prices("<span class='price'>12,99 € / Stück</span>"), Is.EqualTo(new[] { 12.99 }));

    [Test]
    public void RenderedNodesPassTheSameFiltersAndDeduplicateAgainstMarkup()
    {
        var html = Page("<span id='main' class='price'>30 €</span><section class='similar-ads'><span id='rec'>19 €</span></section><span id='ship'>+ Versand ab 0,49 €</span><div id='outer' class='price'><span id='unit'>(25,98 € / 1 kg)</span></div>");
        var render = new RenderedReferenceListing { Html = html, Nodes =
        [
            new RenderedNode { XPath = "//*[@id='main']", Text = "30 €", Price = 30, Currency = "EUR", Confidence = 8 },
            new RenderedNode { XPath = "//*[@id='rec']", Text = "19 €", Price = 19, Currency = "EUR", Confidence = 8 },
            new RenderedNode { XPath = "//*[@id='ship']", Text = "+ Versand ab 0,49 €", Price = 0.49, Currency = "EUR", Confidence = 8 },
            new RenderedNode { XPath = "//*[@id='unit']", Text = "(25,98 € / 1 kg)", Price = 25.98, Currency = "EUR", Confidence = 8 },
        ] };
        var result = Service.BuildFromRender(Url, render, null, null);
        Assert.That(result.PriceCandidates.Select(c => c.Price), Is.EqualTo(new[] { 30d }));
    }

    [Test]
    public void RenderedOnlyNodeIsStillMerged()
    {
        var render = new RenderedReferenceListing { Html = Page("<span>x</span>"), Nodes =
            [new RenderedNode { XPath = "/html/body/span[9]", Text = "42 €", Price = 42, Currency = "EUR", Confidence = 9 }] };
        Assert.That(Service.BuildFromRender(Url, render, null, null).PriceCandidates.Single().Selector, Is.EqualTo("/html/body/span[9]"));
    }

    [Test]
    public void ImagesDoNotMergeDistinctPhotosThatDifferInQuery()
    {
        var images = Preview("<img src='/_next/image?url=%2Fp%2Fa.jpg&amp;w=640'/><img src='/_next/image?url=%2Fp%2Fb.jpg&amp;w=640'/><img src='/image.php?id=1'/><img src='/image.php?id=2'/><img src='/p/A.jpg'/><img src='/p/a.jpg'/>").Listing.ImageUrls;
        Assert.That(images, Has.Count.EqualTo(6));
    }

    [Test]
    public void ImagesWithRenditionParametersCollapse()
        => Assert.That(Preview("<img src='/p/a.jpg?w=640&q=80'/><img src='/p/a.jpg?w=200'/>").Listing.ImageUrls, Has.Count.EqualTo(1));

    [Test]
    public void ProductPhotosNamedLikeChromeAreKept()
    {
        var images = Preview("<img src='https://cdn.example.com/products/logo-hoodie-black.jpg'/><img src='https://cdn.example.com/books/illustrations-of-birds.jpg'/>").Listing.ImageUrls;
        Assert.That(images, Has.Count.EqualTo(2));
    }

    [Test]
    public void PlaceholderSrcFallsBackToDataSrcAndSrcset()
    {
        var images = Preview("<img src='/img/placeholder.gif' data-src='https://cdn.example.com/p/real.jpg'/><img src='/img/logo.svg' srcset='https://cdn.example.com/p/two.jpg 1x, https://cdn.example.com/p/two@2x.jpg 2x'/>").Listing.ImageUrls;
        Assert.That(images, Is.EqualTo(new[] { "https://cdn.example.com/p/real.jpg", "https://cdn.example.com/p/two.jpg" }));
    }

    [TestCase("<div class='product'><div class='price-head-item'><span class='price'>99,00 €</span></div></div>", 99)]
    [TestCase("<ul><li class='download-item'><span class='price'>9,99 €</span></li></ul>", 9.99)]
    [TestCase("<div class='lead-item'><span class='price'>5,00 €</span></div>", 5)]
    [TestCase("<div class='thread-item'><span class='price'>6,00 €</span></div>", 6)]
    public void AdItemIsMatchedAsWholeTokenOnly(string body, double expected)
        => Assert.That(Prices(body), Does.Contain(expected));

    [TestCase("<span class='price'>£25.00 + £3.50 postage</span>", 25)]
    [TestCase("<span class='price'>€30.00 + €4.99 delivery</span>", 30)]
    [TestCase("<span class='price'>4,95 € Versandkosten</span>", -1)]
    [TestCase("<span class='price'>4,95 € Lieferkosten</span>", -1)]
    public void PostageAndDeliveryCostsAreStripped(string body, double expected)
    {
        var prices = Prices(body);
        if (expected < 0)
            Assert.That(prices, Is.Empty);
        else
            Assert.That(prices, Is.EqualTo(new[] { expected }));
    }

    [TestCase("<span class='price'>19,99 € Versand kostenlos</span>", 19.99)]
    [TestCase("<span class='price'>30 € Versand inklusive</span>", 30)]
    [TestCase("<span class='price'>€25.00 shipping included</span>", 25)]
    [TestCase("<span class='price'>12 € Versand gratis</span>", 12)]
    public void ShippingIncludedWordingKeepsTheItemPrice(string body, double expected)
        => Assert.That(Prices(body), Is.EqualTo(new[] { expected }));

    [TestCase("<span class='price'>4,99 € / m</span>", 4.99)]
    [TestCase("<span class='price'>12,99 €/kg</span>", 12.99)]
    [TestCase("<span class='price'>2,00 € / l</span>", 2)]
    public void LoneMeasurePriceIsTheItemPrice(string body, double expected)
        => Assert.That(Prices(body), Is.EqualTo(new[] { expected }));

    [Test]
    public void RrpNextToThePriceDoesNotMakeASliderRegion()
    {
        var html = "<html><body><div class='product-slider-wrap'><span class='price'>99,00 €</span><span class='uvp'>UVP 129,00 €</span></div><h1>Item</h1></body></html>";
        Assert.That(Service.Build(Url, html, null).PriceCandidates.Select(c => c.Price), Does.Contain(99));
    }

    [Test]
    public void BaseUnitEqualsFormIsNotTheItemPrice()
    {
        var result = Preview("<div class='price'>12,99 € <span class='base-price'>(1 kg = 25,98 €)</span></div>");
        Assert.That(result.PriceCandidates.Select(c => c.Price), Is.EqualTo(new[] { 12.99 }));
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(12.99));
    }

    [Test]
    public void RenderedPriceIsReparsedFromTheText()
    {
        var render = new RenderedReferenceListing { Html = Page("<span>x</span>"), Nodes =
            [new RenderedNode { XPath = "/html/body/span[9]", Text = "€ 1,299.00", Price = 1.29, Currency = "EUR", Confidence = 9 }] };
        Assert.That(Service.BuildFromRender(Url, render, null, null).PriceCandidates.Single().Price, Is.EqualTo(1299));
    }

    [Test]
    public void RenderedPriceFallsBackToScraperValueWithoutText()
    {
        var render = new RenderedReferenceListing { Html = Page("<span>x</span>"), Nodes =
            [new RenderedNode { XPath = "/html/body/span[9]", Text = "", Price = 42, Currency = "USD", Confidence = 9 }] };
        var candidate = Service.BuildFromRender(Url, render, null, null).PriceCandidates.Single();
        Assert.That((candidate.Price, candidate.Currency), Is.EqualTo((42d, "USD")));
    }

    [Test]
    public void MarkedUpRecommendationCardsAreNotExemptedByTheirOwnItemprop()
        => Assert.That(Prices("<span class='price' itemprop='price'>30 €</span><section class='related-products'><div class='card'><span itemprop='price' class='price'>19 €</span></div></section>"),
            Is.EqualTo(new[] { 30d }));

    [Test]
    public void DataUriPlaceholderFallsBackToDataSrc()
        => Assert.That(Preview("<img src='data:image/gif;base64,R0lGODlhAQABAAAAACw=' data-src='https://cdn.example.com/p/real.jpg'/>").Listing.ImageUrls,
            Is.EqualTo(new[] { "https://cdn.example.com/p/real.jpg" }));

    [Test]
    public void PreviewSurvivesCandidatesSharingASelector()
    {
        var result = Preview("<div id='p' class='price'>10 €</div><div id='p' class='price'>20 €</div>");
        Assert.That(result.PriceCandidates, Has.Count.EqualTo(2));
        Assert.That(result.PreviewElements, Is.Not.Empty);
    }

    [Test]
    public void PreviewSurvivesIdsThatSanitiseToTheSameSelector()
        => Assert.DoesNotThrow(() => Preview("<div id='a b' class='price'>10 €</div><div id='a_b' class='price'>20 €</div>"));

    private static string Description(string description, string title = "Phone X 16 GB")
        => Service.Build(Url, $"<html><head><meta property='og:title' content='{title}'/><meta property='og:description' content='{description}'/></head><body></body></html>", null).Listing.Description;

    [TestCase("Selling my phone, works...,Phone X 16 GB in Berlin - Mitte", "Selling my phone, works...")]
    [TestCase("Selling my phone, works..., Phone X 16 GB in Berlin", "Selling my phone, works...")]
    [TestCase("Selling my phone, works..., Phone X 16 GB", "Selling my phone, works...")]
    public void DescriptionDropsAppendedTitle(string input, string expected)
        => Assert.That(Description(input), Is.EqualTo(expected));

    [TestCase("Includes charger, Phone X 16 GB")]
    [TestCase("Accessories for the Phone X 16 GB")]
    [TestCase("Case, Phone X 16 GB in Berlin, works well")]
    public void DescriptionLegitimatelyMentioningTheTitleIsNotTruncated(string input)
        => Assert.That(Description(input), Is.EqualTo(input));

    private static string[] Images(string body) => Preview(body).Listing.ImageUrls.ToArray();

    [Test]
    public void ImagesExcludeRecommendationCards()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "kleinanzeigen-ad-with-recommendations.html"));
        var images = Service.Build(new Uri("https://www.kleinanzeigen.de/s-anzeige/x/1"), html, null).Listing.ImageUrls;
        Assert.That(images, Has.Count.EqualTo(1));
        Assert.That(images[0], Does.Contain("own-1"));
    }

    [Test]
    public void ImagesKeepOwnGalleryCarouselIncludingLazyImages()
        => Assert.That(Images("<meta itemprop='price' content='30'/><div class='gallery-carousel swiper'><img src='https://img.example.com/own/a.jpg'/><img src='data:image/gif;base64,R0lGODlhAQABAAAAACw=' data-src='https://img.example.com/own/b.jpg'/></div><div class='buybox'><span class='price'>30 €</span></div>"),
            Is.EqualTo(new[] { "https://img.example.com/own/a.jpg", "https://img.example.com/own/b.jpg" }));

    [Test]
    public void ImagesExcludeSimilarSectionCarouselWithPrices()
        => Assert.That(Images("<div class='gallery'><img src='https://img.example.com/own/a.jpg'/></div><div class='product-carousel'><div><img src='https://img.example.com/o/1.jpg'/><span>10 €</span></div><div><img src='https://img.example.com/o/2.jpg'/><span>20 €</span></div></div>"),
            Is.EqualTo(new[] { "https://img.example.com/own/a.jpg" }));

    [Test]
    public void ImagesExcludeNavAndFooter()
        => Assert.That(Images("<nav><img src='https://img.example.com/nav/n.jpg'/></nav><div class='gallery'><img src='https://img.example.com/own/a.jpg'/></div><footer><img src='https://img.example.com/foot/f.jpg'/></footer>"),
            Is.EqualTo(new[] { "https://img.example.com/own/a.jpg" }));

    [Test]
    public void ImagesWithoutRegionMarkersAreAllKept()
        => Assert.That(Images("<div><img src='https://img.example.com/a.jpg'/></div><section><img src='https://img.example.com/b.jpg'/></section>"),
            Is.EqualTo(new[] { "https://img.example.com/a.jpg", "https://img.example.com/b.jpg" }));
}
