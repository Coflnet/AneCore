using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class ListingUrlTests
{
    [Test]
    public void GetUrlForListing_Vinted_LinksToTheItemOnVinted()
    {
        // Regression: Vinted had no case and fell through to the ane.deals placeholder,
        // which is neither an offer link for users nor a page the scraper can recheck.
        var url = Listing.GetUrlForListing("de-DE", Platform.Vinted, "10146770085");
        Assert.That(url, Is.EqualTo("https://www.vinted.de/items/10146770085"));
    }

    [TestCase("m2447703109")]
    [TestCase("a1517165634")]
    public void GetUrlForListing_MarktplaatsIdOnly_KeepsTheRequiredSlug(string id)
    {
        // Verified against marktplaats.nl on 2026-09-28: with a slug the redirect answers 301 to the
        // offer (410 when it is gone), without a slug it answers 404 for every id.
        var url = Listing.GetUrlForListing("nl-NL", Platform.Marktplaats, id);
        Assert.That(url, Is.EqualTo($"https://www.marktplaats.nl/v/redirect/redirect/{id}-test"));
    }

    [Test]
    public void GetUrlForListing_MarktplaatsPathId_UsesThePath()
    {
        var url = Listing.GetUrlForListing("nl-NL", Platform.Marktplaats, "/v/foo/bar/m1502063062-title");
        Assert.That(url, Is.EqualTo("https://www.marktplaats.nl/v/foo/bar/m1502063062-title"));
    }

    [TestCase(Platform.Vinted)]
    [TestCase(Platform.Marktplaats)]
    [TestCase(Platform.Kleinanzeigen)]
    [TestCase(Platform.Willhaben)]
    [TestCase(Platform.Ebay)]
    public void GetUrlForListing_CrawledMarketplaces_NeverLinkToAneDeals(Platform platform)
    {
        var url = Listing.GetUrlForListing("de-DE", platform, "123");
        Assert.That(new Uri(url).Host, Does.Not.EndWith("ane.deals"));
    }
}
