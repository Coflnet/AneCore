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

    // Spelling-variant recall gaps from the real-title evaluation (EVALUATION.md "Language/spelling
    // variants") - German "Serie"/French "Série", "I Phone"/"X Box" (space-separated).
    [Test]
    public void SpellingVariant_AppleWatchSerieGerman_MatchesSeries3()
    {
        Assert.That(matcher.Match("Apple Watch Serie 3 42mm")!.Id, Is.EqualTo("apple-watch-series-3"));
    }

    [Test]
    public void SpellingVariant_AppleWatchSerieFrench_MatchesSeries3()
    {
        Assert.That(matcher.Match("Apple Watch Série 3 42mm")!.Id, Is.EqualTo("apple-watch-series-3"));
    }

    [Test]
    public void SpellingVariant_XboxSerieS_MatchesSeriesS()
    {
        Assert.That(matcher.Match("Xbox serie s 512GB")!.Id, Is.EqualTo("microsoft-xbox-series-s"));
    }

    [Test]
    public void SpellingVariant_IPhoneSpaceSeparated_MatchesIphone13()
    {
        Assert.That(matcher.Match("I Phone 13 128GB blau")!.Id, Is.EqualTo("apple-iphone-13"));
    }

    [Test]
    public void SpellingVariant_XBoxSpaceSeparated_MatchesXboxOneS()
    {
        Assert.That(matcher.Match("X Box One S 1TB")!.Id, Is.EqualTo("microsoft-xbox-one-s"));
    }

    // ===== Everyday-alias recall gaps from the task's diagnosed problem (MacBook/ThinkPad/camera) =====

    [Test]
    public void MacBookAir_M2_ChipAndSizeInEitherOrder_ResolveToExactVariant()
    {
        Assert.That(matcher.Match("Apple MacBook Air M2 13 256GB")!.Id, Is.EqualTo("apple-macbook-air-13-inch-m2"));
        Assert.That(matcher.Match("Apple MacBook Air 13 M2 256GB")!.Id, Is.EqualTo("apple-macbook-air-13-inch-m2"));
        Assert.That(matcher.Match("Apple MacBook Air M2 15 512GB")!.Id, Is.EqualTo("apple-macbook-air-15-inch-m2"));
        Assert.That(matcher.Match("Apple MacBook Air 15 M2 512GB")!.Id, Is.EqualTo("apple-macbook-air-15-inch-m2"));
    }

    [Test]
    public void MacBookAir_M2_WithInchOrZollWord_Matches()
    {
        Assert.That(matcher.Match("MacBook Air M2 13 inch")!.Id, Is.EqualTo("apple-macbook-air-13-inch-m2"));
        Assert.That(matcher.Match("MacBook Air 15 Zoll M2")!.Id, Is.EqualTo("apple-macbook-air-15-inch-m2"));
    }

    [Test]
    public void ThinkPad_T14_WithGenerationInEveryForm_ResolvesToExactGeneration()
    {
        Assert.That(matcher.Match("Lenovo ThinkPad T14 Gen 3 i5")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-3"));
        Assert.That(matcher.Match("Lenovo ThinkPad T14 G3 i5")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-3"));
        Assert.That(matcher.Match("Lenovo ThinkPad T14 G 3 i5")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-3"));
    }

    [Test]
    public void Camera_SonyA7III_EverydayForms_AllMatch()
    {
        Assert.That(matcher.Match("Sony A7 III Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("Sony Alpha 7 III Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("A7III Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("Sony A7 3 Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("Sony ILCE-7M3 Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
    }

    [Test]
    public void Camera_CanonEosR6MarkII_EverydayForms_AllMatch()
    {
        Assert.That(matcher.Match("Canon EOS R6")!.Id, Is.EqualTo("canon-eos-r6"));
        Assert.That(matcher.Match("Canon EOS R6 Mark II")!.Id, Is.EqualTo("canon-eos-r6-mark-ii"));
        Assert.That(matcher.Match("Canon R6 II")!.Id, Is.EqualTo("canon-eos-r6-mark-ii"));
    }

    [Test]
    public void Camera_NikonZ6II_EverydayForms_AllMatch()
    {
        Assert.That(matcher.Match("Nikon Z6 II")!.Id, Is.EqualTo("nikon-z6-ii"));
        Assert.That(matcher.Match("Nikon Z6ii")!.Id, Is.EqualTo("nikon-z6-ii"));
    }

    // ===== Ambiguity policy (task requirement 2): default to the smaller/base variant on a bare family
    // query; a query naming the distinguishing size/generation still resolves to the exact variant. =====

    [Test]
    public void Ambiguity_MacBookAirM2_BareQuery_DefaultsToSmallerBaseVariant()
    {
        Assert.That(matcher.Match("MacBook Air M2")!.Id, Is.EqualTo("apple-macbook-air-13-inch-m2"));
        Assert.That(matcher.Match("MacBook Air 2022")!.Id, Is.EqualTo("apple-macbook-air-13-inch-m2"));
    }

    [Test]
    public void Ambiguity_MacBookAirM2_ExactSizeTitle_StillResolvesToExactVariant()
    {
        // Same bare-default-eligible chip, but the listing title names the size - must not fall back to
        // the 13-inch default.
        Assert.That(matcher.Match("Apple MacBook Air M2 15-inch 512GB Space Gray")!.Id, Is.EqualTo("apple-macbook-air-15-inch-m2"));
    }

    [Test]
    public void Ambiguity_ThinkPadT14_BareQuery_DefaultsToLatestGeneration()
    {
        Assert.That(matcher.Match("ThinkPad T14")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-5"));
    }

    [Test]
    public void Ambiguity_ThinkPadT14_ExactGenerationTitle_StillResolvesToExactVariant()
    {
        Assert.That(matcher.Match("Lenovo ThinkPad T14 Gen 2 i5 16GB")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-2"));
    }

    [Test]
    public void Ambiguity_IPadAirM2_BareQuery_DefaultsToSmallerBaseVariant()
    {
        Assert.That(matcher.Match("iPad Air M2")!.Id, Is.EqualTo("apple-ipad-air-11-inch-m2"));
    }

    [Test]
    public void Ambiguity_IPadAirM2_ExactSizeTitle_StillResolvesToExactVariant()
    {
        Assert.That(matcher.Match("Apple iPad Air M2 13 256GB")!.Id, Is.EqualTo("apple-ipad-air-13-inch-m2"));
    }

    // ===== Accessories for the newly-aliased families must still be vetoed. =====

    [Test]
    public void Accessory_MacBookAirM2Huelle_ReturnsNull()
    {
        Assert.That(matcher.Match("MacBook Air M2 13 Hülle"), Is.Null);
    }

    [Test]
    public void Accessory_ThinkPadT14Netzteil_ReturnsNull()
    {
        Assert.That(matcher.Match("ThinkPad T14 Netzteil 65W"), Is.Null);
    }

    [Test]
    public void Accessory_SonyA7IIIAkku_ReturnsNull()
    {
        Assert.That(matcher.Match("Sony A7 III Akku Ersatz"), Is.Null);
    }

    // ===== Siblings must still not be confused after the alias expansion. =====

    [Test]
    public void Sibling_SonyA7III_DoesNotMatchA7rIii()
    {
        Assert.That(matcher.Match("Sony A7 III Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("Sony A7R III Body")!.Id, Is.EqualTo("sony-alpha-a7r-iii"));
        // "a7 iv" is a documented catalogue gap (tools/catalog-import/README.md - CameraDatabase's Sony
        // coverage stops ~2019, no A7 IV row exists), so it correctly matches nothing rather than being
        // confused with A7 III - this is the sibling non-confusion property applied to an absent sibling.
        Assert.That(matcher.Match("Sony A7 IV Body"), Is.Null);
    }

    [Test]
    public void Sibling_ThinkPadT14_DoesNotMatchT14s()
    {
        Assert.That(matcher.Match("Lenovo ThinkPad T14 i5")!.Id, Is.EqualTo("lenovo-thinkpad-t14-gen-5"));
        Assert.That(matcher.Match("Lenovo ThinkPad T14s i5")!.Id, Is.EqualTo("lenovo-thinkpad-t14s"));
    }

    [Test]
    public void Sibling_CanonEosR6_DoesNotMatchMarkII()
    {
        Assert.That(matcher.Match("Canon EOS R6 Body")!.Id, Is.EqualTo("canon-eos-r6"));
        Assert.That(matcher.Match("Canon EOS R6 Mark II Body")!.Id, Is.EqualTo("canon-eos-r6-mark-ii"));
    }

    // ===== Samsung Galaxy A-series (task requirement 4) - the highest-volume phone line, newly seeded. =====

    [Test]
    public void GalaxyA_A55_Matches()
    {
        Assert.That(matcher.Match("Samsung Galaxy A55 128GB Awesome Navy")!.Id, Is.EqualTo("samsung-galaxy-a55"));
    }

    [Test]
    public void GalaxyA_ModelCode_Matches()
    {
        Assert.That(matcher.Match("Samsung SM-A556B 128GB")!.Id, Is.EqualTo("samsung-galaxy-a55"));
    }

    [Test]
    public void Sibling_GalaxyA01_DoesNotMatchA01Core()
    {
        Assert.That(matcher.Match("Samsung Galaxy A01 16GB")!.Id, Is.EqualTo("samsung-galaxy-a01"));
        Assert.That(matcher.Match("Samsung Galaxy A01 Core 32GB")!.Id, Is.EqualTo("samsung-galaxy-a01-core"));
    }

    [Test]
    public void Sibling_GalaxyA02_DoesNotMatchA02s()
    {
        Assert.That(matcher.Match("Samsung Galaxy A02 32GB")!.Id, Is.EqualTo("samsung-galaxy-a02"));
        Assert.That(matcher.Match("Samsung Galaxy A02s 32GB")!.Id, Is.EqualTo("samsung-galaxy-a02s"));
    }

    [Test]
    public void Accessory_GalaxyA55Huelle_ReturnsNull()
    {
        Assert.That(matcher.Match("Silikon Hülle für Samsung Galaxy A55"), Is.Null);
    }

    [Test]
    public void Sibling_AppleWatchSE_DoesNotMatchSe2ndGeneration()
    {
        // Regression from this pass's real-title evaluation re-run: "Aple Watch SE Gen 2. 40mm" matched
        // the plain (1st-gen) SE instead of the 2nd-gen one before "Gen N" forms were added for "(Nth
        // generation)" names.
        Assert.That(matcher.Match("Apple Watch SE 40mm")!.Id, Is.EqualTo("apple-watch-se"));
        Assert.That(matcher.Match("Apple Watch SE Gen 2 40mm")!.Id, Is.EqualTo("apple-watch-se-2nd-generation"));
        Assert.That(matcher.Match("Watch SE Gen 2. 40mm - 2 Jahre alt")!.Id, Is.EqualTo("apple-watch-se-2nd-generation"));
    }

    [Test]
    public void Accessory_GalaxyA54Schutzglas_ReturnsNull()
    {
        // Regression from this pass's real-title evaluation re-run: "Samsung Galaxy A 54 5G Schutzglas"
        // (a tempered-glass screen protector listing) matched the phone before "schutzglas" was added to
        // KnownProductMatcher.AccessoryWords.
        Assert.That(matcher.Match("Samsung Galaxy A 54 5G Schutzglas"), Is.Null);
    }
}
