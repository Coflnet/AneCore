using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class BahcoMetricSpannerCatalogTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void ManufacturerSpannerArticlesReachTheExistingResaleAdmissionPath()
    {
        var additions = Catalog.Where(p => p.Source == "seed:manufacturer-verified-2026-10-28-r35").ToList();
        Assert.That(additions, Has.Count.EqualTo(111));
        foreach (var row in additions)
        {
            Assert.That(Catalog.Count(p => p.Id == row.Id || p.Brand == "Bahco" && p.Model == row.Model), Is.EqualTo(1), row.Id);
            Assert.That(row.PossibleAttributes["article_number"], Is.EquivalentTo(new[] { row.Model }));
            Assert.That(row.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "hand_tool" }));
            Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "article_number", "product_kind", "tool_type", "length_mm", "wrench_size_mm" }));
            Assert.That(row.PossibleAttributes["wrench_size_mm"], Has.Count.EqualTo(1));
            Assert.That(Matcher.Match(row.Name)?.Id, Is.EqualTo(row.Id), row.Name);
            foreach (var alias in row.Aliases)
            {
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(row.Id), alias);
                Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(row.Id), alias);
                Assert.That(ResaleCatalog.IsHandToolOffer(alias), Is.True, alias);
                Assert.That(ResaleCatalog.Match(alias + " gebraucht")?.Id, Is.EqualTo(row.Id), alias);
            }
        }
    }

    // Manufacturer table facts; surrounding seller wording in the boundary cases below is synthetic.
    [TestCase("111M-13", "bahco-111m-13", "169", "13")]
    [TestCase("1952M-13", "bahco-1952m-13", "175", "13")]
    [TestCase("1RM-13", "bahco-1rm-13", "178", "13")]
    [TestCase("41RM-13", "bahco-41rm-13", "154", "13")]
    [TestCase("10RM-13", "bahco-10rm-13", "108", "13")]
    [TestCase("111M-5.5", "bahco-111m-5-5", "112", "5.5")]
    public void SameOpeningDifferentBodiesAndLengthsRemainSeparate(string article, string id, string length, string opening)
    {
        var row = ResaleCatalog.Match("Bahco " + article);
        Assert.That(row?.Id, Is.EqualTo(id));
        Assert.That(row!.PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { length }));
        Assert.That(row.PossibleAttributes["wrench_size_mm"], Is.EquivalentTo(new[] { opening }));
    }

    [TestCase("Bahco 111M")]
    [TestCase("Bahco 1952M")]
    [TestCase("Bahco 1RM-20")]
    [TestCase("Bahco SB111M-13")]
    [TestCase("Bahco SB1952M-13")]
    [TestCase("Bahco 111M-13 SB")]
    [TestCase("Bahco 1RM/SH8")]
    [TestCase("Bahco 41RM/SH8")]
    [TestCase("Bahco 10RM/SH6")]
    [TestCase("Bahco 111M-13 Bedienungsanleitung")]
    [TestCase("Bahco 111M-13 gesucht")]
    [TestCase("Bahco 111M-13 1952M-13")]
    [TestCase("Bahco 1RM-13 und 41RM-13")]
    public void FamiliesUnverifiedPackagesSetsAndMultipleToolsDoNotClaimOneArticle(string title) =>
        Assert.That(ResaleCatalog.Match(title), Is.Null);

    [TestCase("Set mit Bahco 111M-13")]
    [TestCase("Bahco 1952M-13 Satz")]
    [TestCase("2 Stück Bahco 1RM-13")]
    [TestCase("Ersatzteile für Bahco 41RM-13")]
    public void PhysicalReferencesKeepIndependentOfferAdmission(string title)
    {
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
        Assert.That(ResaleCatalog.Match(title), Is.Null);
    }

    [Test]
    public void IncludedCarryingCaseDoesNotInventAFactoryToolSet() =>
        Assert.That(ResaleCatalog.Match("Bahco 111M-13 mit Koffer")?.Id, Is.EqualTo("bahco-111m-13"));
}
