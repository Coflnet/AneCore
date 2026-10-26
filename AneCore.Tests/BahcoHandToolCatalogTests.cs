using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class BahcoHandToolCatalogTests
{
    [Test]
    public void ExactManufacturerArticlesKeepDistinctBodiesAndKnownLengths()
    {
        var rows = KnownProductSeed.LoadAll(includeExpandedCatalog: true).Where(p => p.Brand == "Bahco").ToList();
        Assert.That(rows, Has.Count.EqualTo(30));
        foreach (var row in rows)
        {
            Assert.That(ResaleCatalog.Match("Bahco " + row.Model)?.Id, Is.EqualTo(row.Id));
            Assert.That(ResaleCatalog.Match("Bahco " + row.Model.Replace(" ", ""))?.Id, Is.EqualTo(row.Id));
            Assert.That(row.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "hand_tool" }));
            Assert.That(row.PossibleAttributes["tool_type"], Is.EquivalentTo(new[] { "adjustable_wrench" }));
            Assert.That(row.PossibleAttributes["article_number"], Is.EquivalentTo(new[] { row.Model }));
            Assert.That(row.PossibleAttributes.Keys, Does.Not.Contain("package_scope"));
            Assert.That(row.PossibleAttributes.Keys, Does.Not.Contain("battery_count"));
        }
        Assert.That(rows.Single(p => p.Id == "bahco-8070").PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { "155" }));
        Assert.That(rows.Single(p => p.Id == "bahco-9070p").PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { "158" }));
    }

    [TestCase("Bahco 8070 IP", "bahco-8070")]
    [TestCase("Bahco 8070 C IP", "bahco-8070c")]
    [TestCase("Bahco 9070 P mit Ersatzbacke", "bahco-9070p")]
    [TestCase("Bahco 8070 mit Rändelschraube", "bahco-8070")]
    [TestCase("Bahco 9072 PC 257 mm", "bahco-9072pc")]
    public void PackagingAndIncludedSparesDoNotInventAnotherBody(string title, string id)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("Bahco 8070-1")]
    [TestCase("Bahco 8070-2")]
    [TestCase("Bahco 9070 P-1")]
    [TestCase("Bahco 8070 Ersatzbacke")]
    [TestCase("Ersatzbacke Bahco 8070")]
    [TestCase("Rändelschraube Bahco 8070")]
    [TestCase("Bahco 8070 knurl")]
    [TestCase("Ersatzbacken für Bahco 8070 C")]
    [TestCase("Bahco 8070 und 9070")]
    [TestCase("Bahco 8070 9070")]
    [TestCase("Bahco 8070 C 9070 P")]
    [TestCase("Bahco 8070 + KNIPEX 87 01 250")]
    [TestCase("Bahco 8070 bundle")]
    [TestCase("Bahco 8070 gesucht")]
    [TestCase("Bahco 8070 Bedienungsanleitung")]
    [TestCase("Bahco 8070 RC US")]
    [TestCase("Bahco 8070RCUS")]
    [TestCase("Bahco 8070V")]
    [TestCase("Bahco 8070VL")]
    [TestCase("Bahco 8071VLT")]
    [TestCase("Bahco 9074")]
    [TestCase("Bahco 8070 PC")]
    [TestCase("Bahco 8070 P")]
    [TestCase("Bahco 8070 C P")]
    public void PartsBundlesAndUnverifiedAdjacentArticlesDoNotClaimOneBody(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);
}
