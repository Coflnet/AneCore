using Coflnet.Ane;
using NUnit.Framework;

namespace AneCore.Tests;

public class FigureOfferRulesTests
{
    [TestCase("Figurka anime manga")]
    [TestCase("Figurki anime manga")]
    [TestCase("Figur Naruto")]
    [TestCase("Figuren Naruto")]
    [TestCase("Figurine Naruto")]
    [TestCase("Figurines Naruto")]
    [TestCase("Figurka anime manga mit Anleitung")]
    [TestCase("Figurka anime manga with manual")]
    public void ExplicitPhysicalFigureNounsAndIncludedManualsRetainNativeOffers(string title)
    {
        Assert.That(FigureOfferRules.IsFigureTitle(title), Is.True);
        Assert.That(FigureOfferRules.CanExtract(title, Platform.OLX, "pl:synthetic-case"), Is.True);
    }

    [TestCase("Manga Band 1 Figurenzeichnung")]
    [TestCase("Figurka anime PDF")]
    [TestCase("Figurka anime STL download")]
    [TestCase("Figurka anime Reparaturservice")]
    [TestCase("Figurka anime nur Box")]
    [TestCase("Figurka anime Anleitung")]
    [TestCase("Anleitung für Figurka anime")]
    [TestCase("Figurka LEGO Minifigur")]
    [TestCase("LEGO Minifigur anime")]
    [TestCase("Suche Figurka anime")]
    [TestCase("Figurka anime wanted")]
    [TestCase("Figurkarium Manga Band 1")]
    public void PaperDigitalServicePackagingAndLegoAreNotReroutedAsGenericFigures(string title) =>
        Assert.That(FigureOfferRules.CanExtract(title, Platform.Kleinanzeigen, "synthetic"), Is.False);

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("0")]
    [TestCase("Unknown")]
    public void MissingNativeIdNeverFallsBackToSharedTitleIdentity(string? id) =>
        Assert.That(FigureOfferRules.CanExtract("Figurka anime manga", Platform.Vinted, id), Is.False);

    [TestCase(Platform.Unknown)]
    [TestCase((Platform)999)]
    public void UnknownMarketplaceCannotSupplyNativeOfferIdentity(Platform platform) =>
        Assert.That(FigureOfferRules.CanExtract("Figurka anime manga", platform, "synthetic"), Is.False);
}
