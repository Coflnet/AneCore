using Coflnet.Ane.KnownProducts;
using Coflnet.Ane.Parts;

namespace AneCore.Tests;

[TestFixture]
public class ManufacturerBearingCatalogTests
{
    [Test]
    public void ExactManufacturerArticlesEnrichExistingPartFactsWithoutEnteringTheProductSeed()
    {
        var references = ManufacturerBearingCatalog.References;
        Assert.That(references, Has.Count.EqualTo(109));
        Assert.That(references.Select(r => r.Article).Distinct().Count(), Is.EqualTo(109));
        Assert.That(KnownProductSeed.LoadAll(true).Any(p => p.Brand == "SKF"), Is.False);
        foreach (var reference in references)
        {
            var title = $"Kugellager SKF {reference.Article}";
            Assert.That(ManufacturerBearingCatalog.Match(title), Is.EqualTo(reference), title);
            Assert.That(ResaleCatalog.Match(title), Is.Null, title);
            var facts = PartTextExtractors.Extract(title, null)!;
            Assert.That(facts.Class.Name, Is.EqualTo(PartClasses.Bearing), title);
            Assert.That(facts.Designation!.Value, Is.EqualTo(reference.Article), title);
            Assert.That(facts.InnerDiameterMm!.Value, Is.EqualTo(reference.InnerDiameterMm), title);
            Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(reference.OuterDiameterMm), title);
            Assert.That(facts.WidthMm!.Value, Is.EqualTo(reference.WidthMm), title);
            Assert.That(facts.InnerDiameterMm.Evidence, Does.Contain(ManufacturerBearingCatalog.SourceUrl), title);
            Assert.That(reference.InnerDiameterMm, Is.GreaterThan(0).And.LessThan(reference.OuterDiameterMm), title);
            Assert.That(reference.WidthMm, Is.GreaterThan(0), title);
            Assert.That(reference.SourcePage, Is.InRange(40, 46), title);
        }
    }

    [TestCase("SKF 6010", "6010")]
    [TestCase("SKF 6228", "6228")]
    [TestCase("skf   604", "604")]
    public void ExactManufacturerTitleNeedsNoRedundantBearingNoun(string title, string article)
    {
        var facts = PartTextExtractors.Extract(title, null)!;
        Assert.That(facts.Class.Name, Is.EqualTo(PartClasses.Bearing));
        Assert.That(facts.Designation!.Value, Is.EqualTo(article));
        Assert.That(facts.InnerDiameterMm, Is.Not.Null);
    }

    [TestCase("SKF 6010 Anleitung")]
    [TestCase("SKF 6010 Aufkleber")]
    [TestCase("Lagerabzieher SKF 6010")]
    [TestCase("Suche SKF 6010")]
    [TestCase("SKF 6010 P6")]
    [TestCase("SKF 6010 und SKF 6210")]
    [TestCase("NSK 6010")]
    public void ArticleReferenceAloneDoesNotAdmitOtherObjectsOrUnknownVariants(string title) =>
        Assert.That(PartTextExtractors.Extract(title, null), Is.Null);

    [TestCase("6010", 50, 80, 16)]
    [TestCase("6228", 140, 250, 42)]
    [TestCase("6312", 60, 130, 31)]
    [TestCase("6418", 90, 225, 54)]
    [TestCase("604", 4, 12, 4)]
    public void ManufacturerCellsRemainExact(string article, double inner, double outer, double width)
    {
        var reference = ManufacturerBearingCatalog.Match($"SKF {article}")!;
        Assert.That(reference.InnerDiameterMm, Is.EqualTo(inner));
        Assert.That(reference.OuterDiameterMm, Is.EqualTo(outer));
        Assert.That(reference.WidthMm, Is.EqualTo(width));
    }

    [TestCase("NSK 6010")]
    [TestCase("6010")]
    [TestCase("SKF 6010-2RSH")]
    [TestCase("SKF 6010 2RS1")]
    [TestCase("SKF 6010 2 Z")]
    [TestCase("SKF 6010 C3")]
    [TestCase("SKF 6010 C 3")]
    [TestCase("SKF 6010 M")]
    [TestCase("SKF 6010 JEM")]
    [TestCase("SKF 6010 P6")]
    [TestCase("SKF 6010NR")]
    [TestCase("SKF 6010-UNKNOWN")]
    [TestCase("SKF 6034")]
    [TestCase("SKF 6010 und SKF 6210")]
    [TestCase("SKF 6010 / 6210")]
    [TestCase("SKF 6010 / 6210C3")]
    public void DifferentBrandsVariantsUnknownAndMultipleArticlesDoNotBorrowManufacturerDimensions(string reference)
    {
        Assert.That(ManufacturerBearingCatalog.Match(reference), Is.Null);
        var facts = PartTextExtractors.Extract("Kugellager " + reference, null)!;
        Assert.That(facts.InnerDiameterMm, Is.Null);
        Assert.That(facts.OuterDiameterMm, Is.Null);
        Assert.That(facts.WidthMm, Is.Null);
    }

    [Test]
    public void SellerDimensionsStayFirstAndTheExistingCommonFallbackRemainsAvailable()
    {
        var seller = PartTextExtractors.Extract("Kugellager SKF 6010", "51x81x17 mm")!;
        Assert.That(seller.InnerDiameterMm!.Value, Is.EqualTo(51));
        Assert.That(seller.OuterDiameterMm!.Value, Is.EqualTo(81));
        Assert.That(seller.WidthMm!.Value, Is.EqualTo(17));
        Assert.That(seller.InnerDiameterMm.Evidence, Does.StartWith("dimensions:"));
        foreach (var title in new[] { "Kugellager 6203-2RS", "Kugellager NSK 6203", "Kugellager SKF 6203 C3" })
        {
            var common = PartTextExtractors.Extract(title, null)!;
            Assert.That(common.InnerDiameterMm!.Value, Is.EqualTo(17), title);
            Assert.That(common.OuterDiameterMm!.Value, Is.EqualTo(40), title);
            Assert.That(common.WidthMm!.Value, Is.EqualTo(12), title);
            Assert.That(common.InnerDiameterMm.Evidence, Does.StartWith("from designation"), title);
        }
    }
}
