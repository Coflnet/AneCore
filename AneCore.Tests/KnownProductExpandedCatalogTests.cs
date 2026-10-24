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
        var imported = Catalog.Where(p => p.Source == ExpansionSource || p.Source == "seed:cameradatabase-2026-10").ToList();
        Assert.That(imported, Has.Count.GreaterThan(10_000));
        var failures = imported.Where(p => Matcher.Match(p.Name)?.Id != p.Id).Select(p =>
            $"{p.Id}: '{p.Name}' matched {Matcher.Match(p.Name)?.Id ?? "nothing"}");
        Assert.That(failures, Is.Empty);
    }

    [TestCase("Samsung NX3000", "samsung-nx3000")]
    [TestCase("Nvidia Quadro P2000", "nvidia-quadro-p2000")]
    [TestCase("AMD Radeon Pro WX 5100", "amd-radeon-pro-wx-5100")]
    [TestCase("Intel Arc Pro A40", "intel-arc-pro-a40")]
    [TestCase("Fujifilm X-T5", "fujifilm-x-t5")]
    [TestCase("Nikon D850", "nikon-d850")]
    [TestCase("Panasonic Lumix DC-GH7", "panasonic-lumix-dc-gh7")]
    [TestCase("Canon PowerShot G7 X Mark II", "canon-powershot-g7-x-mark-ii")]
    [TestCase("Olympus OM-D E-M10 Mark IV", "olympus-om-d-e-m10-mark-iv")]
    [TestCase("Dell U2723QE", "dell-u2723qe")]
    [TestCase("LG 27GP850-B", "lg-27gp850-b")]
    [TestCase("MSI MAG B650 TOMAHAWK WIFI", "msi-mag-b650-tomahawk-wifi")]
    [TestCase("Asus PRIME B550M-A WIFI II", "asus-prime-b550m-a-wifi-ii")]
    [TestCase("Sennheiser HD 650", "sennheiser-hd-650")]
    public void RealModelCodes_MatchWithOrdinaryListingText(string model, string expectedId)
    {
        Assert.That(Matcher.Match($"{model} gebraucht OVP")?.Id, Is.EqualTo(expectedId));
    }

    [TestCase("Fujifilm X-T")]
    [TestCase("Nikon Kamera")]
    [TestCase("Panasonic Lumix")]
    [TestCase("Quadro Grafikkarte")]
    [TestCase("Nokia Lumia 1020")]
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

    [TestCase("Navitech Black Camera Bag For The Canon EOS 400D Digital SLR Camera")]
    [TestCase("Digital DSLR Cameras Canon EOS 400D & Nikon D50 Working w/ Lenses")]
    [TestCase("XMG I7 4810MQ/16GB/SSD+HDD/GTX780M")]
    [TestCase("Bundle: i7-8700, 32GB DDR4, NVIDIA Quadro P2200")]
    [TestCase("Gaming Dator RTX 5060 Ti 16GB - Intel Core i5 14400F")]
    [TestCase("Nvidia Quadro P2000 Ersatzteil")]
    [TestCase("Fujifilm X-T5 Akku")]
    [TestCase("Panasonic Lumix DC-GH7 Tasche")]
    [TestCase("Gaming PC Nvidia Quadro P2000")]
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

    [TestCase("Intel Core i5-14400F Prozessor", "intel-core-i5-14400f")]
    [TestCase("Nvidia RTX 5060 Ti Grafikkarte", "nvidia-rtx-5060-ti")]
    [TestCase("Canon EOS 400D mit Kameratasche", "canon-eos-400d")]
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
    public void CameraAliases_DoNotClaimPhonesOrTheirCategories()
    {
        Assert.That(Matcher.Match("Samsung Galaxy S22")?.Id, Is.EqualTo("samsung-galaxy-s22"));
        Assert.That(Matcher.Match("Samsung NX3000", categoryPath: new[] { "Elektronik", "Kommunikationsgeräte", "Telefone", "Mobiltelefone" }), Is.Null);
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
