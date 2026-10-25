using System.Text.Json;
using Coflnet.Ane.KnownProducts;
namespace AneCore.Tests;
[TestFixture]
public class ResaleRoundElevenTests
{
    [Test]
    public void VerifiedAliasesResolveDistinctProducts()
    {
        var ids = new[] { "yamaha-psr-e413", "yamaha-psr-e423", "yamaha-psr-e433", "yamaha-psr-e443", "yamaha-psr-e453", "yamaha-psr-e463", "yamaha-psr-e473", "yamaha-psr-e483", "stihl-hs-81-r", "stihl-hs-81-rc", "stihl-hs-81-t", "stihl-hs-81-tc", "ikea-brimnes-2-tuerig-weiss-78x50x190", "preiser-10626", "preiser-10627", "preiser-10629", "schleich-42368", "schleich-42369", "schleich-42370", "schleich-42381", "schleich-42404" };
        var products = KnownProductSeed.LoadAll(true).Where(p => ids.Contains(p.Id, StringComparer.Ordinal)).ToList();
        Assert.That(products.Select(p => p.Id), Is.EquivalentTo(ids));
        foreach (var product in products)
            foreach (var alias in product.Aliases)
                Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
    }
    [Test]
    public void ActualFullOffersPreserveCompletenessEditionAndQuantityBoundaries()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/resale-round11.json")));
        foreach (var row in data.RootElement.EnumerateArray())
            Assert.That(ResaleCatalog.Match(row.GetProperty("title").GetString(),row.GetProperty("description").GetString())?.Id,
                Is.EqualTo(row.GetProperty("expectedProductId").GetString()),row.GetProperty("listingId").GetString());
    }
    [TestCase("Das Set ist vollständig.", true)]
    [TestCase("Das Set ist vollständig. Die Verpackung ist nicht vollständig.", true)]
    [TestCase("Das Set ist nicht mehr ganz vollständig.", false)]
    [TestCase("Das Set ist nicht vollständig.", false)]
    [TestCase("Das Set ist nicht unvollständig.", true)]
    public void FullFactorySetDoesNotIncludeExplicitlyIncompleteOffers(string description,bool complete)
        => Assert.That(ResaleCatalog.Match("Schleich 42370",description)?.Id,Is.EqualTo(complete?"schleich-42370":null));
    [TestCase("Vergaser STIHL HS 81 R")]
    [TestCase("STIHL HS 81 R Vergaser")]
    [TestCase("Ersatzmesser STIHL HS 81 R")]
    [TestCase("STIHL HS 81 R Messerabdeckung")]
    [TestCase("STIHL HS 81 R und HS 81 T")]
    [TestCase("STIHL HS 81 R + unbekanntes Werkzeug")]
    [TestCase("Schleich 42370 42381")]
    [TestCase("Figur für Preiser 10626")]
    [TestCase("IKEA BRIMNES Kleiderschrank schwarz 78x50x190")]
    [TestCase("IKEA BRIMNES Kleiderschrank weiß 117x50x190")]
    [TestCase("Suche Yamaha PSR-E463")]
    [TestCase("Yamaha PSR-E463 zu vermieten")]
    [TestCase("Reparaturservice STIHL HS 81 R")]
    [TestCase("Anleitung Yamaha PSR-E463")]
    public void ReplacementPartsUnknownVariantsBundlesAndServicesStayOut(string title)
        => Assert.That(ResaleCatalog.Match(title),Is.Null,title);
    [TestCase("STIHL HS 81 R mit Ersatzmesser", "stihl-hs-81-r")]
    [TestCase("Yamaha PSR-E463 incl. Netzteil und Ständer", "yamaha-psr-e463")]
    public void CompleteDevicesKeepIncludedUsualExtras(string title,string id)
        => Assert.That(ResaleCatalog.Match(title)?.Id,Is.EqualTo(id));
}
