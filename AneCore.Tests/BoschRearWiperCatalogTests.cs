using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class BoschRearWiperCatalogTests
{
    private static readonly string[] Category = ["Fahrzeuge & Teile", "Fahrzeugersatzteile & -zubehör", "Kfz-Teile"];
    private static readonly KnownProductMatcher Matcher = new(KnownProductSeed.LoadAll(true));

    [TestCase("301", "3397004629", "300")]
    [TestCase("304", "3397004990", "300")]
    [TestCase("305", "3397011239", "305")]
    [TestCase("306", "3397011432", "300")]
    [TestCase("307", "3397011429", "300")]
    [TestCase("308", "3397011628", "300")]
    [TestCase("309", "3397011630", "300")]
    [TestCase("330", "3397011306", "330")]
    [TestCase("341", "3397004755", "340")]
    [TestCase("351", "3397004559", "350")]
    [TestCase("352", "3397011430", "350")]
    [TestCase("353", "3397004631", "350")]
    [TestCase("355", "3397011435", "350")]
    [TestCase("370", "3397011022", "370")]
    [TestCase("380", "3397004756", "380")]
    [TestCase("382", "3397011412", "380")]
    [TestCase("383", "3397011551", "380")]
    [TestCase("384", "3397015045", "380")]
    [TestCase("400", "3397004757", "400")]
    [TestCase("402", "3397004632", "400")]
    public void OfficialShortCodePartNumberAndLengthStayOneCatalogueIdentity(string code, string article, string length)
    {
        var expectedId = "bosch-h" + code;
        var row = KnownProductSeed.LoadAll(true).Single(p => p.Id == expectedId);
        Assert.That(row.Categories, Is.EqualTo(Category));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "device_type", "article_number", "length_mm" }));
        Assert.That(row.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "automotive_part" }));
        Assert.That(row.PossibleAttributes["article_number"], Is.EquivalentTo(new[] { article }));
        Assert.That(row.PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { length }));
        foreach (var alias in row.Aliases)
        {
            Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(expectedId));
            Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(expectedId));
        }
        Assert.That(ResaleCatalog.Match("Bosch H" + code, categoryPath: Category)?.Id, Is.EqualTo(expectedId));
    }

    [TestCase("Balai d’essuie-glace arrière Bosch H 341")]
    [TestCase("Tergilunotto posteriore Bosch H341")]
    [TestCase("Bosch H341 Heckscheibenwischer")]
    [TestCase("Bosch H341 à vendre")]
    [TestCase("Bosch H341, neu")]
    [TestCase("Bosch H341, 340 mm")]
    [TestCase("Bosch H341. Neu")]
    [TestCase("Bosch Rear H341 rear wiper blade")]
    public void MultilingualTitlesUseTheExactVerifiedBlade(string title)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo("bosch-h341"));

    [TestCase("Bosch Heckscheibenwischer")]
    [TestCase("Heckscheibenwischer H341")]
    [TestCase("Bosch H342")]
    [TestCase("Bosch H341A")]
    [TestCase("Bosch H341-A")]
    [TestCase("Bosch H341 + H351")]
    [TestCase("Bosch H341 und Bosch H351")]
    [TestCase("Suche Bosch H341")]
    [TestCase("Bosch H341 Reparaturservice")]
    [TestCase("Bosch H341 Adapter")]
    [TestCase("Wiper arm Bosch H341")]
    [TestCase("Bosch H341 wiper arm")]
    [TestCase("Rubber insert Bosch H341")]
    [TestCase("Bosch H341 rubber insert")]
    [TestCase("Wischerarm Bosch H341")]
    [TestCase("Bosch H341 Wischerarm")]
    [TestCase("Moteur Bosch H341")]
    [TestCase("Bras pour Bosch H341")]
    [TestCase("Braccio per Bosch H341")]
    [TestCase("Gommino Bosch H341")]
    [TestCase("Manuel Bosch H341")]
    [TestCase("Balai compatible Bosch H341")]
    [TestCase("Tergilunotto compatibile Bosch H341")]
    [TestCase("Adattatore Bosch H341")]
    public void UnknownModelsLotsServicesAndOtherPartsDoNotClaimTheBlade(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
    }

    [Test]
    public void ARecognizedIncompatibleCategoryKeepsItsExistingVeto()
        => Assert.That(ResaleCatalog.Match("Bosch H341", categoryPath: ["Elektronik", "Computer", "Laptops"]), Is.Null);
}
