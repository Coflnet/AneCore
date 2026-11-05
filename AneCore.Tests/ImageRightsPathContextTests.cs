using System.Net;
using System.Text.Json;
using Coflnet.Ane.ImageRights;
using Microsoft.Extensions.Logging.Abstractions;

namespace AneCore.Tests;

[TestFixture]
public class ImageRightsPathContextTests
{
    private const string Host = "img.example.test";
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ImageRightsOptions Options = ImageRightsOptions.Parse(null, null);
    private static ImageRightsPolicySnapshot Snapshot(string robots, string? tdm = null) => new(
        Host, Host, FetchedFile.Ok(robots), FetchedFile.NotFound,
        tdm == null ? FetchedFile.NotFound : FetchedFile.Ok(tdm), FetchedFile.NotFound, ImageRightsEvaluator.UserAgent);
    private static string Context(params ImageRightsPolicySnapshot[] snapshots) => JsonSerializer.Serialize(new ImageRightsContext(1, snapshots.ToList(), snapshots.Select(s => s.RootCrawlerCause()).OfType<string>().Distinct().ToList()));
    private static ImageRightsRecord Row(ImageRightsPolicySnapshot snapshot)
    {
        var root = snapshot.Evaluate("/");
        return new() { Host = Host, Status = root.Status.ToString(), Evidence = root.Evidence.ToList(), CheckedAt = Now, PolicyContext = Context(snapshot) };
    }
    private static ImageRightsStatus At(ImageRightsRecord row, string path, ImageRightsPurpose purpose = ImageRightsPurpose.Comparison) =>
        ImageRightsPolicy.ResolveImage("https://" + Host + path, row, Now, Options, purpose).Status;

    [TestCase("/private/a.jpg", ImageRightsStatus.Revoked)]
    [TestCase("/public/a.jpg", ImageRightsStatus.Allowed)]
    public void SubtreeRulesApplyWhenHostSummaryAllows(string path, ImageRightsStatus expected)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /private/"));
        Assert.That(row.StatusValue, Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(At(row, path), Is.EqualTo(expected));
    }

    [TestCase("/public/a.jpg", ImageRightsStatus.Allowed)]
    [TestCase("/private/a.jpg", ImageRightsStatus.Revoked)]
    public void KnownGeneratedRootDenialCanNarrowToAllowedSubtree(string path, ImageRightsStatus expected)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        Assert.That(ImageRightsPolicy.Resolve(Host, row, Now, Options).Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(At(row, path), Is.EqualTo(expected));
        Assert.That(row.StatusValue, Is.EqualTo(ImageRightsStatus.Revoked), "URL resolution never rewrites host summary");
    }

    [TestCase("/a%20b.jpg?download=1", ImageRightsStatus.Revoked)]
    [TestCase("/a%20b.jpg?download=0", ImageRightsStatus.Allowed)]
    public void UsesEscapedPathAndQuery(string path, ImageRightsStatus expected) =>
        Assert.That(At(Row(Snapshot("User-agent: *\nDisallow: /a%20b.jpg?download=1$")), path), Is.EqualTo(expected));

    [TestCase("https://img.example.test/private/a.jpg?download=1")]
    [TestCase("//img.example.test/private/a.jpg?download=1")]
    public void ProtocolRelativeUrlHasSameEscapedPathAndQuery(string url)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /private/a.jpg?download=1$"));
        Assert.That(ImageRightsPolicy.ResolveImage(url, row, Now, Options, ImageRightsPurpose.Comparison).Status,
            Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase("/restricted/a.jpg", ImageRightsStatus.Revoked)]
    [TestCase("/public/a.jpg", ImageRightsStatus.Allowed)]
    public void TdmLocationIsResolvedPerImage(string path, ImageRightsStatus expected) =>
        Assert.That(At(Row(Snapshot("", "[{\"location\":\"/restricted/\",\"tdm-reservation\":1}]")), path), Is.EqualTo(expected));

    [TestCase(ImageRightsPurpose.Comparison, ImageRightsStatus.Allowed)]
    [TestCase(ImageRightsPurpose.Training, ImageRightsStatus.Revoked)]
    public void PurposeSeparationSurvivesContextReevaluation(ImageRightsPurpose purpose, ImageRightsStatus expected) =>
        Assert.That(At(Row(Snapshot("Content-Signal: ai-train=no")), "/public/a.jpg", purpose), Is.EqualTo(expected));

    [TestCase("owner opted out")]
    [TestCase("image response header X-Robots-Tag: noimageai")]
    [TestCase("robots.txt of other.example.test: crawler ane.deals crawler/1.0 is disallowed for /")]
    public void NonReproducedCausesAreNeverWaived(string evidence)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        row.Evidence.Add(evidence);
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void EmptyCauseDenialAndCurrentConfigurationRemainBlocked()
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        row.Evidence.Clear();
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
        row = Row(Snapshot(""));
        Assert.That(ImageRightsPolicy.ResolveImage("https://" + Host + "/public/a.jpg", row, Now,
            ImageRightsOptions.Parse(null, Host), ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase(null)]
    [TestCase("{")]
    [TestCase("{\"Version\":2,\"Snapshots\":[]}")]
    public void MissingOrUnrecognizedContextDoesNotRelaxLegacyDenial(string? context)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        row.PolicyContext = context;
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void ContextForAnotherHostCannotRelaxDenial()
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        row.PolicyContext = Context(Snapshot("") with { ImageHost = "other.example.test" });
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void FailedSnapshotRetainsKnownSubtreeReservationAndRepeatedSnapshotsDeduplicate()
    {
        var previous = Snapshot("User-agent: *\nDisallow: /private/");
        var failed = previous with { ImageRobots = FetchedFile.Failed };
        var row = Row(previous);
        row.PolicyContext = ImageRightsContext.Merge(Host, Context(previous), Context(failed));
        row.PolicyContext = ImageRightsContext.Merge(Host, row.PolicyContext, Context(failed));
        Assert.That(ImageRightsContext.Read(row.PolicyContext, Host)!.Snapshots, Has.Count.EqualTo(2));
        Assert.That(At(row, "/private/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.Throws<InvalidOperationException>(() => ImageRightsContext.Merge(Host, "malformed", Context(failed)));
    }

    [TestCase(200)]
    [TestCase(403)]
    public async Task RefreshUpdatesContextEvenWithExistingRevocationAndKeepsManualCause(int status)
    {
        var entry = ImageHostMapping.KnownHosts.Single(e => e.ImageHost == "img.kleinanzeigen.de");
        var store = new InMemoryImageRightsStore();
        var old = Snapshot("User-agent: *\nDisallow: /private/") with { ImageHost = entry.ImageHost, SiteHost = entry.SiteHost };
        await store.UpsertAsync(new() { Host = entry.ImageHost, Status = "Revoked", Evidence = ["owner opted out"], PolicyContext = Context(old), CheckedAt = Now });
        var refresher = new ImageRightsRefresher(store, Options, NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(new PolicyHandler(status)));
        await refresher.RefreshAllAsync(CancellationToken.None);
        var row = (await store.GetAsync(entry.ImageHost))!;
        Assert.That(ImageRightsContext.Read(row.PolicyContext, entry.ImageHost)!.Snapshots, Has.Count.EqualTo(2));
        Assert.That(row.Evidence, Does.Contain("owner opted out"));
        Assert.That(ImageRightsPolicy.ResolveImage("https://" + entry.ImageHost + "/public/a.jpg", row, Now, Options,
            ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase("/public/a.jpg")]
    [TestCase("/private/a.jpg")]
    public void TrainingNeverNarrowsExistingRootDenial(string path)
    {
        var row = Row(Snapshot("User-agent: *\nDisallow: /\nAllow: /public/"));
        Assert.That(At(row, path, ImageRightsPurpose.Training), Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Allowed));
    }

    [Test]
    public async Task NewObservationBindsCrawlerOriginForComparisonOnly()
    {
        var entry = ImageHostMapping.KnownHosts.Single(e => e.ImageHost == "img.kleinanzeigen.de");
        var store = new InMemoryImageRightsStore();
        var refresher = new ImageRightsRefresher(store, Options, NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(new ChangedPolicyHandler(entry.ImageHost)));
        await refresher.RefreshAllAsync(CancellationToken.None);
        var row = (await store.GetAsync(entry.ImageHost))!;
        Assert.That(ImageRightsContext.Read(row.PolicyContext, entry.ImageHost)!.ProvenRootCauses, Has.Count.EqualTo(1));
        var url = "https://" + entry.ImageHost + "/public/a.jpg";
        Assert.That(ImageRightsPolicy.ResolveImage(url, row, Now, Options, ImageRightsPurpose.Comparison).Status,
            Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That(ImageRightsPolicy.ResolveImage(url, row, Now, Options, ImageRightsPurpose.Training).Status,
            Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NewPolicyWithSameGenericCauseCannotAdoptLegacyOrigin(bool hasUnboundContext)
    {
        var entry = ImageHostMapping.KnownHosts.Single(e => e.ImageHost == "img.kleinanzeigen.de");
        var old = new ImageRightsPolicySnapshot(entry.ImageHost, entry.SiteHost, FetchedFile.Ok("User-agent: *\nDisallow: /"),
            FetchedFile.NotFound, FetchedFile.NotFound, FetchedFile.NotFound, ImageRightsEvaluator.UserAgent);
        var cause = old.RootCrawlerCause()!;
        var store = new InMemoryImageRightsStore();
        await store.UpsertAsync(new() { Host = entry.ImageHost, Status = "Revoked", Evidence = [cause],
            PolicyContext = hasUnboundContext ? JsonSerializer.Serialize(new ImageRightsContext(1, [old])) : null,
            CheckedAt = Now, RobotsHash = ImageRightsRefresher.Hash(old.ImageRobots.Text, "") });
        var refresher = new ImageRightsRefresher(store, Options, NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(new ChangedPolicyHandler(entry.ImageHost)));
        await refresher.RefreshAllAsync(CancellationToken.None);
        await refresher.RefreshAllAsync(CancellationToken.None);
        var row = (await store.GetAsync(entry.ImageHost))!;
        Assert.That(ImageRightsContext.Read(row.PolicyContext, entry.ImageHost)!.ProvenRootCauses, Does.Not.Contain(cause));
        Assert.That(ImageRightsPolicy.ResolveImage("https://" + entry.ImageHost + "/public/a.jpg", row, Now, Options,
            ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void RetainedOriginalSnapshotStillPreventsLaterNewExceptionFromOpeningOldReservation()
    {
        var old = Snapshot("User-agent: *\nDisallow: /");
        var changed = Snapshot("User-agent: *\nDisallow: /\nAllow: /public/");
        var row = Row(old);
        row.PolicyContext = ImageRightsContext.Merge(Host, row.PolicyContext, Context(changed), row.Evidence);
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [TestCase("Content-Signal: ai-train=no")]
    [TestCase("Content-Usage: train-ai=n")]
    public void TrainingInvariantMetadataCannotBecomeCrawlerOriginProof(string robots)
    {
        var row = Row(Snapshot(robots));
        Assert.That(ImageRightsContext.Read(row.PolicyContext, Host)!.ProvenRootCauses, Is.Empty);
        Assert.That(At(row, "/public/a.jpg", ImageRightsPurpose.Training), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void SiteOnlyCrawlerDenialIsNeverImagePathOriginProof()
    {
        var policy = Snapshot("") with { SiteHost = "www.site.example.test",
            SiteRobots = FetchedFile.Ok("User-agent: *\nDisallow: /") };
        var row = Row(policy);
        Assert.That(ImageRightsContext.Read(row.PolicyContext, Host)!.ProvenRootCauses, Is.Empty);
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    [Test]
    public void GlobalTdmCannotBecomeCrawlerOriginProof()
    {
        var row = Row(Snapshot("", "[{\"location\":\"/\",\"tdm-reservation\":1}]"));
        Assert.That(ImageRightsContext.Read(row.PolicyContext, Host)!.ProvenRootCauses, Is.Empty);
        Assert.That(At(row, "/public/a.jpg"), Is.EqualTo(ImageRightsStatus.Revoked));
    }

    private sealed class ChangedPolicyHandler(string imageHost) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var robots = request.RequestUri!.AbsolutePath == "/robots.txt";
            return Task.FromResult(new HttpResponseMessage(robots ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            { Content = new StringContent(robots && request.RequestUri.Host == imageHost
                ? "User-agent: *\nDisallow: /\nAllow: /public/" : "") });
        }
    }

    private sealed class PolicyHandler(int status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("") });
    }
}
