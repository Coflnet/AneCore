using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoPartPageRulesTests
{
    [TestCase("lego-part-3001-used")]
    [TestCase("lego-part-3069b-new")]
    public void ThePageWithoutAColourIsColourless(string seoId) =>
        Assert.That(LegoPartPageRules.IsColourlessPartPage(seoId, null), Is.True);

    [TestCase("lego-part-3001-red-used")]
    [TestCase("lego-part-3001-light-bluish-gray-new")]
    public void AColouredPageStaysComparable(string seoId) =>
        Assert.That(LegoPartPageRules.IsColourlessPartPage(seoId, new Dictionary<string, string> { ["product_kind"] = "lego_part", ["lego_color_id"] = "4" }), Is.False);

    [Test]
    public void ThePartAttributesDecideWhenTheIdIsUnknown()
    {
        var withoutColour = new Dictionary<string, string> { ["product_kind"] = "lego_part", ["part_number"] = "3001" };
        Assert.That(LegoPartPageRules.IsColourlessPartPage(null, withoutColour), Is.True);
        Assert.That(LegoPartPageRules.IsColourlessPartPage(null, withoutColour, "lego_part"), Is.True);
    }

    [TestCase("lego-10874-used")]
    [TestCase("lego-offer-abc-used")]
    [TestCase(null)]
    public void OtherPagesAreNoPartPages(string? seoId) =>
        Assert.That(LegoPartPageRules.IsColourlessPartPage(seoId, new Dictionary<string, string> { ["product_kind"] = "lego_set" }), Is.False);
}
