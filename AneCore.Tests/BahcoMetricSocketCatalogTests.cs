using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class BahcoMetricSocketCatalogTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void AllManufacturerSocketArticlesReachCanonicalAndPhysicalOfferAdmission()
    {
        var additions = Catalog.Where(p => p.Source == "seed:manufacturer-verified-2026-10-28-r37").ToList();
        Assert.That(additions, Has.Count.EqualTo(108));
        foreach (var row in additions)
        {
            Assert.That(Catalog.Count(p => p.Id == row.Id || p.Brand == row.Brand && p.Model == row.Model), Is.EqualTo(1), row.Id);
            Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "tool_type", "article_number", "length_mm", "socket_size_mm", "square_drive_inches", "socket_profile" }));
            Assert.That(row.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "hand_tool" }));
            Assert.That(row.PossibleAttributes["article_number"], Is.EquivalentTo(new[] { row.Model }));
            Assert.That(row.PossibleAttributes.Keys, Does.Not.Contain("wrench_size_mm"));
            foreach (var title in row.Aliases.Append(row.Name))
            {
                Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(row.Id), title);
                Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(row.Id), title);
                Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True, title);
                Assert.That(ResaleCatalog.Match(title + " gebraucht")?.Id, Is.EqualTo(row.Id), title);
            }
        }
    }

    // Exact cells from the official family tables; equal openings do not imply equal bodies.
    [TestCase("6700SM-4.5", "bahco-6700sm-4-5", "24.7", "4.5", "1/4", "hex", "socket")]
    [TestCase("6700SM-13", "bahco-6700sm-13", "24.7", "13", "1/4", "hex", "socket")]
    [TestCase("7800SM-13", "bahco-7800sm-13", "38", "13", "1/2", "hex", "socket")]
    [TestCase("7800DM-20", "bahco-7800dm-20", "32", "20", "1/2", "bi_hex", "socket")]
    [TestCase("7800SM-20", "bahco-7800sm-20", "40", "20", "1/2", "hex", "socket")]
    [TestCase("7805SM-13", "bahco-7805sm-13", "82.6", "13", "1/2", "hex", "deep_socket")]
    [TestCase("7805DM-13", "bahco-7805dm-13", "82.6", "13", "1/2", "bi_hex", "deep_socket")]
    public void DriveProfileLengthAndOpeningStayWithTheirExactArticle(string article, string id, string length, string opening, string drive, string profile, string type)
    {
        var row = ResaleCatalog.Match("Bahco " + article)!;
        Assert.That(row.Id, Is.EqualTo(id));
        foreach (var (key, value) in new[] { ("length_mm", length), ("socket_size_mm", opening), ("square_drive_inches", drive), ("socket_profile", profile), ("tool_type", type) })
            Assert.That(row.PossibleAttributes[key], Is.EquivalentTo(new[] { value }), article);
    }

    [TestCase("Bahco 6700SM")]
    [TestCase("Bahco 7800SM")]
    [TestCase("Bahco 6700SM-4.8")]
    [TestCase("Bahco 6700SM4.8")]
    [TestCase("Bahco 6700SM-4,8")]
    [TestCase("Bahco 7800SM-13FOO")]
    [TestCase("Bahco 7800SM-13/10")]
    [TestCase("Bahco 7805SM-11")]
    [TestCase("Bahco SB7800SM-13")]
    [TestCase("Bahco 7800SM-13 SB")]
    [TestCase("Bahco 7800SM-13 Satz")]
    [TestCase("Bahco 7800SM-13 Bedienungsanleitung")]
    [TestCase("Bahco 7800SM-13 gesucht")]
    [TestCase("Bahco 7800SM-13 7800DM-13")]
    [TestCase("Bahco 7800SM-13 und 7805SM-13")]
    [TestCase("Adapter für Bahco 7800SM-13")]
    public void UnknownVariantsPackagesMediaAndSeveralArticlesDoNotClaimOneSocket(string title) =>
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);

    [TestCase("Set mit Bahco 7800SM-13")]
    [TestCase("2 Stück Bahco 6700SM-4.5")]
    [TestCase("Bahco 7800SM-13 und 7805SM-13")]
    [TestCase("Adapter für Bahco 7800SM-13")]
    [TestCase("Bahco 7800SM-13 SB")]
    public void PhysicalReferencesRemainIndependentOffers(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
    }

    [TestCase("Bahco 7800SM-13 mit Koffer", "bahco-7800sm-13")]
    [TestCase("Bahco 7800SM-13 mit Anleitung", "bahco-7800sm-13")]
    [TestCase("Bahco 7800SM-13mm", "bahco-7800sm-13")]
    [TestCase("Bahco 6700SM-4.5", "bahco-6700sm-4-5")]
    [TestCase("Bahco 6700SM-4,5", "bahco-6700sm-4-5")]
    public void IncludedCaseManualUnitsAndKnownDecimalArticleRemainValid(string title, string id) =>
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));
}
