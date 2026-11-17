using Coflnet.Ane.ReferenceListings;

namespace Coflnet.Ane.Tests;

public class ReferenceListingPreviewTests
{
    private readonly ReferenceListingPreviewService service = new();
    private static readonly Uri Url = new("https://example.com/item");
    private const string Html = "<html><head><title>Camera</title></head><body><span id='price'>100,00 €</span><span id='shipping'>5,00 €</span><span id='foreign'>£8.00</span></body></html>";

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
