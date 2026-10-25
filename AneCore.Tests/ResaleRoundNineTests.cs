using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class ResaleRoundNineTests
{
    [Test]
    public void AllNewAliasesResolveTheirOwnProductAndFacts()
    {
        var ids = new[]
        {
            "mackie-402vlz4", "mackie-802vlz4", "mackie-1202vlz4",
            "mackie-1402vlz4", "mackie-1642vlz4", "mackie-1604vlz4",
            "mackie-2404vlz4", "mackie-3204vlz4", "electro-harmonix-deluxe-memory-man-550-tt",
            "electro-harmonix-deluxe-memory-man-1100-tt", "ninja-creami-nc300eu", "lg-bp440",
            "haba-meine-ersten-spiele-fische-angeln", "ravensburger-maulwurf-und-sein-lieblingsspiel", "remember-woodman-tu1",
            "playmobil-9433", "ikea-malm-6-schubladen-weiss-160x78", "neewer-z1-n",
            "tonies-nico-sternbaum-schuettel-den-weihnachtsbaum-de", "tonies-bibi-tina-der-verschwundene-pokal-de",
        };
        var products = KnownProductSeed.LoadAll(includeExpandedCatalog: true).Where(p => ids.Contains(p.Id, StringComparer.Ordinal)).ToList();
        Assert.That(products.Select(p => p.Id), Is.EquivalentTo(ids));
        foreach (var product in products)
        {
            foreach (var alias in product.Aliases)
                Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
            Assert.That(product.PossibleAttributes, Is.Not.Empty, product.Id);
        }
    }

    [TestCase("Becher für Ninja NC300EU")]
    [TestCase("Fernbedienung für LG BP440")]
    [TestCase("Netzteil für Mackie 402VLZ4")]
    [TestCase("Anleitung PLAYMOBIL 9433")]
    [TestCase("Suche REMEMBER Woodman")]
    [TestCase("Ninja NC300EU zu vermieten")]
    [TestCase("Mackie 402VLZ4 + Mackie 802VLZ4")]
    [TestCase("Mackie 402VLZ4 + unbekanntes Gerät")]
    [TestCase("Tonie Bibi und Tina Der verschwundene Pokal und Ninja NC300EU")]
    [TestCase("PLAYMOBIL 9433 unvollständig")]
    public void WholeProductBoundariesRemainClosed(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);
}
