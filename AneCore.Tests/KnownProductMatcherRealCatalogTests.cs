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
    public void Sibling_SonyA7III_DoesNotMatchA7rIiiOrA7Iv()
    {
        // "A7 IV" was a documented catalogue gap (CameraDatabase's Sony coverage stops ~2019) - now
        // hand-curated (build_cameras_additional_sony), so all three must resolve to their own distinct
        // product rather than any of them being confused with each other.
        Assert.That(matcher.Match("Sony A7 III Body")!.Id, Is.EqualTo("sony-alpha-a7-iii"));
        Assert.That(matcher.Match("Sony A7R III Body")!.Id, Is.EqualTo("sony-alpha-a7r-iii"));
        Assert.That(matcher.Match("Sony A7 IV Body")!.Id, Is.EqualTo("sony-alpha-a7-iv"));
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

    // ===== Second pass: coordinator-reported missing families and truncated-model checks =====

    [Test]
    public void AirPodsPro2_BareGenerationNumber_Matches()
    {
        Assert.That(matcher.Match("airpods pro 2")!.Id, Is.EqualTo("apple-airpods-pro-2nd-generation"));
    }

    [Test]
    public void IPad9thGeneration_EveryNotation_Matches()
    {
        Assert.That(matcher.Match("Apple iPad 9.Generation 64GB")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9. Gen")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9th gen")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 2021")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
    }

    [Test]
    public void IPad9thGeneration_MultiLanguageNotations_Match()
    {
        Assert.That(matcher.Match("iPad 9e génération")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9ème génération")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9e generatie")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9a generazione")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
        Assert.That(matcher.Match("iPad 9° generazione")!.Id, Is.EqualTo("apple-ipad-9th-generation"));
    }

    [Test]
    public void IPadAir_BareGenerationNumber_DoesNotStealFromPlainIPad()
    {
        // The bare-number/year alias policy is scoped to the plain "iPad" base line only - "iPad Air"/
        // "iPad Pro"/"iPad mini" always carry their qualifier word, so they must not also pick up a bare
        // "iPad Air 4" form that could collide with anything.
        Assert.That(matcher.Match("iPad Air (4th generation) 64GB")!.Id, Is.EqualTo("apple-ipad-air-4th-generation"));
        Assert.That(matcher.Match("iPad Air 4"), Is.Null);
    }

    [Test]
    public void FiveGToken_NeverBlocksAMatch()
    {
        Assert.That(matcher.Match("Samsung Galaxy A54 5G 128GB")!.Id, Is.EqualTo("samsung-galaxy-a54"));
        Assert.That(matcher.Match("Samsung Galaxy S21 5G 128GB")!.Id, Is.EqualTo("samsung-galaxy-s21"));
        Assert.That(matcher.Match("iPhone 15 5G 128GB")!.Id, Is.EqualTo("apple-iphone-15"));
        Assert.That(matcher.Match("Xiaomi Redmi Note 12 5G")!.Id, Is.EqualTo("xiaomi-redmi-note-12"));
        // Still correctly vetoed as an accessory regardless of the "5G" token being present.
        Assert.That(matcher.Match("Samsung Galaxy A 54 5G Schutzglas"), Is.Null);
    }

    [Test]
    public void TruncatedModel_RedmiNote7_ResolvesToFullModelNotFamily()
    {
        // Live truncation: "Redmi Note 7" was seen elsewhere in the pipeline becoming "Xiaomi Redmi" -
        // the model was simply missing from the catalogue (min_year cutoff excluded its 2019 launch).
        // Now present and must resolve to the exact model.
        Assert.That(matcher.Match("Xiaomi Redmi Note 7 64GB")!.Id, Is.EqualTo("xiaomi-redmi-note-7"));
        Assert.That(matcher.Match("Xiaomi Redmi Note 7 Pro 128GB")!.Id, Is.EqualTo("xiaomi-redmi-note-7-pro"));
    }

    [Test]
    public void TruncatedModel_OnePlus9Pro_ResolvesToFullModelNotFamily()
    {
        // Live truncation: "OnePlus 9 Pro" was seen becoming "OnePlus" - OnePlus had no catalogue entries
        // at all before this pass.
        Assert.That(matcher.Match("OnePlus 9 Pro 256GB")!.Id, Is.EqualTo("oneplus-9-pro"));
        Assert.That(matcher.Match("OnePlus 9 256GB")!.Id, Is.EqualTo("oneplus-9"));
    }

    [Test]
    public void OnePlus_NeverMatchesOnABareModelNumberAlone()
    {
        // Regression found via the evaluation re-run: OnePlus's numbered line has no distinctive
        // product-line word once the brand is stripped (unlike Samsung "Galaxy"/Xiaomi "Redmi"), so an
        // early version of this catalogue's OnePlus entries carried a dangerously generic bare alias
        // ("10 Pro", "Open") that matched unrelated products naming the same bare phrase -
        // "Xiaomi Mi Note 10 Pro" matched "oneplus-10-pro" and "Bose Ultra Open Earbuds" matched
        // "oneplus-open". Every OnePlus alias must include the "OnePlus" brand word.
        Assert.That(matcher.Match("Xiaomi Mi Note 10 Pro 256GB guter Zustand OVP"), Is.Null);
        Assert.That(matcher.Match("Bose Ultra Open Earbuds Bluetooth schwarz"), Is.Null);
    }

    [Test]
    public void TruncatedModel_GalaxyS25Ultra_ResolvesToFullModelNotFamily()
    {
        // Live truncation: "S25 Ultra" was seen becoming "Samsung" - the full model was already present
        // in the catalogue (added in this pass's first commit), confirming the truncation happens
        // upstream of AneCore's matcher, not inside it (no bare "Samsung"/"Galaxy" alias exists anywhere
        // in the catalogue that could produce that result here).
        Assert.That(matcher.Match("Samsung Galaxy S25 Ultra 256GB")!.Id, Is.EqualTo("samsung-galaxy-s25-ultra"));
    }

    [Test]
    public void Nvidia_Gtx970And1080_Match()
    {
        Assert.That(matcher.Match("MSI GeForce GTX 970 4GB")!.Id, Is.EqualTo("nvidia-gtx-970"));
        Assert.That(matcher.Match("Nvidia GeForce GTX 1080 Ti 11GB")!.Id, Is.EqualTo("nvidia-gtx-1080-ti"));
        Assert.That(matcher.Match("Nvidia GeForce GTX 1080 8GB")!.Id, Is.EqualTo("nvidia-gtx-1080"));
    }

    [Test]
    public void AppleDesktops_IMacAndMacMini_Match()
    {
        Assert.That(matcher.Match("Apple iMac 24-inch (M1, 2021) 256GB")!.Id, Is.EqualTo("apple-imac-24-inch-m1-2021"));
        Assert.That(matcher.Match("Apple Mac mini (M4, 2024)")!.Id, Is.EqualTo("apple-mac-mini-m4-2024"));
        Assert.That(matcher.Match("Apple Mac mini (M4 Pro, 2024)")!.Id, Is.EqualTo("apple-mac-mini-m4-pro-2024"));
    }

    [Test]
    public void MetaQuest_AllModels_Match()
    {
        Assert.That(matcher.Match("Meta Quest 2 128GB")!.Id, Is.EqualTo("meta-quest-2"));
        Assert.That(matcher.Match("Oculus Quest 2 256GB")!.Id, Is.EqualTo("meta-quest-2"));
        Assert.That(matcher.Match("Meta Quest 3 512GB")!.Id, Is.EqualTo("meta-quest-3"));
        Assert.That(matcher.Match("Meta Quest 3S 128GB")!.Id, Is.EqualTo("meta-quest-3s"));
        Assert.That(matcher.Match("Meta Quest Pro")!.Id, Is.EqualTo("meta-quest-pro"));
    }

    [Test]
    public void MicrosoftSurface_ProAndLaptop_Match()
    {
        Assert.That(matcher.Match("Microsoft Surface Pro 9 256GB")!.Id, Is.EqualTo("microsoft-surface-pro-9"));
        Assert.That(matcher.Match("Microsoft Surface Laptop 6")!.Id, Is.EqualTo("microsoft-surface-laptop-6"));
        Assert.That(matcher.Match("Microsoft Surface Go 4")!.Id, Is.EqualTo("microsoft-surface-go-4"));
    }

    [Test]
    public void Dell_XpsAndLatitude_Match()
    {
        Assert.That(matcher.Match("Dell XPS 13 i7 16GB")!.Id, Is.EqualTo("dell-xps-13"));
        Assert.That(matcher.Match("Dell Latitude 5420 i5")!.Id, Is.EqualTo("dell-latitude-5420"));
        Assert.That(matcher.Match("Dell Latitude 7440 i7")!.Id, Is.EqualTo("dell-latitude-7440"));
    }

    [Test]
    public void IPhone_OlderGenerations_Match()
    {
        Assert.That(matcher.Match("Apple iPhone 6 64GB")!.Id, Is.EqualTo("apple-iphone-6"));
        Assert.That(matcher.Match("Apple iPhone 6s Plus 32GB")!.Id, Is.EqualTo("apple-iphone-6s-plus"));
        Assert.That(matcher.Match("Apple iPhone 7 128GB")!.Id, Is.EqualTo("apple-iphone-7"));
        Assert.That(matcher.Match("Apple iPhone 8 Plus 64GB")!.Id, Is.EqualTo("apple-iphone-8-plus"));
        Assert.That(matcher.Match("Apple iPhone X 256GB")!.Id, Is.EqualTo("apple-iphone-x"));
        Assert.That(matcher.Match("Apple iPhone XR 128GB")!.Id, Is.EqualTo("apple-iphone-xr"));
        Assert.That(matcher.Match("Apple iPhone XS Max 256GB")!.Id, Is.EqualTo("apple-iphone-xs-max"));
    }

    [Test]
    public void Sibling_IPhone6_DoesNotMatch6sOr6Plus()
    {
        Assert.That(matcher.Match("Apple iPhone 6 64GB")!.Id, Is.EqualTo("apple-iphone-6"));
        Assert.That(matcher.Match("Apple iPhone 6s 64GB")!.Id, Is.EqualTo("apple-iphone-6s"));
        Assert.That(matcher.Match("Apple iPhone 6 Plus 64GB")!.Id, Is.EqualTo("apple-iphone-6-plus"));
    }

    [Test]
    public void IPhoneSE_Generations_EveryNotation_Match()
    {
        Assert.That(matcher.Match("Apple iPhone SE (2nd generation) 64GB")!.Id, Is.EqualTo("apple-iphone-se-2nd-generation"));
        Assert.That(matcher.Match("iPhone SE 2020")!.Id, Is.EqualTo("apple-iphone-se-2nd-generation"));
        Assert.That(matcher.Match("iPhone SE 2")!.Id, Is.EqualTo("apple-iphone-se-2nd-generation"));
        Assert.That(matcher.Match("iPhone SE 3")!.Id, Is.EqualTo("apple-iphone-se-3rd-generation"));
        Assert.That(matcher.Match("iPhone SE 2022")!.Id, Is.EqualTo("apple-iphone-se-3rd-generation"));
    }

    [Test]
    public void IPhone17And18Line_Match()
    {
        Assert.That(matcher.Match("Apple iPhone 17 256GB")!.Id, Is.EqualTo("apple-iphone-17"));
        Assert.That(matcher.Match("Apple iPhone Air 256GB")!.Id, Is.EqualTo("apple-iphone-air"));
        Assert.That(matcher.Match("Apple iPhone 17 Pro Max 1TB")!.Id, Is.EqualTo("apple-iphone-17-pro-max"));
        Assert.That(matcher.Match("Apple iPhone 18 Pro 256GB")!.Id, Is.EqualTo("apple-iphone-18-pro"));
        Assert.That(matcher.Match("Apple iPhone 18 Pro Max 1TB Burgundy")!.Id, Is.EqualTo("apple-iphone-18-pro-max"));
    }

    [Test]
    public void Sony_NewerBodies_Match()
    {
        Assert.That(matcher.Match("Sony A7 IV Body")!.Id, Is.EqualTo("sony-alpha-a7-iv"));
        Assert.That(matcher.Match("Sony A7R V Body")!.Id, Is.EqualTo("sony-alpha-a7r-v"));
        Assert.That(matcher.Match("Sony A9 III Body")!.Id, Is.EqualTo("sony-alpha-a9-iii"));
        Assert.That(matcher.Match("Sony A1 Body")!.Id, Is.EqualTo("sony-alpha-a1"));
        Assert.That(matcher.Match("Sony ILCE-7M4 Body")!.Id, Is.EqualTo("sony-alpha-a7-iv"));
    }

    [Test]
    public void Headphones_NewlyAdded_Match()
    {
        Assert.That(matcher.Match("Sony WH-1000XM4 Kopfhörer")!.Id, Is.EqualTo("sony-wh-1000xm4"));
        Assert.That(matcher.Match("Sony WH-1000XM5 Kopfhörer")!.Id, Is.EqualTo("sony-wh-1000xm5"));
        Assert.That(matcher.Match("Bose QuietComfort 45 Kopfhörer")!.Id, Is.EqualTo("bose-quietcomfort-45"));
        Assert.That(matcher.Match("Bose QC45 Kopfhörer")!.Id, Is.EqualTo("bose-quietcomfort-45"));
    }

    // ===== An accessory word that is only an extra of the offer ("inkl. Zubehör", "mit Controller") is not an accessory listing. =====
    // Real titles from the live Switch / PlayStation / AirPods products; before the change every one of them fell back to the
    // extractor result because the blunt accessory word list vetoed the match (the reason ane_known_product_fallback_total is ten times
    // the matched counter, next to clothing and cards, which the catalogue does not cover).

    [TestCase("Nintendo Switch OLED inkl. Zubehör & 3 Mario Spiele", "nintendo-switch-oled")]
    [TestCase("Nintendo Switch OLED Weiß mit Zubehör & OVP", "nintendo-switch-oled")]
    [TestCase("Nintendo Switch Konsole mit viel Zubehör für 4 Spieler", "nintendo-switch")]
    [TestCase("Nintendo Switch 2 mit Spielen und Zubehör", "nintendo-switch-2")]
    [TestCase("PS4 Konsole 500GB schwarz mit Controller und Kabeln", "sony-playstation-4")]
    [TestCase("Sony PlayStation 4 Konsole mit Controller und Zubehör", "sony-playstation-4")]
    [TestCase("Playstation 5 mit original Zubehör und 2 Spielen", "sony-playstation-5")]
    [TestCase("Sony PlayStation 5 Konsole mit Controller, Headset und Spiel", "sony-playstation-5")]
    [TestCase("Apple AirPods Pro 2. Generation mit MagSafe Ladecase", "apple-airpods-pro-2nd-generation")]
    [TestCase("Nintendo Switch ohne Controller", "nintendo-switch")]
    [TestCase("Nintendo Switch Zubehör inklusive", "nintendo-switch")]
    public void AccessoryWordThatIsAnIncludedExtra_StillMatchesTheDevice(string title, string expectedId)
    {
        Assert.That(matcher.Match(title)?.Id, Is.EqualTo(expectedId), title);
    }

    [TestCase("Nintendo Switch Zubehör Set")]
    [TestCase("Zubehör für Nintendo Switch")]
    [TestCase("Hülle mit Ständer für PlayStation 5 Controller")]
    [TestCase("Galaxy S21+ Hülle")]
    [TestCase("PS5 Controller Ladekabel")]
    [TestCase("Nintendo Switch Tragetasche")]
    [TestCase("Nintendo Switch OLED Skin mit Schutzfolie")]
    public void AccessoryTitle_IsStillAnAccessory(string title)
    {
        Assert.That(matcher.Match(title), Is.Null, title);
    }
}
