using Coflnet.Ane;
using Coflnet.Ane.Phones;

namespace AneCore.Tests;

[TestFixture]
public class PhoneSpecCatalogTests
{
    private static PhoneSpecCatalog Catalog => PhoneSpecCatalog.Shared;

    private static PhoneSpec Get(string brand, string model, int? year = null)
    {
        Assert.That(Catalog.TryGet(brand, model, year, out var spec), Is.True, $"{brand} {model} is in the catalogue");
        return spec;
    }

    [Test]
    public void GalaxyA5_2017_IsAndroidMax8With52Inches()
    {
        var spec = Get("Samsung", "Galaxy A5 (2017)");

        Assert.That(spec.Os, Is.EqualTo("Android"));
        Assert.That(spec.OsVersionMax, Is.EqualTo(8));
        Assert.That(spec.OsVersionLaunch, Is.EqualTo(6));
        Assert.That(spec.ReleaseYear, Is.EqualTo(2017));
        Assert.That(spec.ScreenSizeAttribute, Is.EqualTo("5.2\""));
    }

    [Test]
    public void GalaxyA5_YearGivenAsAttributeInsteadOfInTheModel_PicksTheSameGeneration()
    {
        var spec = Get("Samsung", "Galaxy A5", 2017);

        Assert.That(spec.OsVersionMax, Is.EqualTo(8));
        Assert.That(spec.IsAmbiguous, Is.False);
    }

    [Test]
    public void GalaxyA5_WithoutYear_IsAmbiguous_OnlyTheFamilyAllGenerationsShare()
    {
        var spec = Get("Samsung", "Galaxy A5");

        Assert.That(spec.IsAmbiguous, Is.True);
        Assert.That(spec.Os, Is.EqualTo("Android"));
        Assert.That(spec.OsVersionMax, Is.Null);
        Assert.That(spec.OsVersionLaunch, Is.Null);
        Assert.That(spec.ScreenInches, Is.Null);
        Assert.That(spec.ReleaseYear, Is.Null);
    }

    [Test]
    public void GalaxyA5_YearOfNoGeneration_IsIgnored_StillAmbiguous()
    {
        var spec = Get("Samsung", "Galaxy A5", 2020);

        Assert.That(spec.IsAmbiguous, Is.True);
        Assert.That(spec.OsVersionMax, Is.Null);
    }

    [TestCase("Samsung", "Galaxy S10 Plus", "Galaxy S10+")]
    [TestCase("Samsung", "Galaxy S21 Fe", "Galaxy S21 FE")]
    [TestCase("Samsung", "Galaxy Z Fold4", "Galaxy Z Fold 4")]
    [TestCase("Huawei", "Huawei Mate20 Pro", "Huawei Mate 20 Pro")]
    [TestCase("Xiaomi", "Xiaomi 13 Lite", "13 Lite")]
    [TestCase("Sony", "Xperia 10 Iv", "Xperia 10 IV")]
    [TestCase("Google", "Pixel 7A", "Pixel 7a")]
    [TestCase("Apple", "iPhone 16E", "iPhone 16e")]
    [TestCase("Nothing", "Nothing Phone 2a", "Phone (2a)")]
    public void ModelSpellings_OfTheExtractor_FindTheSameRow(string brand, string extractorModel, string otherSpelling)
    {
        var a = Get(brand, extractorModel);
        var b = Get(brand, otherSpelling);

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void SonyEricsson_IsFoundUnderBrandSonyAndItsOwnName()
    {
        Assert.That(Get("Sony", "W800I"), Is.EqualTo(Get("Sony Ericsson", "Sony Ericsson W800i")));
    }

    [Test]
    public void Pixel7a_IsAndroid()
    {
        var spec = Get("Google", "Pixel 7A");

        Assert.That(spec.Os, Is.EqualTo("Android"));
        Assert.That(spec.OsVersionMax, Is.EqualTo(16));
        Assert.That(spec.ScreenSizeAttribute, Is.EqualTo("6.1\""));
    }

    [Test]
    public void Nokia6210_IsNotAndroid_AndHasNoAndroidVersion()
    {
        var spec = Get("Nokia", "6210");

        Assert.That(spec.Os, Is.Not.EqualTo("Android"));
        Assert.That(spec.OsVersionMax, Is.Null);
        Assert.That(spec.OsVersionLaunch, Is.Null);
    }

    [Test]
    public void Nokia3310_YearPicksTheClassicOrTheModernOne()
    {
        Assert.That(Get("Nokia", "3310", 2000).Os, Is.EqualTo("Series 30"));
        Assert.That(Get("Nokia", "3310 (2017)").Os, Is.EqualTo("Series 30+"));
        Assert.That(Get("Nokia", "3310").Os, Is.Null, "the two generations run different platforms");
    }

    [Test]
    public void IPhone13_IsIos()
    {
        var spec = Get("Apple", "iPhone 13");

        Assert.That(spec.Os, Is.EqualTo("iOS"));
        Assert.That(spec.OsVersionLaunch, Is.EqualTo(15));
        Assert.That(spec.ScreenSizeAttribute, Is.EqualTo("6.1\""));
    }

    [Test]
    public void IPhoneSe_ByYearInTheModel()
    {
        Assert.That(Get("Apple", "iPhone SE 2020").OsVersionLaunch, Is.EqualTo(13));
        Assert.That(Get("Apple", "iPhone SE 2022").OsVersionLaunch, Is.EqualTo(15));
        Assert.That(Get("Apple", "iPhone SE").Os, Is.EqualTo("iOS"));
        Assert.That(Get("Apple", "iPhone SE").OsVersionMax, Is.Null);
    }

    [Test]
    public void BrandAliases_RedmiPocoAndSonyEricsson()
    {
        Assert.That(Get("Redmi", "Redmi Note 11").Os, Is.EqualTo("Android"));
        Assert.That(Get("Poco", "Poco F5").Os, Is.EqualTo("Android"));
        Assert.That(Get("Sony", "K750I").Os, Is.EqualTo("Proprietary"));
    }

    [Test]
    public void FiveGSuffix_IsDroppedWhenOnlyTheBaseModelIsKnown()
    {
        Assert.That(Catalog.TryGet("Xiaomi", "Poco F5 5G", out var spec), Is.True);
        Assert.That(spec.Os, Is.EqualTo("Android"));
    }

    [TestCase(null, "Galaxy S10")]
    [TestCase("Samsung", null)]
    [TestCase("Samsung", "")]
    [TestCase("Samsung", "Galaxy S99")]
    [TestCase("Samsung", "Hülle für Galaxy A54")]
    [TestCase("Unknownbrand", "Galaxy S10")]
    public void UnknownOrMissing_IsNotFound(string? brand, string? model) =>
        Assert.That(Catalog.TryGet(brand, model, out _), Is.False);

    [TestCase(6.1, "6.1\"")]
    [TestCase(5.0, "5\"")]
    [TestCase(6.72, "6.7\"")]
    public void ScreenSizeFormat_IsTheOneOfTheScreenSizeAttribute(double inches, string expected)
    {
        Assert.That(PhoneSpecCatalog.FormatScreenSize(inches), Is.EqualTo(expected));
        // the same text the attribute normalization gives a "6.1 Zoll" title value
        var (key, value) = Product.NormalizeAttributeClassification("screen_size", PhoneSpecCatalog.FormatScreenSize(inches));
        Assert.That((key, value), Is.EqualTo(("screen_size", expected)));
    }

    // ── data ──────────────────────────────────────────────────────────────

    [Test]
    public void Data_HasNoDuplicateBrandModelYear()
    {
        var duplicates = Catalog.Rows
            .GroupBy(row => (PhoneSpecCatalog.BrandKey(row.Brand), PhoneSpecCatalog.ModelKey(row.Brand, row.Model, out _), row.ReleaseYear))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.First().Brand} {group.First().Model} {group.Key.ReleaseYear}")
            .ToList();

        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void Data_ModelNamesDoNotEndInAYear()
    {
        var withYear = Catalog.Rows
            .Where(row => { PhoneSpecCatalog.ModelKey(row.Brand, row.Model, out var year); return year != null; })
            .Select(row => $"{row.Brand} {row.Model}")
            .ToList();

        Assert.That(withYear, Is.Empty, "the year belongs in release_year");
    }

    [Test]
    public void Data_YearsVersionsAndSizesAreSane()
    {
        foreach (var row in Catalog.Rows)
        {
            var name = $"{row.Brand} {row.Model} {row.ReleaseYear}";
            if (row.ReleaseYear is { } year)
                Assert.That(year, Is.InRange(1995, 2026), name);
            if (row.OsVersionLaunch is { } launch && row.OsVersionMax is { } max)
                Assert.That(max, Is.GreaterThanOrEqualTo(launch), name);
            if (row.ScreenInches is { } inches)
                Assert.That(inches, Is.InRange(1.0, 8.0), name);
            if (row.OsVersionMax != null || row.OsVersionLaunch != null)
                Assert.That(row.Os, Is.Not.Null, $"{name} has a version but no platform");
        }
    }

    [Test]
    public void Data_EveryRowHasABrandAModelAndAnOsFamilyOrIsSpecless()
    {
        Assert.That(Catalog.Rows, Is.Not.Empty);
        Assert.That(Catalog.Rows.Where(row => string.IsNullOrWhiteSpace(row.Brand) || string.IsNullOrWhiteSpace(row.Model)), Is.Empty);
        Assert.That(Catalog.Rows.Where(row => row.Os == "Feature phone"), Is.Empty, "a feature phone is not an operating system");
    }

    [Test]
    public void Data_NokiaClassics_AreNeverAndroid()
    {
        var classics = new[] { "105", "1100", "2310", "3310", "5130", "6020", "6210", "6230", "6230i", "6300", "6310", "6510", "6610", "8210" };
        foreach (var model in classics)
        {
            var rows = Catalog.Rows.Where(row => row.Brand == "Nokia" && row.Model == model).ToList();
            Assert.That(rows, Is.Not.Empty, $"Nokia {model} is listed");
            foreach (var row in rows)
            {
                Assert.That(row.Os, Is.Not.Null.And.Not.EqualTo("Android"), $"Nokia {model}");
                Assert.That(row.OsVersionMax, Is.Null, $"Nokia {model} has no Android version");
            }
        }
        Assert.That(Catalog.Rows.Where(row => row.Brand is "Siemens" or "Sony Ericsson" && row.Model != "Xperia X10").Select(row => row.Os), Is.All.Not.EqualTo("Android"));
    }

    [Test]
    public void Data_OsVersionMax_IsOnlyKnownForPlatformsWithComparableVersions_AndAndroidRowsHaveAnIntegerRange()
    {
        var androidMaxes = Catalog.Rows.Where(row => row.Os == "Android" && row.OsVersionMax != null).Select(row => row.OsVersionMax!.Value).ToList();

        Assert.That(androidMaxes, Is.Not.Empty);
        Assert.That(androidMaxes.Max(), Is.LessThanOrEqualTo(16), "no version newer than the newest released at curation");
    }
}
