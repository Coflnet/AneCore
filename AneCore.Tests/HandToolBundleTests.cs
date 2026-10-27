using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

public class HandToolBundleTests
{
    [TestCase("KNIPEX 8701250 8603180")]
    [TestCase("KNIPEX 87 01 250 86 03 180")]
    [TestCase("KNIPEX 87 01 250 / 86 03 180")]
    public void TwoKnownArticlesWithOneManufacturerAreNotOneTool(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);

    [TestCase("KNIPEX 87 01 250 Wasserpumpenzange 250 mm")]
    [TestCase("KNIPEX 87 01 250 8701250")]
    public void DimensionsOrRepeatedSameArticleDoNotCreateAnExtraTool(string title)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo("knipex-8701250"));

    [TestCase("Werkzeugset mit KNIPEX 86 04 100")]
    [TestCase("Set mit Wera 8000 A")]
    [TestCase("Toolset with Wera 8000 A")]
    [TestCase("Kit with KNIPEX 86 04 100")]
    [TestCase("Satz mit Bahco 8224")]
    [TestCase("Bundle with Wera 8000 A")]
    [TestCase("Set Wera 8000 A")]
    [TestCase("KNIPEX 86 04 100 Set")]
    [TestCase("Bahco 8224 8225 Satz")]
    [TestCase("KNIPEX 86 02 180 T")]
    [TestCase("Backen für KNIPEX 86 04 100")]
    [TestCase("2 Stück Bahco 8224")]
    [TestCase("Wera 8000 A und Wera 8000 B")]
    public void PhysicalToolReferencesRemainOffersWithoutClaimingOneVerifiedBody(string title)
    {
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
        Assert.That(ResaleCatalog.Match(title), Is.Null);
    }

    [TestCase("KNIPEX 86 04 100")]
    [TestCase("Wera 8000 A mit Anleitung")]
    [TestCase("Wera 8000 A inklusive Anleitung")]
    public void PrimaryToolWithIncludedExtrasStillHasAnOfferReference(string title)
        => Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);

    [TestCase("Suche Wera 8000 A")]
    [TestCase("Wanted KNIPEX 86 04 100")]
    [TestCase("Reparaturservice Bahco 8224")]
    [TestCase("Wera 8000 A repair service")]
    [TestCase("KNIPEX 86 04 100 Bedienungsanleitung")]
    [TestCase("Wera 8000 A manual")]
    [TestCase("Handy mit KNIPEX 86 04 100")]
    [TestCase("Kamera with Wera 8000 A")]
    [TestCase("KNIPEX Cobra")]
    [TestCase("KNIPEX 86 04 1000")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("LEGO 75305")]
    public void NonPhysicalAndMerelyIncludedToolReferencesDoNotAdmitAnOffer(string? title)
        => Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False);

    [TestCase("KNIPEX Seitenschneider")]
    [TestCase("Wera Schraubendreher")]
    [TestCase("Bahco Rollgabelschlüssel")]
    [TestCase("2x KNIPEX Seitenschneider")]
    [TestCase("Set mit Wera Schraubendreher")]
    [TestCase("Wera Schraubendreher mit Tasche")]
    public void PrimaryBrandedToolWithoutArticleIsOnlyAnIndependentOffer(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
        Assert.That(ResaleCatalog.IsHandToolOffer(title, categoryPath: new[] { "Bekleidung & Accessoires", "Schuhe" }), Is.False);
    }

    [TestCase("KNIPEX")]
    [TestCase("Acme Seitenschneider")]
    [TestCase("Knipexig Seitenschneider")]
    [TestCase("KNIPEX Seitenschneidertasche")]
    [TestCase("Kamera mit KNIPEX Seitenschneider")]
    [TestCase("Handy with Wera Schraubendreher")]
    [TestCase("Tasche für KNIPEX Seitenschneider")]
    [TestCase("KNIPEX Seitenschneider Tasche")]
    [TestCase("Suche KNIPEX Seitenschneider")]
    [TestCase("Wera Schraubendreher Reparaturservice")]
    [TestCase("Handbuch Bahco Rollgabelschlüssel")]
    [TestCase("KNIPEX Seitenschneider Poster")]
    public void GenericToolAdmissionRequiresAPhysicalPrimaryToolFromAKnownBrand(string title)
        => Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False);

    [Test]
    public void ToolReferenceHonorsKnownIncompatibleCategoryAndDoesNotInferContents()
    {
        Assert.That(ResaleCatalog.IsHandToolOffer("Wera 8000 A", categoryPath: new[] { "Bekleidung & Accessoires", "Schuhe" }), Is.False);
        Assert.That(ResaleCatalog.IsHandToolOffer("Set Wera 8000 A", description: "Teile fehlen"), Is.True);
    }
}
