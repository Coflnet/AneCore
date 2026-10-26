using Coflnet.Ane;

namespace AneCore.Tests;

public class FurnitureOfferRulesTests
{
    [TestCase("Ikea Hemnes Schrank", "Schlafzimmer", true)]
    [TestCase("IKEA MALM Kommode / Nachttisch 2 Schubladen weiß", "Schlafzimmer", true)]
    [TestCase("IKEA Billy Regal weiß", "Wohnzimmer", true)]
    [TestCase("IKEA Schrank", "Küche & Esszimmer", true)]
    [TestCase("IKEA Schrank", "Elektronik", false)]
    [TestCase("IKEA Schrank", "", false)]
    [TestCase("Hemnes Schrank", "Schlafzimmer", false)]
    [TestCase("Essgruppe: 4 Samt-Drehstühle & Esstisch", "Küche & Esszimmer", false)]
    [TestCase("Suche IKEA Hemnes Schrank", "Schlafzimmer", false)]
    [TestCase("IKEA Hemnes Schrank zu vermieten", "Schlafzimmer", false)]
    [TestCase("Ersatzteil für IKEA Hemnes Schrank", "Schlafzimmer", false)]
    [TestCase("IKEA Hemnes Schrank Tür", "Schlafzimmer", false)]
    [TestCase("Bezug für IKEA Sofa", "Wohnzimmer", false)]
    [TestCase("IKEA Puppenhaus Schrank Miniatur", "Schlafzimmer", false)]
    [TestCase("IKEA Hemnes Schrank und MALM Kommode", "Schlafzimmer", false)]
    [TestCase("2x IKEA Hemnes Schrank", "Schlafzimmer", false)]
    [TestCase("10x IKEA Hemnes Schrank", "Schlafzimmer", false)]
    [TestCase("IKEA Schrank wie IKEA Hemnes", "Schlafzimmer", false)]
    public void OnlyCategoryAnchoredWholeFurnitureEnters(string title, string category, bool expected) =>
        Assert.That(FurnitureOfferRules.CanExtract(title, category), Is.EqualTo(expected));

    [Test]
    public void MissingIdAndOtherPlatformsDoNotSupplyOfferIdentity()
    {
        var listing = new Listing { Title = "Ikea Hemnes Schrank", Category = "Schlafzimmer", Platform = Platform.Kleinanzeigen };
        Assert.That(FurnitureOfferRules.CanExtract(listing), Is.False);
        listing.Id = "0";
        Assert.That(FurnitureOfferRules.CanExtract(listing), Is.False);
        listing.Id = "3553335759";
        Assert.That(FurnitureOfferRules.CanExtract(listing), Is.True);
        listing.Platform = Platform.Ebay;
        Assert.That(FurnitureOfferRules.CanExtract(listing), Is.False);
        Assert.That(FurnitureOfferRules.CanExtract(null, null), Is.False);
        Assert.DoesNotThrow(() => FurnitureOfferRules.CanExtract("IKEA Schrank \ud800", "Schlafzimmer"));
    }

    [Test]
    public void SellerExplicitMixedOfferDoesNotBecomeAWholeItem() =>
        Assert.That(FurnitureOfferRules.CanExtract("IKEA Hemnes Schrank", "Schlafzimmer", "Verkaufe Schrank und Kommode als Set."), Is.False);
}
