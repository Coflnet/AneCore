using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class DjiManufacturerCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-10-28-r38-dji";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);
    private static readonly string[] DroneModels = (
        "Mavic 4 Pro|Mavic 3 Pro|Mavic 3 Classic|Mavic 3|Mavic 2 Pro|Mavic 2 Zoom|Mavic Pro|Mavic Pro Platinum|" +
        "Air 3S|Air 3|Air 2S|Mavic Air 2|Mavic Air|Mini 5 Pro|Mini 4 Pro|Mini 4K|Mini 3 Pro|Mini 3|Mini 2 SE|Mini 2|Mini SE|Mavic Mini|" +
        "Spark|Flip|Neo 2|Neo|Avata 2|Avata|FPV|Inspire 3|Inspire 2|Inspire 1|" +
        "Phantom 4 Pro V2.0|Phantom 4 Pro|Phantom 4 Advanced|Phantom 4|Phantom 3 Standard|Phantom 3 SE|Phantom 3 Advanced|Phantom 3 Professional|Phantom 3 4K").Split('|');
    private static readonly string[] CameraModels = (
        "Osmo Action 6|Osmo Action 5 Pro|Osmo Action 4|Osmo Action 3|Action 2|Osmo Action|Osmo 360|Osmo Nano|Osmo Pocket 4|Osmo Pocket 3|Pocket 2|Osmo Pocket").Split('|');

    public static IEnumerable<string> ManufacturerModels => DroneModels.Concat(CameraModels);

    [TestCaseSource(nameof(ManufacturerModels))]
    public void VerifiedModelHasOneIdentityAndReachesExistingResaleAdmission(string model)
    {
        var row = Catalog.Single(p => p.Source == Source && p.Model == model);
        Assert.That(Catalog.Count(p => p.Id == row.Id || p.Brand == "DJI" && p.Model == model), Is.EqualTo(1));
        Assert.That(row.Aliases, Is.EquivalentTo(new[] { "DJI " + model }));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "device_type", "package_scope" }));
        Assert.That(row.PossibleAttributes["package_scope"], Is.EquivalentTo(new[] { "unspecified" }));
        var drone = DroneModels.Contains(model);
        Assert.That(row.PossibleAttributes["device_type"], Is.EquivalentTo(new[] { drone ? "drone" : "camera" }));
        Assert.That(row.Categories, Is.EqualTo(drone
            ? new[] { "Kameras & Optik" }
            : new[] { "Kameras & Optik", "Kameras", "Videokameras" }));
        Assert.That(Matcher.Match(row.Name)?.Id, Is.EqualTo(row.Id));
        Assert.That(ResaleCatalog.Match(row.Name)?.Id, Is.EqualTo(row.Id));
        Assert.That(ResaleCatalog.Match(row.Name + " gebraucht")?.Id, Is.EqualTo(row.Id));
    }

    [Test]
    public void CatalogueAddsManufacturerModelsWithoutPackageOrAccessoryRows()
    {
        var additions = Catalog.Where(p => p.Source == Source).ToList();
        Assert.That(additions.Select(p => p.Model), Is.EquivalentTo(ManufacturerModels));
        Assert.That(additions, Has.Count.EqualTo(53));
        var existingAliases = Catalog.Where(p => p.Source != Source).SelectMany(p => p.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(additions.SelectMany(p => p.Aliases).Select(KnownProductMatcher.Normalize)
            .Where(existingAliases.Contains), Is.Empty);
    }

    [TestCase("DJI Mavic 4 Pro Fly More Combo med DJI RC 2 - ny och oöppnad")]
    [TestCase("DJI Osmo Pocket 4 Creator Combo")]
    [TestCase("DJI Osmo Pocket 4 Creator Combo + filtres ND – Excellent état")]
    [TestCase("Creator Combo DJI Osmo Pocket 4")]
    [TestCase("DJI Osmo Pocket 4 107GB Creator Combo")]
    [TestCase("DJI Action 2 Dual Screen Combo")]
    [TestCase("DJI Mini 4 Pro Intelligent Flight Battery")]
    [TestCase("Battery for DJI Mini 4 Pro")]
    [TestCase("DJI Mini 4 Pro Remote Controller")]
    [TestCase("Remote Controller for DJI Mini 4 Pro")]
    [TestCase("DJI Mini 4 Pro Propellers")]
    [TestCase("Propellers DJI Mini 4 Pro")]
    [TestCase("Gimbal DJI Mavic 4 Pro")]
    [TestCase("DJI Mavic 3 Cine")]
    [TestCase("DJI Mavic 3 Enterprise")]
    [TestCase("DJI Mini 3 Bedienungsanleitung")]
    public void PackagesVariantsAndAccessoryReferencesCannotClaimTheDevice(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("DJI Mavic 4 Pro gesucht")]
    [TestCase("DJI Air 3 und DJI Mini 4 Pro")]
    [TestCase("DJI Osmo Pocket 4 + DJI Osmo Pocket 3")]
    [TestCase("DJI Mini 4 Pro unvollständig")]
    public void NonSaleSeveralDevicesAndIncompleteOffersHaveNoSingleResaleIdentity(string title) =>
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);

    [TestCase("DJI Mavic 4 Pro drönare HELT NY", "dji-mavic-4-pro")]
    [TestCase("DJI Osmo Pocket 4 mit Tasche", "dji-osmo-pocket-4")]
    [TestCase("DJI Mini 4 Pro mit Akku", "dji-mini-4-pro")]
    [TestCase("DJI Mini 4 Pro mit Fernsteuerung", "dji-mini-4-pro")]
    public void ExplicitDeviceAndIncludedExtrasPreserveUnknownPackageScope(string title, string id)
    {
        var row = ResaleCatalog.Match(title);
        Assert.That(row?.Id, Is.EqualTo(id));
        Assert.That(row!.PossibleAttributes["package_scope"], Is.EquivalentTo(new[] { "unspecified" }));
    }
}
