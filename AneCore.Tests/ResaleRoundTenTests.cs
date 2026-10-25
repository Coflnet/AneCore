using Coflnet.Ane.KnownProducts;
namespace AneCore.Tests;
[TestFixture]
public class ResaleRoundTenTests
{
    [Test]
    public void VerifiedNewModelsAndTheirAliasesRemainDistinct()
    {
        var ids = new[] { "sennheiser-mke-200-mobile-kit", "yamaha-psr-e403", "shimano-cs-hg200-8", "shimano-cs-hg300-8", "shimano-cs-hg400-7", "shimano-cs-hg41-7", "shimano-cs-hg20-7", "shimano-cs-hg210-7", "shimano-cs-hg200-7", "villeroy-boch-68350001", "villeroy-boch-683500rw", "sennheiser-mke-200", "sennheiser-mke-440", "garmin-speed-sensor-2", "tonies-die-drei-ausrufezeichen-das-rote-phantom-de", "ravensburger-echoes-der-cocktail-de", "ravensburger-echoes-der-mikrochip-de", "playmobil-70206", "ravensburger-dragons-wettflug-zur-dracheninsel-de" };
        var products = KnownProductSeed.LoadAll(true).Where(p => ids.Contains(p.Id, StringComparer.Ordinal)).ToList();
        Assert.That(products.Select(p => p.Id), Is.EquivalentTo(ids));
        foreach (var product in products)
            foreach (var alias in product.Aliases)
                Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
    }
    [TestCase("Sennheiser MKE 200 Mobile Kit", "sennheiser-mke-200-mobile-kit")]
    [TestCase("Sennheiser MKE 200", "sennheiser-mke-200")]
    [TestCase("Sennheiser MKE 200 mit Windschutz", "sennheiser-mke-200")]
    [TestCase("Shimano CS-HG200-8 mit Verschlussring", "shimano-cs-hg200-8")]
    [TestCase("Yamaha PSR-E403 with power adapter and stand", "yamaha-psr-e403")]
    [TestCase("Yamaha PSR-E403 inkl. Netzteil und Ständer", "yamaha-psr-e403")]
    [TestCase("Yamaha Keyboard PSR- E403, incl. Netzteil und Ständer", "yamaha-psr-e403")]
    public void IncludedExtrasAndFactoryKitKeepTheirOwnProduct(string title, string id)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));
    [TestCase("Yamaha PSR-E403 und Yamaha PSR-E413")]
    [TestCase("Yamaha PSR-E403 incl. Netzteil und unbekanntes Gerät")]
    [TestCase("Ständer für Yamaha PSR-E403")]
    [TestCase("Verschlussring Shimano CS-HG200-8")]
    [TestCase("Shimano CS-HG200-8 Verschlussring")]
    [TestCase("Windschutz Sennheiser MKE 200")]
    [TestCase("Garmin Speed Sensor 2 und Cadence Sensor 2")]
    [TestCase("Deckel für Villeroy Boch 68350001")]
    [TestCase("Suche Shimano CS-HG200-8")]
    [TestCase("Shimano CS-HG200-8 + CS-HG200-7")]
    [TestCase("Anleitung PLAYMOBIL 70206")]
    [TestCase("PLAYMOBIL 70206 70207")]
    [TestCase("Tonie Die drei !!! Das rote Phantom Buch")]
    public void PartsOfPartsMediaKitsAndBundlesDoNotBecomeWholeProducts(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null, title);
}
