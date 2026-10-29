using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexCirclipCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-10-29-r43-circlip";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    // Reviewed complete articles and explicit Length cells from the two manufacturer family tables.
    public static readonly object[][] ManufacturerRows =
    {
        new object[] { "44 11 J0", "140", "internal_circlip_pliers" },
        new object[] { "44 11 J1", "140", "internal_circlip_pliers" },
        new object[] { "44 11 J2", "180", "internal_circlip_pliers" },
        new object[] { "44 11 J3", "225", "internal_circlip_pliers" },
        new object[] { "44 11 J4", "320", "internal_circlip_pliers" },
        new object[] { "44 13 J0", "140", "internal_circlip_pliers" },
        new object[] { "44 13 J1", "140", "internal_circlip_pliers" },
        new object[] { "44 13 J2", "180", "internal_circlip_pliers" },
        new object[] { "44 13 J3", "225", "internal_circlip_pliers" },
        new object[] { "44 13 J4", "320", "internal_circlip_pliers" },
        new object[] { "44 21 J01", "130", "internal_circlip_pliers" },
        new object[] { "44 21 J11", "130", "internal_circlip_pliers" },
        new object[] { "44 21 J21", "170", "internal_circlip_pliers" },
        new object[] { "44 21 J31", "215", "internal_circlip_pliers" },
        new object[] { "44 21 J41", "300", "internal_circlip_pliers" },
        new object[] { "44 23 J11", "130", "internal_circlip_pliers" },
        new object[] { "44 23 J21", "170", "internal_circlip_pliers" },
        new object[] { "44 23 J31", "215", "internal_circlip_pliers" },
        new object[] { "44 31 J02", "140", "internal_circlip_pliers" },
        new object[] { "44 31 J12", "140", "internal_circlip_pliers" },
        new object[] { "44 31 J22", "180", "internal_circlip_pliers" },
        new object[] { "44 31 J32", "225", "internal_circlip_pliers" },
        new object[] { "44 31 J42", "310", "internal_circlip_pliers" },
        new object[] { "46 11 A0", "140", "external_circlip_pliers" },
        new object[] { "46 11 A1", "140", "external_circlip_pliers" },
        new object[] { "46 11 A2", "180", "external_circlip_pliers" },
        new object[] { "46 11 A3", "210", "external_circlip_pliers" },
        new object[] { "46 11 A4", "320", "external_circlip_pliers" },
        new object[] { "46 13 A0", "140", "external_circlip_pliers" },
        new object[] { "46 13 A1", "140", "external_circlip_pliers" },
        new object[] { "46 13 A2", "180", "external_circlip_pliers" },
        new object[] { "46 13 A3", "210", "external_circlip_pliers" },
        new object[] { "46 21 A01", "125", "external_circlip_pliers" },
        new object[] { "46 21 A11", "125", "external_circlip_pliers" },
        new object[] { "46 21 A21", "170", "external_circlip_pliers" },
        new object[] { "46 21 A31", "200", "external_circlip_pliers" },
        new object[] { "46 21 A41", "300", "external_circlip_pliers" },
        new object[] { "46 23 A01", "125", "external_circlip_pliers" },
        new object[] { "46 23 A11", "125", "external_circlip_pliers" },
        new object[] { "46 23 A21", "170", "external_circlip_pliers" },
        new object[] { "46 23 A31", "200", "external_circlip_pliers" },
        new object[] { "46 31 A02", "130", "external_circlip_pliers" },
        new object[] { "46 31 A12", "130", "external_circlip_pliers" },
        new object[] { "46 31 A22", "185", "external_circlip_pliers" },
        new object[] { "46 31 A32", "210", "external_circlip_pliers" },
        new object[] { "46 31 A42", "310", "external_circlip_pliers" },
    };

    [TestCaseSource(nameof(ManufacturerRows))]
    public void CompleteManufacturerArticleHasItsOwnIdentityAndExplicitLength(string article, string length, string type)
    {
        var id = "knipex-" + article.Replace(" ", "").ToLowerInvariant();
        var row = Catalog.SingleOrDefault(product => product.Id == id);
        Assert.That(row, Is.Not.Null, article);
        Assert.That(Catalog.Count(product => product.Id == id || product.Brand == "KNIPEX" && product.Model == article), Is.EqualTo(1));
        Assert.That(row!.Source, Is.EqualTo(Source));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "tool_type", "article_number", "length_mm" }));
        foreach (var (key, value) in new[] { ("product_kind", "hand_tool"), ("tool_type", type), ("article_number", article), ("length_mm", length) })
            Assert.That(row.PossibleAttributes[key], Is.EquivalentTo(new[] { value }), article);
        foreach (var title in row.Aliases.Append("KNIPEX circlip pliers " + article))
        {
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
            Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
            Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True, title);
        }
    }

    [Test]
    public void SupplementContainsExactlyTheReviewedArticlesWithoutAliasCollisions()
    {
        var added = Catalog.Where(product => product.Source == Source).ToList();
        Assert.That(added, Has.Count.EqualTo(46));
        Assert.That(added.Select(product => product.Model), Is.EquivalentTo(ManufacturerRows.Select(row => (string)row[0])));
        var baselineAliases = Catalog.Where(product => product.Source != Source).SelectMany(product => product.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(added.SelectMany(product => product.Aliases).Select(KnownProductMatcher.Normalize).Where(baselineAliases.Contains), Is.Empty);
    }

    [TestCase("KNIPEX 44 11 J")]
    [TestCase("KNIPEX 44 11 J10")]
    [TestCase("KNIPEX 44 11 J1FOO")]
    [TestCase("KNIPEX 44 11 J1/01")]
    [TestCase("KNIPEX 44 11 J1-01")]
    [TestCase("KNIPEX 44 11 J1 SB")]
    [TestCase("KNIPEX 44 11 J1 T")]
    [TestCase("KNIPEX 44 23 J01")]
    [TestCase("KNIPEX 46 13 A4")]
    [TestCase("KNIPEX 44 19 J5")]
    [TestCase("KNIPEX 44 11 J1 Ersatzspitzen")]
    [TestCase("Ersatzfeder für KNIPEX 46 11 A1")]
    [TestCase("KNIPEX spare tips for 44 11 J1")]
    [TestCase("KNIPEX 44 11 J1 Satz")]
    [TestCase("KNIPEX 44 11 J1 kit")]
    [TestCase("KNIPEX 44 11 J1 und 46 11 A1")]
    [TestCase("Bahco 44 11 J1")]
    public void UnverifiedArticlesVariantsPartsAndSetsDoNotClaimOneWholeTool(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("KNIPEX 44 11 J1 Ersatzspitzen")]
    [TestCase("KNIPEX spare tips for 44 11 J1")]
    [TestCase("2 Stück KNIPEX 46 11 A1")]
    [TestCase("KNIPEX 44 11 J1 und 46 11 A1")]
    public void RelatedPhysicalOffersStaySeparateFromTheVerifiedWholeTool(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
    }

    [TestCase("KNIPEX 44 11 J1 gesucht")]
    [TestCase("KNIPEX 44 11 J1 Bedienungsanleitung")]
    public void NonSaleAndMediaAreNotPhysicalToolOffers(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False);
    }
}
