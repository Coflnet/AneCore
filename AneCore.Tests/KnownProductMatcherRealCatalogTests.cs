using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// Matcher tests against the REAL embedded seed catalogue (not synthetic fixtures) for every sibling/
/// container/accessory case named in the catalogue-expansion task, so a seed data mistake (missing
/// exclude term, wrong alias) fails here even if the synthetic KnownProductMatcherEngineTests still pass.
/// </summary>
[TestFixture]
public class KnownProductMatcherRealCatalogTests
{
    private KnownProductMatcher matcher = null!;

    [SetUp]
    public void SetUp()
    {
        matcher = new KnownProductMatcher(KnownProductSeed.LoadAll(includeExpandedCatalog: true));
    }

    [Test]
    public void Gpu_Rtx3060_DoesNotMatchRtx3060Ti()
    {
        Assert.That(matcher.Match("MSI GeForce RTX 3060 Ti 12GB")!.Id, Is.EqualTo("nvidia-rtx-3060-ti"));
        Assert.That(matcher.Match("MSI GeForce RTX 3060 12GB")!.Id, Is.EqualTo("nvidia-rtx-3060"));
    }

    [Test]
    public void Console_Ps5_DoesNotMatchProOrDigitalEdition()
    {
        Assert.That(matcher.Match("Sony PlayStation 5 Pro 2TB")!.Id, Is.EqualTo("sony-playstation-5-pro"));
        Assert.That(matcher.Match("Sony PlayStation 5 Digital Edition")!.Id, Is.EqualTo("sony-playstation-5-digital-edition"));
        Assert.That(matcher.Match("Sony PlayStation 5 825GB")!.Id, Is.EqualTo("sony-playstation-5"));
    }

    [Test]
    public void Console_XboxSeriesS_DoesNotMatchSeriesX()
    {
        Assert.That(matcher.Match("Microsoft Xbox Series X 1TB")!.Id, Is.EqualTo("microsoft-xbox-series-x"));
        Assert.That(matcher.Match("Microsoft Xbox Series S 512GB")!.Id, Is.EqualTo("microsoft-xbox-series-s"));
    }

    [Test]
    public void Pixel_Pixel8_DoesNotMatch8aOr8Pro()
    {
        Assert.That(matcher.Match("Google Pixel 8a 128GB")!.Id, Is.EqualTo("google-pixel-8a"));
        Assert.That(matcher.Match("Google Pixel 8 Pro 256GB")!.Id, Is.EqualTo("google-pixel-8-pro"));
        Assert.That(matcher.Match("Google Pixel 8 128GB")!.Id, Is.EqualTo("google-pixel-8"));
    }

    [Test]
    public void Samsung_GalaxyS21_DoesNotMatchUltraOrFe()
    {
        Assert.That(matcher.Match("Samsung Galaxy S21 Ultra 256GB")!.Id, Is.EqualTo("samsung-galaxy-s21-ultra"));
        Assert.That(matcher.Match("Samsung Galaxy S21 FE 128GB")!.Id, Is.EqualTo("samsung-galaxy-s21-fe"));
        Assert.That(matcher.Match("Samsung Galaxy S21 128GB")!.Id, Is.EqualTo("samsung-galaxy-s21"));
    }

    [Test]
    public void Cpu_ContainerVeto_GamingPcListingWithCpuAndGpu_MatchesNeither()
    {
        var result = matcher.Match("Gaming PC AMD Ryzen 5 5600X RTX 3060 16GB RAM 1TB SSD");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Accessory_HuelleForGalaxyS21_ReturnsNull()
    {
        Assert.That(matcher.Match("Handyhülle für Samsung Galaxy S21"), Is.Null);
    }

    [Test]
    public void Accessory_ControllerFuerPs5_ReturnsNull()
    {
        Assert.That(matcher.Match("Controller für PS5"), Is.Null);
    }

    // The next four are regressions from the real-listing-title evaluation (tools/catalog-import/eval) -
    // each of these titles matched the console/GPU itself before the AccessoryWords list was extended.
    [Test]
    public void Accessory_NintendoSwitchTasche_ReturnsNull()
    {
        Assert.That(matcher.Match("Nintendo Switch Tasche"), Is.Null);
        Assert.That(matcher.Match("Nintendo Switch Tragetasche schwarz"), Is.Null);
    }

    [Test]
    public void Accessory_Ps5Skin_ReturnsNull()
    {
        Assert.That(matcher.Match("Skin ps5 rosa gta6"), Is.Null);
    }

    [Test]
    public void Accessory_Ps5Luefter_ReturnsNull()
    {
        Assert.That(matcher.Match("lüfter PS 5"), Is.Null);
    }

    [Test]
    public void Accessory_NintendoSwitchGamingHeadset_ReturnsNull()
    {
        Assert.That(matcher.Match("Nintendo Switch Gaming-Headset mit Mikrofon – voll funktionsfähig"), Is.Null);
    }

    [Test]
    public void NonAccessory_NintendoSwitchConsoleListing_StillMatches()
    {
        // Guard against the accessory-word additions above being too broad.
        Assert.That(matcher.Match("Nintendo Switch Konsole - Top Zustand")!.Id, Is.EqualTo("nintendo-switch"));
    }

    [Test]
    public void Watch_AppleWatchSeries9_DoesNotFalseMatchSe()
    {
        var result = matcher.Match("Apple Watch Series 9 45mm GPS");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("apple-watch-series-9"));
    }
}
