using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductReaderRecorderTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [TestCase("PocketBook 622 gebraucht", "pocketbook-622")]
    [TestCase("PocketBook Touch 622 E-Reader", "pocketbook-622")]
    [TestCase("PocketBook Touch Lux 623", "pocketbook-623")]
    [TestCase("PocketBook Touch Lux 2", "pocketbook-touch-lux-2")]
    [TestCase("PocketBook Touch Lux 3", "pocketbook-touch-lux-3")]
    [TestCase("PocketBook Basic Lux 2", "pocketbook-basic-lux-2")]
    [TestCase("PocketBook Basic Lux 3", "pocketbook-basic-lux-3")]
    [TestCase("Yamaha MDX595", "yamaha-mdx595")]
    [TestCase("Yamaha MDX-595", "yamaha-mdx595")]
    [TestCase("Yamaha Natural Sound Mini Disc Recorder MDX-595", "yamaha-mdx595")]
    [TestCase("LEGO 75305 Scout Trooper Helmet", "lego-75305")]
    public void ExactModels_ResolveWithoutExtractedPlatformAttributes(string title, string expected)
        => Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expected));

    [Test]
    public void ReviewedReaderAliases_AllResolveAndContainNoGuessedVariants()
    {
        var readers = Catalog.Where(p => p.Brand == "PocketBook").ToList();
        Assert.That(readers, Has.Count.EqualTo(30));
        foreach (var product in readers)
        {
            Assert.That(product.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "device_type" }));
            Assert.That(product.PossibleAttributes["device_type"], Is.EquivalentTo(new[] { "e-reader" }));
            foreach (var alias in product.Aliases)
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
        }
    }

    [TestCase("PocketBook")]
    [TestCase("PocketBook E-Reader")]
    [TestCase("PocketBook Touch")]
    [TestCase("PocketBook Touch Lux")]
    [TestCase("PocketBook 626")]
    [TestCase("PocketBook 631")]
    [TestCase("PocketBook 840")]
    [TestCase("PocketBook 6220")]
    [TestCase("PocketBook Touch Lux 20")]
    [TestCase("PocketBook Touch Lux 3 Hülle")]
    [TestCase("Akku für PocketBook 622")]
    [TestCase("PocketBook Basic Lux 2 Ladekabel")]
    [TestCase("Yamaha MDX-595 Ersatzteil")]
    [TestCase("Yamaha MDX-596")]
    [TestCase("LEGO 75305 Bauanleitung")]
    [TestCase("LEGO 75305 nur Karton")]
    [TestCase("LEGO 75305 unvollständig")]
    [TestCase("LEGO 75305 Light Kit")]
    [TestCase("LEGO 75305 + 75304")]
    public void AmbiguousModelsAndScopedOffers_DoNotClaimTheDevice(string title)
        => Assert.That(Matcher.Match(title), Is.Null);

    [TestCase("PocketBook Touch 622")]
    [TestCase("Yamaha MDX-595")]
    public void IncompatibleKnownCategory_RejectsDevice(string title)
        => Assert.That(Matcher.Match(title, categoryPath: new[] { "Bekleidung & Accessoires", "Schuhe" }), Is.Null);
}
