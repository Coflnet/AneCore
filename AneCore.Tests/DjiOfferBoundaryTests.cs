using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class DjiOfferBoundaryTests
{
    private static readonly KnownProductMatcher Matcher = new(KnownProductSeed.LoadAll(true));

    [TestCase("Battery DJI Mini 4 Pro")]
    [TestCase("DJI replacement battery Mini 4 Pro")]
    [TestCase("DJI Mini 4 Pro spare battery")]
    [TestCase("DJI Mini 4 Pro flight battery")]
    [TestCase("Propellers for DJI Mavic 4 Pro")]
    [TestCase("Gimbal replacement DJI Osmo Pocket 4")]
    [TestCase("DJI Osmo Pocket 4 replacement gimbal")]
    [TestCase("DJI Mini 4 Pro drone battery")]
    [TestCase("Fly More Combo DJI Mavic 4 Pro")]
    [TestCase("DJI Mavic 4 Pro drone Fly More Combo")]
    public void InterposedAndPrefixedPartsOrPackagesDoNotClaimADevice(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("DJI Mini 4 Pro with battery", "dji-mini-4-pro")]
    [TestCase("DJI Mini 4 Pro with spare propellers", "dji-mini-4-pro")]
    [TestCase("DJI Mini 4 Pro with remote controller", "dji-mini-4-pro")]
    [TestCase("DJI Osmo Pocket 4 with box", "dji-osmo-pocket-4")]
    public void DeviceWithIncludedExtrasStillNamesTheDevice(string title, string id) =>
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));

    [Test]
    public void PackageBoundaryRemainsScopedToDji()
    {
        var existing = Matcher.Match("Sony A7 III");
        Assert.That(existing, Is.Not.Null);
        Assert.That(Matcher.Match("Sony A7 III Creator Combo")?.Id, Is.EqualTo(existing!.Id));
    }

    [TestCase("CPL ND filter kit voor DJI Osmo Action 5 Pro (4 stuks)")]
    [TestCase("ND filters DJI Osmo Action 5 Pro")]
    [TestCase("DJI Osmo Action 5 Pro ND filter kit")]
    [TestCase("DJI Osmo Action 5 Pro camera ND filters")]
    public void FilterOffersDoNotClaimTheCamera(string title)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Matcher.Match(title), Is.Null, title);
            Assert.That(ResaleCatalog.Match(title), Is.Null, title);
        });
    }

    [TestCase("DJI Osmo Action 5 Pro")]
    [TestCase("DJI Osmo Action 5 Pro met ND filter")]
    [TestCase("DJI Osmo Action 5 Pro with filters")]
    public void CameraWithIncludedFiltersStillNamesTheCamera(string title)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo("dji-osmo-action-5-pro"));
            Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo("dji-osmo-action-5-pro"));
        });
    }
}
