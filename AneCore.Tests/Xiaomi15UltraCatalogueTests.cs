using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class Xiaomi15UltraCatalogueTests
{
    private static KnownProduct Product => KnownProductSeed.LoadAll(includeExpandedCatalog: true).Single(p => p.Id == "xiaomi-15-ultra");

    [Test]
    public void ActualStoredImpossibleRamAndScreenAreRemovedWithRawTwinsWithoutInventingRam()
    {
        // R9 random group xiaomi-15-ultra-512gb-4gb-5-used, listing3533954124.
        var attributes = new Dictionary<string,string> { ["ram_size"] = "4GB", ["ram_gb"] = "4", ["screen_size"] = "5\"", ["screen_size_inch"] = "5", ["storage_size"] = "512GB", ["storage_gb"] = "512" };
        KnownProductAttributeConstraint.Apply(Product, attributes);
        Assert.That(attributes.Keys, Is.EquivalentTo(new[] { "storage_size", "storage_gb" }));
        Assert.That(attributes["storage_size"], Is.EqualTo("512GB"));
    }

    [TestCase("12GB", "256GB")]
    [TestCase("16GB", "512GB")]
    [TestCase("16GB", "1TB")]
    public void VerifiedDomesticAndChineseImportConfigurationsRemainValid(string ram, string storage)
    {
        var attributes = new Dictionary<string,string> { ["ram_size"] = ram, ["storage_size"] = storage, ["screen_size"] = "6.73\"" };
        Assert.That(KnownProductAttributeConstraint.Apply(Product, attributes), Is.Empty);
        Assert.That(attributes["ram_size"], Is.EqualTo(ram));
    }

    [Test]
    public void UnknownListingRamIsNotAutofilledFromCatalogueChoices()
    {
        var attributes = new Dictionary<string,string> { ["storage_size"] = "512GB" };
        KnownProductAttributeConstraint.Apply(Product, attributes);
        Assert.That(attributes.ContainsKey("ram_size"), Is.False);
        Assert.That(attributes.ContainsKey("ram_gb"), Is.False);
    }
}
