using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexWirePreparationCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-10-29-r45-wire-preparation";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    // Exact complete manufacturer articles and explicit Length cells; not decoded from model suffixes.
    public static readonly object[][] ManufacturerRows =
    {
        new object[] { "97 68 145 A", "145", "crimping_pliers" },
        new object[] { "97 53 18", "200", "crimping_pliers" },
        new object[] { "97 53 19", "200", "crimping_pliers" },
        new object[] { "97 55 04", "180", "crimping_pliers" },
        new object[] { "97 62 145 A", "145", "crimping_pliers" },
        new object[] { "97 78 180", "180", "crimping_pliers" },
        new object[] { "97 52 67 DT", "230", "crimping_pliers" },
        new object[] { "97 21 215", "230", "crimping_pliers" },
        new object[] { "97 21 215 B", "230", "crimping_pliers" },
        new object[] { "97 32 240", "240", "crimping_pliers" },
        new object[] { "97 21 215 C", "230", "crimping_pliers" },
        new object[] { "97 22 240", "240", "crimping_pliers" },
        new object[] { "97 71 180", "180", "crimping_pliers" },
        new object[] { "97 72 180", "180", "crimping_pliers" },
        new object[] { "97 53 04", "180", "crimping_pliers" },
        new object[] { "97 53 14", "180", "crimping_pliers" },
        new object[] { "97 55 14", "180", "crimping_pliers" },
        new object[] { "97 51 12", "200", "crimping_pliers" },
        new object[] { "97 51 10", "190", "crimping_pliers" },
        new object[] { "97 51 13", "190", "crimping_pliers" },
        new object[] { "97 52 30", "220", "crimping_pliers" },
        new object[] { "97 52 33", "220", "crimping_pliers" },
        new object[] { "97 52 34", "220", "crimping_pliers" },
        new object[] { "97 52 35", "220", "crimping_pliers" },
        new object[] { "97 52 36", "220", "crimping_pliers" },
        new object[] { "97 52 37", "220", "crimping_pliers" },
        new object[] { "97 52 38", "220", "crimping_pliers" },
        new object[] { "97 52 42", "220", "crimping_pliers" },
        new object[] { "97 52 50", "220", "crimping_pliers" },
        new object[] { "97 61 145 A", "145", "crimping_pliers" },
        new object[] { "97 61 145 F", "145", "crimping_pliers" },
        new object[] { "97 53 08", "190", "crimping_pliers" },
        new object[] { "97 53 09", "190", "crimping_pliers" },
        new object[] { "97 52 20", "195", "crimping_pliers" },
        new object[] { "97 52 14", "195", "crimping_pliers" },
        new object[] { "97 54 26", "190", "crimping_pliers" },
        new object[] { "97 54 25", "190", "crimping_pliers" },
        new object[] { "97 54 24", "190", "crimping_pliers" },
        new object[] { "97 81 180", "180", "crimping_pliers" },
        new object[] { "97 50 01", "155", "crimping_pliers" },
        new object[] { "97 43 66 EVO", "200", "crimping_pliers" },
        new object[] { "97 43 66", "200", "crimping_pliers" },
        new object[] { "97 43 06", "200", "crimping_pliers" },
        new object[] { "97 43 05", "200", "crimping_pliers" },
        new object[] { "97 52 23", "250", "crimping_pliers" },
        new object[] { "97 52 19", "250", "crimping_pliers" },
        new object[] { "97 52 18", "250", "crimping_pliers" },
        new object[] { "97 52 13", "250", "crimping_pliers" },
        new object[] { "97 52 10", "250", "crimping_pliers" },
        new object[] { "97 52 09", "250", "crimping_pliers" },
        new object[] { "97 52 08", "250", "crimping_pliers" },
        new object[] { "97 52 06", "250", "crimping_pliers" },
        new object[] { "97 52 05", "250", "crimping_pliers" },
        new object[] { "97 52 04", "250", "crimping_pliers" },
        new object[] { "97 52 63 A", "175", "crimping_pliers" },
        new object[] { "97 52 63 DG A", "175", "crimping_pliers" },
        new object[] { "97 52 65 A", "230", "crimping_pliers" },
        new object[] { "97 52 65 DG A", "240", "crimping_pliers" },
        new object[] { "97 52 42 6", "220", "crimping_pliers" },
        new object[] { "97 52 63", "175", "crimping_pliers" },
        new object[] { "97 52 63 DG", "195", "crimping_pliers" },
        new object[] { "97 52 65", "230", "crimping_pliers" },
        new object[] { "12 42 195", "195", "wire_stripper" },
        new object[] { "12 52 195", "190", "wire_stripper" },
        new object[] { "12 40 200", "200", "wire_stripper" },
        new object[] { "11 01 160", "160", "wire_stripper" },
        new object[] { "11 02 160", "160", "wire_stripper" },
        new object[] { "11 05 160", "160", "wire_stripper" },
        new object[] { "11 06 160", "160", "wire_stripper" },
        new object[] { "11 07 160", "160", "wire_stripper" },
        new object[] { "11 12 160", "160", "wire_stripper" },
        new object[] { "14 22 160", "160", "wire_stripper" },
        new object[] { "14 25 160", "160", "wire_stripper" },
        new object[] { "14 26 160", "160", "wire_stripper" },
        new object[] { "13 71 8", "200", "wire_stripper" },
        new object[] { "13 72 8", "200", "wire_stripper" },
        new object[] { "13 71 200 ME", "200", "wire_stripper" },
        new object[] { "13 72 200 ME", "200", "wire_stripper" },
        new object[] { "13 76 200 ME", "200", "wire_stripper" },
        new object[] { "13 62 180", "180", "wire_stripper" },
        new object[] { "13 66 180", "180", "wire_stripper" },
        new object[] { "12 72 190", "190", "wire_stripper" },
        new object[] { "11 92 140", "140", "wire_stripper" },
        new object[] { "15 81 160", "160", "wire_stripper" },
        new object[] { "15 61 160", "160", "wire_stripper" },
        new object[] { "15 51 160", "160", "wire_stripper" },
        new object[] { "12 64 180", "180", "wire_stripper" },
        new object[] { "12 12 14", "195", "wire_stripper" },
        new object[] { "12 12 13", "195", "wire_stripper" },
        new object[] { "12 12 12", "195", "wire_stripper" },
        new object[] { "12 12 11", "195", "wire_stripper" },
        new object[] { "12 12 10", "195", "wire_stripper" },
        new object[] { "12 12 06", "195", "wire_stripper" },
        new object[] { "12 12 02", "195", "wire_stripper" },
        new object[] { "12 21 180", "180", "wire_stripper" },
        new object[] { "12 11 180", "180", "wire_stripper" },
        new object[] { "13 46 165", "165", "wire_stripper" },
        new object[] { "13 42 165", "165", "wire_stripper" },
        new object[] { "12 74 180", "175", "wire_stripper" },
        new object[] { "13 01 160", "160", "electricians_pliers" },
        new object[] { "13 02 160", "160", "electricians_pliers" },
        new object[] { "13 05 160", "160", "electricians_pliers" },
        new object[] { "13 81 200", "200", "installation_pliers" },
        new object[] { "13 82 200", "200", "installation_pliers" },
        new object[] { "13 86 200", "200", "installation_pliers" },
        new object[] { "13 92 200", "200", "installation_pliers" },
        new object[] { "13 96 200", "200", "installation_pliers" },
        new object[] { "16 40 150", "150", "cable_stripper" },
        new object[] { "16 45 220", "225", "cable_stripper" },
    };

    [TestCaseSource(nameof(ManufacturerRows))]
    public void CompleteArticleLoadsWithOnlyItsVerifiedFactsAndMatchesEveryAlias(string article, string length, string type)
    {
        var id = "knipex-" + article.Replace(" ", "").ToLowerInvariant();
        var row = Catalog.SingleOrDefault(product => product.Id == id);
        Assert.That(row, Is.Not.Null, article);
        Assert.That(Catalog.Count(product => product.Id == id || product.Brand == "KNIPEX" && product.Model == article), Is.EqualTo(1));
        Assert.That(row!.Source, Is.EqualTo(Source));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "tool_type", "article_number", "length_mm" }));
        foreach (var (key, value) in new[] { ("product_kind", "hand_tool"), ("tool_type", type), ("article_number", article), ("length_mm", length) })
            Assert.That(row.PossibleAttributes[key], Is.EquivalentTo(new[] { value }), article);
        foreach (var title in row.Aliases.Append("KNIPEX " + article + " gebraucht"))
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
    }

    [Test]
    public void SupplementAddsExactly109NewIdentitiesWithoutChangingPriorCoverage()
    {
        var added = Catalog.Where(product => product.Source == Source).ToList();
        Assert.That(added, Has.Count.EqualTo(109));
        Assert.That(added.Select(product => product.Model), Is.EquivalentTo(ManufacturerRows.Select(row => (string)row[0])));
        Assert.That(Catalog.Count(product => product.Source != Source), Is.EqualTo(27417));
        Assert.That(Catalog.Count(product => product.Vertical == "lego"), Is.EqualTo(7895));
        Assert.That(KnownProductSeed.LoadAll().Count, Is.EqualTo(23), "the original curated-only loader contract stays unchanged");
        var previousAliases = Catalog.Where(product => product.Source != Source).SelectMany(product => product.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(added.SelectMany(product => product.Aliases).Select(KnownProductMatcher.Normalize).Where(previousAliases.Contains), Is.Empty);
    }

    [Test]
    public void SharedResaleAdmissionLoadsEveryReviewedManualTool()
    {
        Assert.Multiple(() =>
        {
            foreach (var row in ManufacturerRows)
            {
                var article = (string)row[0];
                var title = "KNIPEX " + article + " gebraucht";
                var id = "knipex-" + article.Replace(" ", "").ToLowerInvariant();
                Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
                Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True, title);
            }
        });
    }

    [TestCase("KNIPEX 97 52 63", "knipex-975263")]
    [TestCase("KNIPEX 97 52 63 A", "knipex-975263a")]
    [TestCase("KNIPEX 97 52 63 DG", "knipex-975263dg")]
    [TestCase("KNIPEX 97 52 63 DG A", "knipex-975263dga")]
    [TestCase("KNIPEX 97 21 215", "knipex-9721215")]
    [TestCase("KNIPEX 97 21 215 B", "knipex-9721215b")]
    [TestCase("KNIPEX 97 21 215 C", "knipex-9721215c")]
    [TestCase("KNIPEX 97 52 42 6", "knipex-9752426")]
    [TestCase("KNIPEX Twistor T 97 53 19", "knipex-975319")]
    [TestCase("KNIPEX 97 53 19 Twistor T", "knipex-975319")]
    [TestCase("KNIPEX 97 53 04 mit Ersatzbacken", "knipex-975304")]
    public void ExactSiblingArticlesAndIncludedExtrasRetainTheirOwnIdentity(string title, string id)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));
    }

    [TestCase("KNIPEX Crimpzange")]
    [TestCase("KNIPEX Abisolierzange")]
    [TestCase("KNIPEX 97 52 99")]
    [TestCase("KNIPEX 97 53 04 SB")]
    [TestCase("KNIPEX 97 53 04 T")]
    [TestCase("KNIPEX 97 53 04FOO")]
    [TestCase("KNIPEX 97 53 04/01")]
    [TestCase("KNIPEX 97 53 04-01")]
    [TestCase("Ersatzbacken für KNIPEX 97 53 04")]
    [TestCase("KNIPEX die set for 97 43 06")]
    [TestCase("Locator for KNIPEX 97 52 65 A")]
    [TestCase("KNIPEX 97 43 200")]
    [TestCase("KNIPEX 97 33 LE")]
    [TestCase("KNIPEX 12 62 180")]
    [TestCase("KNIPEX 11 82 130")]
    [TestCase("KNIPEX 97 52 65 DG")]
    [TestCase("KNIPEX 97 53 04 set")]
    [TestCase("KNIPEX 97 53 04 und 12 72 190")]
    [TestCase("Bahco 97 53 04")]
    public void UnselectedArticlesPackagesPartsAndSetsCannotClaimOneWholeTool(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("2 Stück KNIPEX 97 53 04")]
    [TestCase("KNIPEX 97 53 04 und 12 72 190")]
    [TestCase("Ersatzbacken für KNIPEX 97 53 04")]
    public void RelatedPhysicalOffersRemainIndependentRatherThanClaimingTheTool(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
    }

    [TestCase("KNIPEX 97 53 04 gesucht")]
    [TestCase("KNIPEX 97 53 04 Bedienungsanleitung")]
    public void WantedOrMediaOffersAreNotPhysicalToolOffers(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False);
    }
}
