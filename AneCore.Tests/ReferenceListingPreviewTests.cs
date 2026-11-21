using Coflnet.Ane.ReferenceListings;

namespace Coflnet.Ane.Tests;

public class ReferenceListingPreviewTests
{
    private readonly ReferenceListingPreviewService service = new();
    private static readonly Uri Url = new("https://example.com/item");
    private const string Html = "<html><head><title>Camera</title></head><body><span id='price'>100,00 €</span><span id='shipping'>5,00 €</span><span id='foreign'>£8.00</span></body></html>";
    private static readonly Uri CardmarketUrl = new("https://www.cardmarket.com/en/Pokemon/Products/Singles/151/Charizard-ex-V3-MEW199");

    [Test]
    public void CardmarketUsesProductMetricsAndSuggestsTheCardNumber()
    {
        const string html = """
            <html><head><meta property="og:title" content="Charizard ex | Cardmarket"></head><body>
              <h1>Charizard ex (MEW 199)<span class="h4 text-muted"> 151 - Singles</span></h1>
              <dl class="labeled row mx-auto g-0">
                <dt>From</dt><dd>220,00 €</dd>
                <dt>Price Trend</dt><dd><span>377,84 €</span></dd>
                <dt>30-days average price</dt><dd>356,40 €</dd>
                <dt>7-days average price</dt><dd>356,40 €</dd>
                <dt>1-day average price</dt><dd>–</dd>
              </dl>
              <dl><dt>Price Trend</dt><dd>10 €</dd></dl>
              <span id="shipping">5,00 €</span><span id="seller-price">199,00 €</span>
            </body></html>
            """;
        var result = service.Build(CardmarketUrl, html, null);

        Assert.That(result.PriceCandidates.Select(c => c.Selector), Is.EqualTo(new[]
            { "cardmarket:trend", "cardmarket:avg30", "cardmarket:avg7", "cardmarket:from" }));
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(377.84));
        Assert.That(result.SelectedReference?.IsAutoSelected, Is.True);
        Assert.That(result.Listing.Title, Is.EqualTo("Charizard ex (MEW 199)"));
        Assert.That(result.SuggestedFilter.SearchTerm, Is.EqualTo("Charizard ex 199"));
        Assert.That(result.SuggestedFilter.ReferenceCurrency, Is.EqualTo("EUR"));
        Assert.That(service.Build(CardmarketUrl, html, "//*[@id='seller-price']").SelectedReference?.Price, Is.EqualTo(199));
        Assert.That(service.Build(CardmarketUrl, html, "cardmarket:trend", ["cardmarket:avg30"]).SelectedReference, Is.Null);
    }

    [TestCase("Preis-Trend", "30-Tages-Durchschnitt")]
    [TestCase("Tendance des prix", "Prix moyen 30 jours")]
    [TestCase("Tendencia de precio", "Precio medio 30 dias")]
    [TestCase("Tendenza di prezzo", "Prezzo medio 30 giorni")]
    public void SavedCardmarketMetricResolvesAcrossLocalesAndRowChanges(string trend, string avg30)
    {
        var html = $"<dl class='labeled'><dt>{avg30}</dt><dd>1.356,40&nbsp;€</dd><dt>{trend}</dt><dd>377,84 €</dd></dl>";
        var result = service.Build(CardmarketUrl, html, "cardmarket:avg30");

        Assert.That(result.SelectedReference?.Price, Is.EqualTo(1356.4));
        Assert.That(result.SelectedReference?.Text, Does.StartWith(avg30));
        Assert.That(service.Build(CardmarketUrl, html, "cardmarket:avg7").SelectedReference, Is.Null);
        Assert.That(service.Build(CardmarketUrl, html, "cardmarket:manual").SelectedReference, Is.Null);
    }

    [Test]
    public void NoCardmarketTrendDoesNotAutoSelectTheLowestAskingPrice()
    {
        const string html = "<dl class='labeled'><dt>From</dt><dd>£80.00</dd><dt>Price Trend</dt><dd>–</dd></dl>";
        var result = service.Build(CardmarketUrl, html, null);

        Assert.That(result.SelectedReference, Is.Null);
        Assert.That(result.RequiresReferencePriceSelection, Is.True);
        Assert.That(result.PriceCandidates.Single().Currency, Is.EqualTo("GBP"));
        Assert.That(service.Build(CardmarketUrl, "<span class='price'>80 €</span>", null).PriceCandidates, Is.Empty);
        Assert.That(service.Build(new Uri("https://www.cardmarket.com/en/Pokemon/Cards/Charizard-ex"), html, null).PriceCandidates, Is.Empty);
    }

    [Test]
    public void CardmarketChallengeDoesNotBecomeASuggestedSearch()
    {
        var result = service.Build(CardmarketUrl, "<html><head><title>Just a moment...</title></head></html>", null);
        Assert.That(result.PriceCandidates, Is.Empty);
        Assert.That(result.SuggestedFilter.SearchTerm, Is.Empty);
    }

    [Test]
    public void ExplicitXPathReadsTheSelectedPublicElement()
    {
        var result = service.Build(Url, Html, "//*[@id='shipping']");
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(5));
        Assert.That(result.SelectedReference?.Selector, Is.EqualTo("//*[@id='shipping']"));
    }

    [TestCase("1000.00 €", 1000)]
    [TestCase("1.250,50 €", 1250.5)]
    [TestCase("€ 12345.67", 12345.67)]
    public void UngroupedLargePricesAreNotTruncated(string text, double expected)
        => Assert.That(service.Build(Url, $"<html><body><span id='price'>{text}</span></body></html>", "//*[@id='price']").SelectedReference?.Price, Is.EqualTo(expected));

    [TestCase("100,00 zł", "PLN")]
    [TestCase("1200 Kč", "CZK")]
    [TestCase("1500 DKK", "DKK")]
    [TestCase("1500 SEK", "SEK")]
    [TestCase("1500 HUF", "HUF")]
    [TestCase("1500 RON", "RON")]
    [TestCase("1500 BGN", "BGN")]
    [TestCase("1500 NOK", "NOK")]
    public void BrowserSupportedCurrenciesRemainUsableOnTheServer(string text, string currency)
        => Assert.That(service.Build(Url, $"<html><body><span id='price'>{text}</span></body></html>", "//*[@id='price']").SelectedReference?.Currency, Is.EqualTo(currency));

    [Test]
    public void ExactPublicDomTextWinsOverIncorrectRenderedPrice()
    {
        var render = new RenderedReferenceListing { Html = "<html><body><span id='price'>1000 PLN</span></body></html>",
            Nodes = [new RenderedNode { XPath = "//*[@id='price']", Text = "1000 PLN", Price = 100, Currency = "EUR", Confidence = 100 }] };
        var result = service.BuildFromRender(Url, render, "//*[@id='price']", null);
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(1000));
        Assert.That(result.SelectedReference?.Currency, Is.EqualTo("PLN"));
        Assert.That(result.PriceCandidates.Single(c => c.Selector == "//*[@id='price']").Price, Is.EqualTo(1000));
    }

    [TestCase("//*[@id='missing']")]
    [TestCase("//*[")]
    [TestCase("//span")]
    public void MissingInvalidOrAmbiguousSelectionDoesNotChooseAnotherPrice(string selector)
        => Assert.That(service.Build(Url, Html, selector).SelectedReference, Is.Null);

    [Test]
    public void ExtraSelectionsAreDistinctAndExcludePrimary()
    {
        var result = service.Build(Url, Html, "//*[@id='price']", ["//*[@id='price']", "//*[@id='shipping']", "//*[@id='shipping']"]);
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(105));
    }

    [TestCase("//*[@id='missing']")]
    [TestCase("//*[@id='foreign']")]
    public void MissingOrMixedCurrencyExtraDoesNotProducePartialSum(string extra)
        => Assert.That(service.Build(Url, Html, "//*[@id='price']", [extra]).SelectedReference, Is.Null);

    [Test]
    public void ExplicitRenderedSelectionOutsideRankedCandidatesStillResolves()
    {
        var render = new RenderedReferenceListing { Html = Html, Nodes = Enumerable.Range(0, 80)
            .Select(i => new RenderedNode { XPath = $"//*[@id='node{i}']", Text = $"{i + 1} €", Price = i + 1, Currency = "EUR", Confidence = 100 - i }).ToList() };
        Assert.That(service.BuildFromRender(Url, render, "//*[@id='node79']", null).SelectedReference?.Price, Is.EqualTo(80));
    }

    [Test]
    public void ListingPageCandidatesIgnoreRecommendationsShippingAndContainers()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "kleinanzeigen-ad-with-recommendations.html"));
        var result = service.Build(new Uri("https://www.kleinanzeigen.de/s-anzeige/phone-example/1-173-1"), html, null);

        var candidates = result.PriceCandidates;
        Assert.That(candidates.Select(c => c.Price).Distinct().ToArray(), Is.EqualTo(new[] { 30d }), "only the main listing price may remain");
        Assert.That(candidates.Any(c => c.Text.Contains("Versand")), Is.False);
        Assert.That(candidates.All(c => c.Text.Count(ch => ch == '€') <= 1), Is.True);
        Assert.That(candidates.Select(c => (c.Price, c.Currency, c.Text)).Distinct().Count(), Is.EqualTo(candidates.Count));
        Assert.That(candidates.Any(c => c.Selector.Contains("viewad-price")), Is.True);
        Assert.That(result.RequiresReferencePriceSelection, Is.False);
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(30));
    }

    [Test]
    public void ImagesSkipSiteChromeAndRenditionDuplicates()
    {
        const string html = "<html><head><meta property='og:image' content='https://img.example.com/a/photo1?rule=$_59.JPG'/></head><body>" +
            "<img src='https://static.example.com/static/img/common/logo.svg'/>" +
            "<img src='https://static.example.com/icons/categories/all.png'/>" +
            "<img src='https://img.example.com/a/photo1?rule=$_2.AUTO'/>" +
            "<img src='https://img.example.com/a/photo2?rule=$_2.AUTO'/></body></html>";
        var images = service.Build(Url, html, null).Listing.ImageUrls;
        Assert.That(images, Is.EqualTo(new[] { "https://img.example.com/a/photo1?rule=$_59.JPG", "https://img.example.com/a/photo2?rule=$_2.AUTO" }));
    }

    [Test]
    public void DescriptionDropsAppendedTitleAndCity()
    {
        const string html = "<html><head><meta property='og:title' content='Phone X 16 GB'/>" +
            "<meta property='og:description' content='Selling my phone, works...,Phone X 16 GB in Berlin - Mitte'/></head><body></body></html>";
        Assert.That(service.Build(Url, html, null).Listing.Description, Is.EqualTo("Selling my phone, works..."));
    }

    [TestCase("https://user:password@example.com/item")]
    [TestCase("https://example.com:8443/item")]
    [TestCase("file:///etc/passwd")]
    public void UnsafeUrlFormsAreRejected(string url)
        => Assert.That(ReferenceListingUrlPolicy.TryCreateWebUri(url, out _), Is.False);

    [TestCase("http://127.0.0.1/item")]
    [TestCase("http://[::ffff:127.0.0.1]/item")]
    [TestCase("http://100.64.0.1/item")]
    [TestCase("http://0.0.0.0/item")]
    public async Task InternalTargetsAreRejected(string url)
        => Assert.That(await ReferenceListingUrlPolicy.HostResolvesToPublicAddress(new Uri(url), CancellationToken.None), Is.False);
}
