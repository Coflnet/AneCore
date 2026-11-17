using Coflnet.Ane.ReferenceListings;

namespace Coflnet.Ane.Tests;

public class ReferencePriceParsingTests
{
    private static readonly ReferenceListingPreviewService Service = new();

    [TestCase("€ 1,299.00", 1299)]
    [TestCase("1,299.00 €", 1299)]
    [TestCase("1,299 €", 1299)]
    [TestCase("£12,345.67", 12345.67)]
    [TestCase("1.299,00 €", 1299)]
    [TestCase("1 299,00 €", 1299)]
    [TestCase("1299 €", 1299)]
    [TestCase("12,50 €", 12.5)]
    [TestCase("12.50 €", 12.5)]
    [TestCase("1.299 €", 1299)]
    public void ThousandsAndDecimalSeparatorsParseInAllCommonLayouts(string text, double expected)
    {
        var html = $"<html><body><span class='price'>{text}</span></body></html>";
        var result = Service.Build(new Uri("https://shop.example.com/p"), html, null);
        Assert.That(result.SelectedReference?.Price, Is.EqualTo(expected));
    }
}
