using System.Diagnostics;
using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// Tests for the matcher's engine-level behaviour (perf, container veto, category plausibility) added
/// while extending the known-products catalogue beyond iPhones. The pre-existing alias/accessory/brand
/// tests for the Apple seed continue to live in AneNotifier's KnownProductCore.Tests.cs (AneCore had no
/// test project before); this file only covers behaviour that seed alone can't exercise well.
/// </summary>
[TestFixture]
public class KnownProductMatcherEngineTests
{
    private static KnownProduct Gpu(string id, string name, string alias, params string[] excludeTerms) => new()
    {
        Id = id,
        Brand = "Nvidia",
        Model = name,
        Name = name,
        Categories = new List<string> { "Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Computer-Steckkarten", "Grafikkarten & Videoadapter" },
        Vertical = "electronics",
        Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias },
        ExcludeTerms = new HashSet<string>(excludeTerms, StringComparer.OrdinalIgnoreCase),
    };

    private static KnownProduct Cpu(string id, string name, string alias) => new()
    {
        Id = id,
        Brand = "AMD",
        Model = name,
        Name = name,
        Categories = new List<string> { "Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Prozessoren" },
        Vertical = "electronics",
        Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias },
    };

    private static KnownProduct Laptop(string id, string name, string alias) => new()
    {
        Id = id,
        Brand = "Lenovo",
        Model = name,
        Name = name,
        Categories = new List<string> { "Elektronik", "Computer", "Laptops" },
        Vertical = "electronics",
        Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias },
    };

    private static KnownProduct Console(string id, string name, string alias) => new()
    {
        Id = id,
        Brand = "Sony",
        Model = name,
        Name = name,
        Categories = new List<string> { "Elektronik", "Videospielkonsolen" },
        Vertical = "electronics",
        Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias },
    };

    private static KnownProduct Phone(string id, string name, string alias) => new()
    {
        Id = id,
        Brand = "Samsung",
        Model = name,
        Name = name,
        Categories = new List<string> { "Elektronik", "Kommunikationsgeräte", "Telefone", "Mobiltelefone" },
        Vertical = "electronics",
        Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias },
    };

    [TestCase("Galaxy S21+", "galaxy s 21 plus")]
    [TestCase("Galaxy S21 Plus", "galaxy s 21 plus")]
    [TestCase("Galaxy S21", "galaxy s 21")]
    public void Normalize_PlusSign_BecomesPlusWord_SoPlusAndNonPlusAreDistinctTokenSequences(string input, string expected)
    {
        // Regression: "+" used to be silently stripped by the [^a-z0-9 ] catch-all, making "Galaxy S21+"
        // normalize to the exact same tokens as "Galaxy S21" - permanently ambiguous between the two
        // real, differently-priced products. Found while seeding Samsung's S21+/S22+/... line.
        Assert.That(KnownProductMatcher.Normalize(input), Is.EqualTo(expected));
    }

    [Test]
    public void SiblingExclusion_GalaxyS21_DoesNotMatchS21Plus()
    {
        var basePhone = new KnownProduct
        {
            Id = "samsung-galaxy-s21", Brand = "Samsung", Model = "Galaxy S21", Name = "Samsung Galaxy S21",
            Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Galaxy S21" },
            ExcludeTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "plus" },
        };
        var plusPhone = new KnownProduct
        {
            Id = "samsung-galaxy-s21-plus", Brand = "Samsung", Model = "Galaxy S21+", Name = "Samsung Galaxy S21+",
            Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Galaxy S21+" },
        };
        var matcher = new KnownProductMatcher(new[] { basePhone, plusPhone });

        Assert.That(matcher.Match("Samsung Galaxy S21+ 256GB")!.Id, Is.EqualTo("samsung-galaxy-s21-plus"));
        Assert.That(matcher.Match("Samsung Galaxy S21 128GB")!.Id, Is.EqualTo("samsung-galaxy-s21"));
    }

    [Test]
    public void ContainerVeto_GamingPcWithCpuAndGpu_MatchesNeither()
    {
        var products = new[]
        {
            Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060", "ti"),
            Cpu("amd-ryzen-5-5600", "Ryzen 5 5600", "ryzen 5 5600"),
        };
        var matcher = new KnownProductMatcher(products);

        var result = matcher.Match("Gaming PC Ryzen 5 5600 RTX 3060 16GB");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ContainerVeto_StandaloneGpuListing_StillMatches()
    {
        // Same GPU alias, no system wording and no second component - must not be swept up by the veto.
        var products = new[]
        {
            Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060", "ti"),
            Cpu("amd-ryzen-5-5600", "Ryzen 5 5600", "ryzen 5 5600"),
        };
        var matcher = new KnownProductMatcher(products);

        var result = matcher.Match("MSI RTX 3060 Ventus 12GB Grafikkarte");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("nvidia-rtx-3060"));
    }

    [Test]
    public void ContainerVeto_SingleComponentInSystemWordedTitle_StillMatches()
    {
        // Only one component alias present (no second distinct component) - the "Rechner"/system wording
        // alone must not be enough to veto a legitimate single-part listing.
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060", "ti") };
        var matcher = new KnownProductMatcher(products);

        var result = matcher.Match("Rechner Upgrade RTX 3060 12GB neu eingebaut");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void SiblingExclusion_Rtx3060_DoesNotMatchTi()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060", "ti") };
        var matcher = new KnownProductMatcher(products);

        Assert.That(matcher.Match("Gigabyte RTX 3060 Ti 12GB"), Is.Null);
        Assert.That(matcher.Match("Gigabyte RTX 3060 12GB"), Is.Not.Null);
    }

    [Test]
    public void CategoryPlausibility_GpuAliasUnderPhoneCategory_DoesNotMatch()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060") };
        var matcher = new KnownProductMatcher(products);

        var phoneCategory = new[] { "Elektronik", "Kommunikationsgeräte", "Telefone", "Mobiltelefone" };
        Assert.That(matcher.Match("RTX 3060 Aufkleber Handyhuelle", categoryPath: phoneCategory), Is.Null);
    }

    [Test]
    public void CategoryPlausibility_UnknownCategory_StillMatches()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060") };
        var matcher = new KnownProductMatcher(products);

        Assert.That(matcher.Match("MSI RTX 3060 12GB", categoryPath: null), Is.Not.Null);
        Assert.That(matcher.Match("MSI RTX 3060 12GB", categoryPath: Array.Empty<string>()), Is.Not.Null);
    }

    [Test]
    public void CategoryPlausibility_MatchingCategory_StillMatches()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060") };
        var matcher = new KnownProductMatcher(products);
        var gpuCategory = new[] { "Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Computer-Steckkarten", "Grafikkarten & Videoadapter" };

        Assert.That(matcher.Match("MSI RTX 3060 12GB", categoryPath: gpuCategory), Is.Not.Null);
    }

    // Regression tests for the generic-taxonomy-root veto fix: every real product/listing category path
    // carries the taxonomy's generic top-level root (e.g. "Elektronik"), so comparing raw paths never
    // vetoed anything within the same vertical - see the "Lego Worlds PS4" false-match this fixes.
    // IsCategoryCompatible now ignores taxonomy root labels (derived from Categories/UnifiedCategories.json,
    // not hardcoded) on both sides before comparing.

    [Test]
    public void CategoryPlausibility_VideoGameTitleSharingOnlyGenericRoot_DoesNotMatchConsole()
    {
        // "PC- & Videospiele" is a real UnifiedCategories.json label (normally paired with the "Software"
        // root there); used here as the listing's own non-root label, paired with "Elektronik" the way a
        // real marketplace listing path does, to prove the veto now looks past the shared generic root
        // instead of accepting any candidate that merely shares "Elektronik".
        var products = new[] { Console("sony-ps4", "PlayStation 4", "ps4") };
        var matcher = new KnownProductMatcher(products);
        var videoGameCategory = new[] { "Elektronik", "PC- & Videospiele" };

        Assert.That(matcher.Match("Lego Worlds PS4", categoryPath: videoGameCategory), Is.Null);
    }

    [Test]
    public void CategoryPlausibility_ConsoleListingWithOwnCategory_Matches()
    {
        var products = new[] { Console("sony-ps4", "PlayStation 4", "ps4") };
        var matcher = new KnownProductMatcher(products);
        var consoleCategory = new[] { "Elektronik", "Videospielkonsolen" };

        Assert.That(matcher.Match("Sony PS4 500GB", categoryPath: consoleCategory)?.Id, Is.EqualTo("sony-ps4"));
    }

    [Test]
    public void CategoryPlausibility_PhoneListingWithRealisticFullPath_Matches()
    {
        var products = new[] { Phone("samsung-galaxy-s21", "Galaxy S21", "galaxy s21") };
        var matcher = new KnownProductMatcher(products);
        var phoneCategory = new[] { "Elektronik", "Kommunikationsgeräte", "Telefone", "Mobiltelefone" };

        Assert.That(matcher.Match("Samsung Galaxy S21 128GB", categoryPath: phoneCategory)?.Id, Is.EqualTo("samsung-galaxy-s21"));
    }

    [Test]
    public void CategoryPlausibility_GpuListingWithRealisticFullPath_Matches()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060") };
        var matcher = new KnownProductMatcher(products);
        var gpuCategory = new[] { "Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Computer-Steckkarten", "Grafikkarten & Videoadapter" };

        Assert.That(matcher.Match("MSI RTX 3060 12GB Grafikkarte", categoryPath: gpuCategory)?.Id, Is.EqualTo("nvidia-rtx-3060"));
    }

    [Test]
    public void CategoryPlausibility_LaptopListingWithRealisticFullPath_Matches()
    {
        var products = new[] { Laptop("lenovo-thinkpad-t14", "ThinkPad T14", "thinkpad t14") };
        var matcher = new KnownProductMatcher(products);
        var laptopCategory = new[] { "Elektronik", "Computer", "Laptops" };

        Assert.That(matcher.Match("Lenovo ThinkPad T14 16GB RAM", categoryPath: laptopCategory)?.Id, Is.EqualTo("lenovo-thinkpad-t14"));
    }

    [Test]
    public void CategoryPlausibility_ListingPathOnlyMarketplaceLabels_MatchAllowed()
    {
        // "Konsolen" is real marketplace-tree vocabulary that does not appear anywhere in
        // UnifiedCategories.json at all - must not cause a false veto (treated as unknown category).
        var products = new[] { Console("sony-ps4", "PlayStation 4", "ps4") };
        var matcher = new KnownProductMatcher(products);
        var marketplaceOnlyCategory = new[] { "Konsolen" };

        Assert.That(matcher.Match("Sony PS4 500GB", categoryPath: marketplaceOnlyCategory)?.Id, Is.EqualTo("sony-ps4"));
    }

    [Test]
    public void CategoryPlausibility_ListingPathOnlyGenericRoot_MatchAllowed()
    {
        var products = new[] { Gpu("nvidia-rtx-3060", "RTX 3060", "rtx 3060") };
        var matcher = new KnownProductMatcher(products);
        var rootOnlyCategory = new[] { "Elektronik" };

        Assert.That(matcher.Match("MSI RTX 3060 12GB", categoryPath: rootOnlyCategory), Is.Not.Null);
    }

    [Test]
    public void CategoryPlausibility_ClothingPathAgainstElectronicsProduct_DoesNotMatch()
    {
        var products = new[] { Phone("samsung-galaxy-s21", "Galaxy S21", "galaxy s21") };
        var matcher = new KnownProductMatcher(products);
        var clothingCategory = new[] { "Bekleidung & Accessoires", "Bekleidung", "Hosen" };

        Assert.That(matcher.Match("Samsung Galaxy S21 128GB", categoryPath: clothingCategory), Is.Null);
    }

    [Test]
    public void Accessory_ControllerFuerPs5_ReturnsNull()
    {
        var products = new[] { Laptop("sony-ps5", "PlayStation 5", "playstation 5") };
        var matcher = new KnownProductMatcher(products);
        Assert.That(matcher.Match("Controller für PS5"), Is.Null);
    }

    [Test]
    public void Accessory_AkkuFuerLaptop_ReturnsNull()
    {
        var products = new[] { Laptop("lenovo-thinkpad-t14", "ThinkPad T14", "thinkpad t14") };
        var matcher = new KnownProductMatcher(products);
        Assert.That(matcher.Match("Akku für ThinkPad T14"), Is.Null);
    }

    [Test]
    public void Accessory_TascheFuerKamera_ReturnsNull()
    {
        var products = new[] { Laptop("canon-eos-r6", "Canon EOS R6", "eos r6") };
        var matcher = new KnownProductMatcher(products);
        Assert.That(matcher.Match("Tasche für Canon EOS R6 Kamera"), Is.Null);
    }

    [Test]
    public void Accessory_LeerkartonAndOvpLeer_ReturnNull()
    {
        var products = new[] { Laptop("lenovo-thinkpad-t14", "ThinkPad T14", "thinkpad t14") };
        var matcher = new KnownProductMatcher(products);
        Assert.That(matcher.Match("ThinkPad T14 Leerkarton"), Is.Null);
        Assert.That(matcher.Match("ThinkPad T14 nur OVP"), Is.Null);
        Assert.That(matcher.Match("ThinkPad T14 OVP leer"), Is.Null);
    }

    [Test]
    public void Performance_TenThousandMatchesOnFullSeed_CompletesWithinBound()
    {
        var seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
        var matcher = new KnownProductMatcher(seed);
        var titles = new[]
        {
            "Apple iPhone 14 Pro Max 256GB Space Black neuwertig",
            "Samsung Galaxy S21 Ultra 128GB gebraucht",
            "Handyhülle für iPhone 13",
            "RTX 3060 Ti 12GB Grafikkarte",
            "Gaming PC Ryzen 5 5600 RTX 3060 16GB",
        };

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10_000; i++)
            matcher.Match(titles[i % titles.Length]);
        sw.Stop();

        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000),
            $"10k Match calls took {sw.ElapsedMilliseconds}ms - matcher is likely doing an O(products*aliases) scan again");
    }

    [Test]
    public void Matcher_BuildsIndexOnceInConstructor_NotOnEachMatchCall()
    {
        // Indirect check: constructing once and calling Match many times must be far cheaper per call
        // than constructing a new matcher per call would be - guards against reintroducing per-call index
        // rebuilding inside Match itself.
        var seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
        var matcher = new KnownProductMatcher(seed);

        var swManyMatches = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
            matcher.Match("Apple iPhone 14 Pro Max 256GB");
        swManyMatches.Stop();

        var swManyConstructs = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
            _ = new KnownProductMatcher(seed).Match("Apple iPhone 14 Pro Max 256GB");
        swManyConstructs.Stop();

        Assert.That(swManyMatches.ElapsedMilliseconds, Is.LessThan(swManyConstructs.ElapsedMilliseconds),
            "Matching on a pre-built matcher should be meaningfully cheaper than rebuilding the matcher per call");
    }
}
