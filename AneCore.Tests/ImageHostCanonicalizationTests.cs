using Coflnet.Ane;
using Coflnet.Ane.ImageRights;

namespace AneCore.Tests;

[TestFixture]
public class ImageHostCanonicalizationTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [TestCase("https://images.WILLHABEN.AT./a.jpg", "images.willhaben.at")]
    [TestCase("//images1.VINTED.NET./a.jpg", "images1.vinted.net")]
    [TestCase("https://images.vinted\u3002net/a.jpg", "images.vinted.net")]
    [TestCase("https://images.vinted\uff0enet/a.jpg", "images.vinted.net")]
    [TestCase("https://images.willhaben\uff61at/a.jpg", "images.willhaben.at")]
    [TestCase("https://café.example./a.jpg", "xn--caf-dma.example")]
    [TestCase("https://XN--CAF-DMA.example/a.jpg", "xn--caf-dma.example")]
    public void UrlAuthorityUsesCanonicalDnsIdentity(string url, string expected) =>
        Assert.That(ImageHostMapping.HostOf(url), Is.EqualTo(expected));

    [TestCase("https://[invalid/a.jpg")]
    [TestCase("https://")]
    [TestCase("https://\uD800.example/a.jpg")]
    [TestCase("not a url")]
    public void InvalidAuthorityDoesNotThrowOrGrantAHost(string url) =>
        Assert.That(ImageHostMapping.HostOf(url), Is.Null);

    [TestCase("images.WILLHABEN.AT.", Platform.Willhaben)]
    [TestCase("IMAGES1.VINTED.NET.", Platform.Vinted)]
    public void RootDotDoesNotBypassKnownOwnerReservation(string host, Platform platform)
    {
        Assert.That(ImageHostMapping.TryGet(host)?.Platform, Is.EqualTo(platform));
        foreach (var purpose in new[] { ImageRightsPurpose.Training, ImageRightsPurpose.Comparison })
        {
            Assert.That(ImageRightsPolicy.ResolveImage("https://" + host + "/a.jpg", null, Now,
                ImageRightsOptions.Parse(null, null), purpose).Status, Is.EqualTo(ImageRightsStatus.Revoked));
            Assert.That(ImageRightsPolicy.Resolve(host, null, Now,
                ImageRightsOptions.Parse(null, null), purpose).Status, Is.EqualTo(ImageRightsStatus.Revoked));
        }
    }

    [TestCase("PHOTOS.EXAMPLE.", "photos.example")]
    [TestCase("photos.example", "PHOTOS.EXAMPLE.")]
    [TestCase("café.example.", "xn--caf-dma.example")]
    [TestCase("xn--caf-dma.example", "café.example.")]
    public void ManualHostDenyUsesSameCanonicalAuthority(string configured, string actual)
    {
        var options = ImageRightsOptions.Parse("", configured);
        Assert.That(ImageRightsPolicy.ResolveImage("https://" + actual + "/a.jpg", null, Now, options,
            ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(ImageRightsPolicy.DenialEvidence(actual, options), Is.Not.Null);
        Assert.That(ImageRightsPolicy.ResolveImage("https://photos.example.evil/a.jpg", null, Now, options,
            ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Allowed));
    }

    [TestCase("images.willhaben.at.evil.")]
    [TestCase("notvinted.net.")]
    [TestCase("img.kleinanzeigen.de.")]
    public void UnrelatedAndNonOptedOutAuthoritiesRemainEligible(string host) =>
        Assert.That(ImageRightsPolicy.ResolveImage("https://" + host + "/a.jpg", null, Now,
            ImageRightsOptions.Parse(null, null), ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Allowed));
}
