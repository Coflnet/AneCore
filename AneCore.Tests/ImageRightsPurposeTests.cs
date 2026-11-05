using Coflnet.Ane;
using Coflnet.Ane.ImageRights;
using Microsoft.Extensions.Logging.Abstractions;

namespace AneCore.Tests;

[TestFixture]
public class ImageRightsPurposeTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private const string Host = "img.example.test";

    private static ImageRightsRecord Row(params string[] evidence) => new()
    {
        Host = Host, Status = "Revoked", Evidence = evidence.ToList(), CheckedAt = Now.AddYears(-1)
    };

    private static ImageRightsStatus Resolve(ImageRightsRecord? row, ImageRightsPurpose purpose) =>
        ImageRightsPolicy.Resolve(Host, row, Now, ImageRightsOptions.Parse(null, null), purpose).Status;

    [TestCase("User-agent: GPTBot\nDisallow: /", true, true)]
    [TestCase("User-agent: CCBot\nDisallow: /", true, true)]
    [TestCase("User-agent: ane.deals\nDisallow: /", false, false)]
    [TestCase("User-agent: *\nDisallow: /", false, false)]
    [TestCase("User-agent: *\nDisallow: /\nUser-agent: ane.deals\nAllow: /", true, true)]
    [TestCase("User-agent: *\nAllow: /\nUser-agent: ane.deals\nDisallow: /", false, false)]
    [TestCase("User-agent: ane.deals\nDisallow: /\nAllow: /", true, true)]
    [TestCase("User-agent: ane\nDisallow: /\nUser-agent: ane.deals\nAllow: /", true, true)]
    [TestCase("User-agent: GPTBot\nContent-Signal: ai-input=no\nDisallow: /\nUser-agent: *\nAllow: /", true, true)]
    [TestCase("Content-Signal: ai-train=no, ai-input=yes, search=yes", false, true)]
    [TestCase("Content-Signal: ai-train = no, ai-input = yes", false, true)]
    [TestCase("Content-Signal: ai-input = no, ai-train = yes", true, false)]
    [TestCase("Content-Signal: ai-input=no, ai-train=yes, search=yes", true, false)]
    [TestCase("Content-Signal: ai-train=no, ai-input=no", false, false)]
    [TestCase("Content-Signal: ai-train=no, use=immediate", false, false)]
    [TestCase("Content-Signal: ai-train=no, use=reference", false, true)]
    [TestCase("Content-Usage: train-ai=n", false, true)]
    public void ApplicableAgentAndPurposeDecideEligibility(string robots, bool training, bool comparison)
    {
        var result = ImageRightsEvaluator.Evaluate(new ImageRightsInput(Host, Host, FetchedFile.Ok(robots), FetchedFile.NotFound));
        var row = new ImageRightsRecord { Host = Host, Status = result.Status.ToString(), Evidence = result.Evidence.ToList(), CheckedAt = Now };
        Assert.Multiple(() =>
        {
            Assert.That(Resolve(row, ImageRightsPurpose.Training), Is.EqualTo(training ? ImageRightsStatus.Allowed : ImageRightsStatus.Revoked));
            Assert.That(Resolve(row, ImageRightsPurpose.Comparison), Is.EqualTo(comparison ? ImageRightsStatus.Allowed : ImageRightsStatus.Revoked));
        });
    }

    [TestCase("/private/photo.jpg", false)]
    [TestCase("/private/public/photo.jpg", true)]
    [TestCase("/elsewhere/photo.jpg", true)]
    public void ApplicablePathUsesLongestRuleAndAllowWinsTies(string path, bool allowed)
    {
        var result = ImageRightsEvaluator.Evaluate(new ImageRightsInput(Host, Host,
            FetchedFile.Ok("User-agent: ane.deals\nDisallow: /private/\nAllow: /private/public/"), FetchedFile.NotFound, ImagePath: path));
        Assert.That(result.Status, Is.EqualTo(allowed ? ImageRightsStatus.Allowed : ImageRightsStatus.Revoked));
    }

    [TestCase("img.kleinanzeigen.de")]
    [TestCase("i.ebayimg.com")]
    [TestCase("unmapped.example.test")]
    public void ValidHostWithoutRecordedOptOutDefaultsEligible(string host) =>
        Assert.That(ImageRightsPolicy.Resolve(host, null, Now, ImageRightsOptions.Parse(null, null)).Status, Is.EqualTo(ImageRightsStatus.Allowed));

    [Test]
    public void MissingEvidenceAndAgeNeverEraseARecordedRevocation()
    {
        Assert.That(Resolve(Row(), ImageRightsPurpose.Comparison), Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(Resolve(Row(), ImageRightsPurpose.Training), Is.EqualTo(ImageRightsStatus.Revoked));
        var missing = Row();
        missing.Evidence = null!;
        Assert.That(Resolve(missing, ImageRightsPurpose.Comparison), Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(ImageRightsPolicy.Resolve(null, Row(), Now, ImageRightsOptions.Parse(null, null)).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void LegacyOtherBotOnlyEvidenceIsInapplicableWithoutRewritingTheRow()
    {
        var row = Row("robots.txt of img.example.test: AI crawler group (GPTBot, CCBot) has Disallow: / - \"Disallow: /\"");
        Assert.That(Resolve(row, ImageRightsPurpose.Comparison), Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(Resolve(row, ImageRightsPurpose.Training), Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(row.Status, Is.EqualTo("Revoked"));
        row.Evidence.Add("image response header X-Robots-Tag: noai");
        Assert.That(Resolve(row, ImageRightsPurpose.Comparison), Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(Resolve(row, ImageRightsPurpose.Training), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase("robots.txt of img.example.test: Content-Signal ai-train=no - \"Content-Signal: ai-train=no, ai-input=no\"")]
    [TestCase("robots.txt of img.example.test: Content-Signal ai-train=no - \"Content-Signal: ai-train=no, use=immediate\"")]
    [TestCase("tdmrep.json of img.example.test: tdm-reservation 1 for location \"/\"")]
    [TestCase("image response header X-Robots-Tag: noimageai")]
    public void MixedBroadManualAndAmbiguousLegacyEvidenceStaysBlocked(string evidence) =>
        Assert.That(Resolve(Row(evidence), ImageRightsPurpose.Comparison), Is.EqualTo(ImageRightsStatus.Revoked));

    [Test]
    public void CurrentConfigurationWinsAndRemovedMachineOnlyDenialsAreNotOwnerOptOuts()
    {
        var row = Row("denied by configuration: platform Willhaben is in IMAGE_RIGHTS:DENY_PLATFORMS");
        row.Host = "cache.willhaben.at";
        var current = ImageRightsOptions.Parse("Willhaben", null);
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, Now, current).Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, Now, ImageRightsOptions.Parse("", null)).Status, Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(row.Status, Is.EqualTo("Revoked"), "resolution does not rewrite stored data");
        row.Evidence.Add("image response header X-Robots-Tag: noai");
        Assert.That(ImageRightsPolicy.Resolve(row.Host, row, Now, ImageRightsOptions.Parse("", null)).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public async Task RemovedConfigurationOnlyRowCanRefreshWithoutCarryingItsOldRevocationAge()
    {
        var store = new InMemoryImageRightsStore();
        var row = Row("denied by configuration: platform Willhaben is in IMAGE_RIGHTS:DENY_PLATFORMS");
        row.Host = "cache.willhaben.at";
        await store.UpsertAsync(row);
        var refresher = new ImageRightsRefresher(store, ImageRightsOptions.Parse("", null), NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(new PolicyHandler(403)));
        await refresher.RefreshAllAsync(CancellationToken.None);
        var current = await store.GetAsync(row.Host);
        Assert.That(current!.Status, Is.EqualTo("Unknown"), "an unreadable new policy is not a new explicit opt-out");
        Assert.That(ImageRightsPolicy.Resolve(row.Host, current, Now, ImageRightsOptions.Parse("", null)).Status, Is.EqualTo(ImageRightsStatus.Allowed));
    }

    [TestCase(404)]
    [TestCase(401)]
    [TestCase(403)]
    [TestCase(503)]
    public async Task RefreshNeverClearsAnExistingRevocationIncludingPartialTdmFailure(int tdmStatus)
    {
        var store = new InMemoryImageRightsStore();
        var previous = Row("image response header X-Robots-Tag: noimageai");
        previous.Host = "img.kleinanzeigen.de";
        await store.UpsertAsync(previous);
        var refresher = new ImageRightsRefresher(store, ImageRightsOptions.Parse(null, null), NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(new PolicyHandler(tdmStatus)));
        await refresher.RefreshAllAsync(CancellationToken.None);
        var current = await store.GetAsync(previous.Host);
        Assert.That(current!.Status, Is.EqualTo("Revoked"));
        Assert.That(current.Evidence, Is.EquivalentTo(previous.Evidence));
        Assert.That(current.CheckedAt, Is.EqualTo(previous.CheckedAt), "a failed/absent fetch is not a newly verified withdrawal");
    }

    private sealed class PolicyHandler(int tdmStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)(request.RequestUri!.AbsolutePath == "/robots.txt" ? 200 : tdmStatus))
            { Content = new StringContent("") });
    }
}
