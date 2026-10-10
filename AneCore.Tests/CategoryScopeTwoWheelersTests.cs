using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class CategoryScopeTwoWheelersTests
{
    private static readonly CategoryScopeCatalog Scope = CategoryScopeCatalog.Shared;

    // Pinned on purpose: the notifier's Extraction/Data/two-wheelers.txt names the same brands. A change here
    // must be made there too (and the other way round), so editing the list means editing this test.
    private static readonly string[] PinnedBrands =
    {
        "Zündapp", "Zuendapp", "Zundapp", "Zundap", "Zuendap",
        "Kreidler",
        "Hercules", "Herkules",
        "Simson",
        "Puch",
        "NSU",
        "DKW",
        "MZ",
        "Vespa",
        "Solex", "Velosolex",
        "Tomos",
        "Sachs", "Fichtel Sachs", "Fichtel",
        "Maico",
        "Victoria",
        "Horex",
        "Garelli",
        "Rixe",
        "Ardie",
        "Peugeot 103",
        "Piaggio Ciao",
    };

    [Test]
    public void BrandListIsPinned()
    {
        var vertical = Scope.Get("classic_two_wheelers");

        Assert.That(vertical, Is.Not.Null);
        Assert.That(vertical!.IsActive, Is.True);
        Assert.That(vertical.Brands!.Words, Is.EqualTo(PinnedBrands));
    }

    [TestCase("305", null)]
    [TestCase("306", null)]
    [TestCase(null, "Motorräder & Motorroller")]
    [TestCase(null, "Motorradteile &amp; Zubehör")]
    public void KleinanzeigenMotorcycleCategoriesBelongToTheVertical(string? id, string? label)
    {
        var scope = Scope.ResolvePlatform(Platform.Kleinanzeigen, label, id);

        Assert.That(scope?.Vertical?.Key, Is.EqualTo("classic_two_wheelers"));
    }

    [TestCase("216")]   // Autos
    [TestCase("223")]   // Autoteile & Reifen
    [TestCase("276")]   // Nutzfahrzeuge & Anhänger
    public void OtherVehicleCategoriesStayOutOfScope(string id)
    {
        Assert.That(Scope.ResolvePlatform(Platform.Kleinanzeigen, null, id)!.IsOutOfScope, Is.True);
    }

    [Test]
    public void BicyclesStayInTheirVertical()
    {
        Assert.That(Scope.ResolvePlatform(Platform.Kleinanzeigen, "Fahrräder & Zubehör")!.Vertical!.Key, Is.EqualTo("bikes"));
    }

    [TestCase("Zündapp KS 50 Felge Hinterrad")]
    [TestCase("ZUNDAPP ks80")]
    [TestCase("Zuendapp Combinette")]
    [TestCase("Simson S51 Schwalbe")]
    [TestCase("Kreidler Florett RS")]
    [TestCase("Fichtel & Sachs Motor 504")]
    [TestCase("MZ ETZ 250")]
    [TestCase("Peugeot 103 SP Mofa")]
    [TestCase("Piaggio Ciao Moped")]
    [TestCase("Herkules Prima 5")]
    public void BrandTitlesMatch(string title)
    {
        Assert.That(Scope.Get("classic_two_wheelers")!.Brands!.Matches(title), Is.True);
    }

    [TestCase("Honda CBR 600")]
    [TestCase("Peugeot Speedfight 2")]
    [TestCase("Piaggio Zip 50")]
    [TestCase("Suzuki Mzansi")]          // whole tokens only
    [TestCase("Hercules2000")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherTitlesDoNotMatch(string? title)
    {
        Assert.That(Scope.Get("classic_two_wheelers")!.Brands!.Matches(title), Is.False);
    }

    [Test]
    public void OtherVerticalsHaveNoBrandGate()
    {
        Assert.That(Scope.Get("clothing")!.Brands, Is.Null);
    }
}
