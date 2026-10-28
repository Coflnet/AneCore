using Coflnet.Ane;
using Coflnet.Ane.KnownProducts;
using Coflnet.Ane.ShopReferences;
using NUnit.Framework;

namespace AneCore.Tests;

public class MediaStoreReferencePolicyTests
{
    [TestCase("identifier", false)]
    [TestCase("prefix", false)]
    [TestCase("kind", false)]
    [TestCase("ordinary", true)]
    public void SurvivingMediaIdentityCannotAcquireAToolReference(string marker, bool eligible)
    {
        // Synthetic partially lost metadata: a native media marker must outrank misleading tool fields.
        var tool = KnownProductSeed.LoadAll(true).Single(p => p.Id == "knipex-0301160");
        var product = new Product
        {
            SeoId = marker == "prefix" ? "media-offer-synthetic-used" : "knipex-0301160-used",
            Name = tool.Name, Brand = tool.Brand, Model = tool.Model,
            IdentifierType = marker == "identifier" ? "media_offer" : "catalogued_goods",
            IdentifierValue = tool.Id,
            Attributes = new() { ["product_kind"] = marker == "kind" ? "media_offer" : "hand_tool" }
        };
        var now = new DateTimeOffset(2026, 10, 28, 20, 0, 0, TimeSpan.Zero);
        var observed = now.AddMinutes(-1);
        var reference = new ProductStoreReference(tool.Id, "Lefeld",
            "https://www.lefeld.de/knipex-kombizange-03-01-160", "SYNTHETIC", tool.Brand, tool.Model,
            null, 10.07m, "EUR", "InStock", "unknown", observed, observed.AddMinutes(15));
        Assert.That(ProductStoreReferencePolicy.IsCandidate(product), Is.EqualTo(eligible));
        Assert.That(ProductStoreReferencePolicy.IsEligible(reference, product, [tool], now), Is.EqualTo(eligible));
    }
}
