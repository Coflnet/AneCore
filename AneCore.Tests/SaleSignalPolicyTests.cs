namespace Coflnet.Ane;

public sealed class SaleSignalPolicyTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Test]
    public void GenuineDisappearanceWindowIsInclusiveAndPreservedForAllRequestedMarketplaces(
        [Values(Platform.Kleinanzeigen, Platform.Marktplaats, Platform.Willhaben, Platform.OLX)] Platform platform,
        [Values(-1, 0, 1.999, 2, 12, 48, 48.001, 200)] double hours)
    {
        var expected = hours is >= 2 and <= 48;
        var created = Now.AddHours(-hours);
        Assert.Multiple(() =>
        {
            Assert.That(SaleSignalPolicy.AgeBasis(platform, created, null, Now), Is.EqualTo(expected ? "listing_created_at" : null));
            Assert.That(SaleSignalPolicy.AgeBasis(platform, null, created, Now), Is.EqualTo(expected ? "first_seen" : null));
        });
    }

    [Test]
    public void MissingDatesNeverInventAgeAndCreationAgeTakesPrecedenceOverFirstSeen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SaleSignalPolicy.AgeBasis(Platform.Marktplaats, null, null, Now), Is.Null);
            Assert.That(SaleSignalPolicy.AgeBasis(Platform.OLX, Now.AddDays(-10), Now.AddHours(-4), Now), Is.Null);
            Assert.That(SaleSignalPolicy.AgeBasis(Platform.Ebay, Now.AddHours(-4), null, Now), Is.Null);
        });
    }

    [TestCase(Platform.Kleinanzeigen, null, true)]
    [TestCase(Platform.Marktplaats, null, true)]
    [TestCase(Platform.Willhaben, null, true)]
    [TestCase(Platform.OLX, "PLN", false)]
    [TestCase(Platform.Ebay, "GBP", false)]
    [TestCase(Platform.Kleinanzeigen, "USD", false)]
    [TestCase(Platform.Ebay, "EUR", true)]
    public void LegacyPriceTableNeverMixesNonEuroPrices(Platform platform, string? currency, bool expected) =>
        Assert.That(SaleSignalPolicy.IsLegacyEuroCurrency(platform, currency), Is.EqualTo(expected));
}
