using Coflnet.Ane.ImageRights;

namespace AneCore.Tests;

public class ImageRightsHeaderPurposeTests
{
    [TestCase("tdm-reservation", "1", true, true)]
    [TestCase("tdm-reservation", "0", false, false)]
    [TestCase("tdm-reservation", "0, 1", true, true)]
    [TestCase("tdm-reservation", "0\n1", true, true)]
    [TestCase("tdm-reservation", "0 1", true, true)]
    [TestCase("tdm-reservation", "01, 0", false, false)]
    [TestCase("X-Robots-Tag", "noai", true, true)]
    [TestCase("X-Robots-Tag", "NoImageAI", true, true)]
    [TestCase("X-Robots-Tag", "notnoai", false, false)]
    [TestCase("X-Robots-Tag", "noai-training", false, false)]
    [TestCase("X-Robots-Tag", "GPTBot: noai", false, false)]
    [TestCase("X-Robots-Tag", "CCBot: noindex, noimageai", false, false)]
    [TestCase("X-Robots-Tag", "ane.deals: noai", true, true)]
    [TestCase("X-Robots-Tag", "ane.deals crawler/1.0: noimageai", true, true)]
    [TestCase("X-Robots-Tag", "*: noai", true, true)]
    [TestCase("X-Robots-Tag", "GPTBot: noindex, noai", false, false)]
    [TestCase("X-Robots-Tag", "GPTBot: noai, ane.deals: noindex", false, false)]
    [TestCase("X-Robots-Tag", "GPTBot: noindex, ane.deals: noai", true, true)]
    [TestCase("X-Robots-Tag", "GPTBot: noai\nnoimageai", true, true)]
    [TestCase("X-Robots-Tag", "GPTBot: noindex, noai\nCCBot: noimageai", false, false)]
    [TestCase("X-Robots-Tag", "ane.deals: noai\nGPTBot: noindex", true, true)]
    [TestCase("X-Robots-Tag", ": noai", true, true)]
    [TestCase("X-Robots-Tag", "uncertain bot scope: noai", true, true)]
    [TestCase("Content-Signal", "ai-train=no", true, false)]
    [TestCase("Content-Signal", "ai-input=no", false, true)]
    [TestCase("Content-Signal", "ai-train=no, ai-input=no", true, true)]
    [TestCase("Content-Signal", "ai-train=no, use=immediate", true, true)]
    public void ResponseHeaderEligibilityHonorsPurposeExactTokensAndPhysicalFields(string name, string value, bool training, bool comparison)
    {
        Assert.That(ImageRightsEvaluator.HeaderReserves(name, value, ImageRightsPurpose.Training), Is.EqualTo(training));
        Assert.That(ImageRightsEvaluator.HeaderReserves(name, value, ImageRightsPurpose.Comparison), Is.EqualTo(comparison));
        var result = ImageRightsEvaluator.Evaluate(new ImageRightsInput("img.example.test", "img.example.test",
            FetchedFile.NotFound, FetchedFile.NotFound, ImageHeaders: new Dictionary<string, string> { [name] = value }));
        var row = new ImageRightsRecord { Host = "img.example.test", Status = result.Status.ToString(), Evidence = result.Evidence.ToList() };
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, DateTime.UtcNow, ImageRightsOptions.Parse(null, null), ImageRightsPurpose.Training).Status,
            Is.EqualTo(training ? ImageRightsStatus.Revoked : ImageRightsStatus.Allowed));
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, DateTime.UtcNow, ImageRightsOptions.Parse(null, null), ImageRightsPurpose.Comparison).Status,
            Is.EqualTo(comparison ? ImageRightsStatus.Revoked : ImageRightsStatus.Allowed));
    }

    [TestCase("GPTBot: noai", true)]
    [TestCase("GPTBot: noindex, noai", false)]
    [TestCase("GPTBot: noai, CCBot: noimageai", true)]
    [TestCase("notnoai", true)]
    [TestCase("noai-training", true)]
    [TestCase("ane.deals: noai", false)]
    [TestCase("GPTBot: noai\nnoimageai", false)]
    [TestCase(": noai", false)]
    [TestCase("unknown legacy restriction", false)]
    public void LegacyKnownHeaderCauseCanOnlyBeNarrowedWithoutRewritingStoredRevocation(string value, bool allowed)
    {
        var row = new ImageRightsRecord
        {
            Host = "unmapped.example.test", Status = "Revoked", CheckedAt = DateTime.UtcNow.AddYears(-1),
            Evidence = ["image response header X-Robots-Tag: " + value]
        };
        foreach (var purpose in new[] { ImageRightsPurpose.Training, ImageRightsPurpose.Comparison })
            Assert.That(ImageRightsPolicy.Resolve(row.Host, row, DateTime.UtcNow, ImageRightsOptions.Parse(null, null), purpose).Status,
                Is.EqualTo(allowed ? ImageRightsStatus.Allowed : ImageRightsStatus.Revoked));
        Assert.That(row.Status, Is.EqualTo("Revoked"));
        row.Evidence.Add("image response header tdm-reservation: 1");
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, DateTime.UtcNow, ImageRightsOptions.Parse(null, null), ImageRightsPurpose.Comparison).Status,
            Is.EqualTo(ImageRightsStatus.Revoked));
    }
}
