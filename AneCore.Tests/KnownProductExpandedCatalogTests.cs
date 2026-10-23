using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductExpandedCatalogTests
{
    private const string ExpansionSource = "seed:docyx-pc-part-dataset-2026-10";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void CategoryTaxonomy_IsAvailableInPublishedLayout()
    {
        Assert.That(File.Exists(Path.Combine(AppContext.BaseDirectory, "Categories", "UnifiedCategories.json")), Is.True,
            "The matcher needs the taxonomy in deployed output to reject incompatible categories.");
    }

    [Test]
    public void EveryImportedModel_ResolvesToItsOwnIdentity()
    {
        var imported = Catalog.Where(p => p.Source == ExpansionSource).ToList();
        Assert.That(imported, Has.Count.GreaterThan(10_000));
        var failures = imported.Where(p => Matcher.Match(p.Name)?.Id != p.Id).Select(p =>
            $"{p.Id}: '{p.Name}' matched {Matcher.Match(p.Name)?.Id ?? "nothing"}");
        Assert.That(failures, Is.Empty);
    }

    [TestCase("Dell U2723QE", "dell-u2723qe")]
    [TestCase("LG 27GP850-B", "lg-27gp850-b")]
    [TestCase("MSI MAG B650 TOMAHAWK WIFI", "msi-mag-b650-tomahawk-wifi")]
    [TestCase("Asus PRIME B550M-A WIFI II", "asus-prime-b550m-a-wifi-ii")]
    [TestCase("Sennheiser HD 650", "sennheiser-hd-650")]
    public void RealModelCodes_MatchWithOrdinaryListingText(string model, string expectedId)
    {
        Assert.That(Matcher.Match($"{model} gebraucht OVP")?.Id, Is.EqualTo(expectedId));
    }

    [TestCase("LG UltraGear")]
    [TestCase("Samsung Odyssey G5")]
    [TestCase("Samsung Odyssey G7")]
    [TestCase("HP OMEN Monitor")]
    [TestCase("Asus TUF Gaming Monitor")]
    [TestCase("EVGA Classified")]
    [TestCase("Asus B650 Mainboard")]
    public void AmbiguousFamilyOrChipset_DoesNotInventASpecificModel(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null);
    }

    [TestCase("Dell U2723QE Netzteil")]
    [TestCase("MSI MAG B650 TOMAHAWK WIFI Ersatzteil")]
    [TestCase("Sennheiser HD 650 Kabel")]
    [TestCase("Gaming PC MSI MAG B650 TOMAHAWK WIFI Ryzen 5 7600X")]
    public void PartsAndCompleteSystems_AreNotTheNamedDevice(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null);
    }

    [Test]
    public void MotherboardVariants_KeepWirelessRevisionAndPlusDistinct()
    {
        foreach (var title in new[] { "Asus PRIME B550M-A", "Asus PRIME B550M-A WIFI II", "Asus PRIME B550M-A/CSM" })
            Assert.That(Matcher.Match(title)?.Name, Is.EqualTo(title), title);
        Assert.That(Matcher.Match("Asus PRIME B650-PLUS")?.Id, Is.EqualTo("asus-prime-b650-plus"));
        Assert.That(Matcher.Match("Asus PRIME B650-PLUS WIFI")?.Id, Is.EqualTo("asus-prime-b650-plus-wifi"));
    }
}
