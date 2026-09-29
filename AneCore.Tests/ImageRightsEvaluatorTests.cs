using Coflnet.Ane.ImageRights;

namespace AneCore.Tests;

[TestFixture]
public class ImageRightsEvaluatorTests
{
    private static string Robots(string host) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "robots", host + ".txt"));

    private static FetchedFile Fixture(string host) => FetchedFile.Ok(Robots(host));

    private static ImageRightsResult Eval(string imageHost, string siteHost, FetchedFile? image = null, FetchedFile? site = null,
        string? imageTdm = null, string? siteTdm = null, string path = "/", Dictionary<string, string>? headers = null) =>
        ImageRightsEvaluator.Evaluate(new ImageRightsInput(imageHost, siteHost,
            image ?? FetchedFile.Ok(""), site ?? FetchedFile.Ok(""), imageTdm, siteTdm, path, headers));

    [Test]
    public void Vinted_RealFiles_AreRevoked_WithQuotedEvidence()
    {
        var result = Eval("images1.vinted.net", "www.vinted.de", Fixture("images1.vinted.net"), Fixture("www.vinted.de"));

        Assert.That(result.Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(result.Evidence, Has.Some.Contains("Content-Signal: ai-train=no, search=yes, ai-input=no"));
        Assert.That(result.Evidence, Has.Some.Contains("Content-Usage: train-ai=n"));
        Assert.That(result.Evidence, Has.Some.Contains("AI crawler group").And.Contains("GPTBot"));
    }

    [Test]
    public void Ebay_AiCrawlerGroupWithDisallowRoot_IsRevokedEvenWithFollowingAllowLines()
    {
        var result = Eval("i.ebayimg.com", "www.ebay.de", FetchedFile.Ok(""), Fixture("www.ebay.de"));

        Assert.That(result.Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(result.Evidence, Has.Some.Contains("Applebot-Extended"));
    }

    [Test]
    public void Kleinanzeigen_GptBotAllowRootWithSpecificDisallows_IsAllowed()
    {
        // GPTBot has "Allow: /" plus specific disallows, zoomRank has "Disallow: /" but is no AI training crawler.
        var result = Eval("img.kleinanzeigen.de", "www.kleinanzeigen.de", Fixture("img.kleinanzeigen.de"), Fixture("www.kleinanzeigen.de"));

        Assert.That(result.Status, Is.EqualTo(ImageRightsStatus.Allowed));
    }

    [Test]
    public void EmptyAndMissingRobots_CountAsNoSignal()
    {
        Assert.That(Eval("a", "b", FetchedFile.Ok(""), FetchedFile.NotFound).Status, Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(Eval("a", "b", FetchedFile.FromHttp(404, "Not Found"), FetchedFile.FromHttp(403, "no")).Status,
            Is.EqualTo(ImageRightsStatus.Allowed));
    }

    [TestCase(500)]
    [TestCase(503)]
    [TestCase(429)]
    public void FetchFailure_IsUnknown(int status)
    {
        var result = Eval("a", "b", FetchedFile.FromHttp(status, null), Fixture("www.kleinanzeigen.de"));

        Assert.That(result.Status, Is.EqualTo(ImageRightsStatus.Unknown));
        Assert.That(result.Evidence, Has.Some.Contains("could not be fetched"));
    }

    [Test]
    public void NetworkError_IsUnknown() =>
        Assert.That(Eval("a", "b", FetchedFile.Ok(""), FetchedFile.Failed).Status, Is.EqualTo(ImageRightsStatus.Unknown));

    [Test]
    public void Revocation_WinsOverFetchFailureOfTheOtherHost() =>
        Assert.That(Eval("a", "b", FetchedFile.Ok("Content-Usage: train-ai=n"), FetchedFile.Failed).Status,
            Is.EqualTo(ImageRightsStatus.Revoked));

    [TestCase("User-agent: GPTBot\nDisallow: /", true)]
    [TestCase("User-agent: gptbot\nDisallow: /", true)]
    [TestCase("User-agent: GPTBot\nAllow: /\nDisallow: /account", false)]
    [TestCase("User-agent: GPTBot\nAllow: /\nDisallow: /", false)]
    [TestCase("User-agent: GPTBot\nDisallow: /private", false)]
    [TestCase("User-agent: *\nDisallow: /", false)]
    [TestCase("User-agent: SomeBot\nDisallow: /", false)]
    [TestCase("User-agent: SomeBot\nUser-agent: CCBot\nDisallow: /", true)]
    [TestCase("User-agent: CCBot\nAllow: /\n\nUser-agent: ClaudeBot\nDisallow: /", true)]
    [TestCase("Content-Signal: ai-train=yes, search=yes", false)]
    [TestCase("Content-Signal: search=yes, ai-train=no", true)]
    [TestCase("Content-Usage: train-ai=y", false)]
    [TestCase("# Content-Signal: ai-train=no", false)]
    public void RobotsRules(string robots, bool revoked) =>
        Assert.That(Eval("a", "b", FetchedFile.Ok(robots)).Status,
            Is.EqualTo(revoked ? ImageRightsStatus.Revoked : ImageRightsStatus.Allowed));

    [TestCase("""[{"location":"/","tdm-reservation":1}]""", "/img/1.jpg", true)]
    [TestCase("""[{"location":"/images/*","tdm-reservation":1}]""", "/images/a.jpg", true)]
    [TestCase("""[{"location":"/images/*","tdm-reservation":1}]""", "/other/a.jpg", false)]
    [TestCase("""[{"location":"/","tdm-reservation":0}]""", "/a.jpg", false)]
    [TestCase("""{"location":"/","tdm-reservation":"1"}""", "/a.jpg", true)]
    [TestCase("not json", "/a.jpg", false)]
    public void Tdmrep_OnImageHost(string json, string path, bool revoked) =>
        Assert.That(Eval("a", "b", imageTdm: json, path: path).Status,
            Is.EqualTo(revoked ? ImageRightsStatus.Revoked : ImageRightsStatus.Allowed));

    [Test]
    public void Tdmrep_OnSiteHost_ReservingRoot_IsRevoked() =>
        Assert.That(Eval("a", "b", siteTdm: """[{"location":"/","tdm-reservation":1}]""").Status, Is.EqualTo(ImageRightsStatus.Revoked));

    [TestCase("tdm-reservation", "1", true)]
    [TestCase("TDM-Reservation", "0", false)]
    [TestCase("X-Robots-Tag", "noai, noimageai", true)]
    [TestCase("x-robots-tag", "NoImageAI", true)]
    [TestCase("X-Robots-Tag", "noindex", false)]
    [TestCase("Content-Type", "image/jpeg", false)]
    public void ResponseHeaders(string name, string value, bool revoked) =>
        Assert.That(Eval("a", "b", headers: new() { [name] = value }).Status,
            Is.EqualTo(revoked ? ImageRightsStatus.Revoked : ImageRightsStatus.Allowed));

    [Test]
    public void FetchedFile_FromHttp_Mapping()
    {
        Assert.That(FetchedFile.FromHttp(200, "x").State, Is.EqualTo(FetchState.Ok));
        Assert.That(FetchedFile.FromHttp(404, null).State, Is.EqualTo(FetchState.NotFound));
        Assert.That(FetchedFile.FromHttp(403, null).State, Is.EqualTo(FetchState.NotFound));
        Assert.That(FetchedFile.FromHttp(502, null).State, Is.EqualTo(FetchState.Failed));
    }
}
