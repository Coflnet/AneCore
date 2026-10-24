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

    [TestCase("Desktop PC / Rechner Intel Core i7 4790K Win 10")]
    [TestCase("Gaming PC Intel Core i7-6700 16GB DDR4")]
    [TestCase("Mainboard Fujitsu D3401-B21 + Intel Core i7-6700")]
    public void SingleRecognizedCpu_InWholeSystemOrBoardBundle_DoesNotMatch(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null);
    }

    [TestCase("Intel Core i7-6700 Desktop Prozessor", "intel-core-i7-6700")]
    [TestCase("Intel Core i7-6700 für Desktop PC", "intel-core-i7-6700")]
    [TestCase("Intel Core i7-6700 Mainboard kompatibel", "intel-core-i7-6700")]
    [TestCase("Crucial CT2K16G48C40U5 32 GB gebraucht", "crucial-ct2k16g48c40u5-32-gb")]
    [TestCase("Lexar NM790 2TB SSD gebraucht", "lexar-nm790")]
    public void ExactComponents_KeepTheirIdentity(string title, string expectedId)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expectedId));
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
