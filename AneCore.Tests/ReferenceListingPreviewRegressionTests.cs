using Coflnet.Ane.ReferenceListings;

namespace Coflnet.Ane.Tests;

public class ReferenceListingPreviewRegressionTests
{
    private static readonly ReferenceListingPreviewService Service = new();
    private static readonly Uri Url = new("https://shop.example.com/p/1");

    private static string Page(string body, string head = "")
        => $"<html><head><title>T</title>{head}</head><body><h1>Item</h1>{body}</body></html>";

    private static ReferenceListingPreviewResponse Preview(string body, string? selector = null)
        => Service.Build(Url, Page(body), selector);

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
}
