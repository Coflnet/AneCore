using Coflnet.Ane;
using Coflnet.Ane.ImageRights;
using Microsoft.Extensions.Logging.Abstractions;

namespace AneCore.Tests;

[TestFixture]
public class ImageRightsPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static ImageRightsRecord Row(string host, ImageRightsStatus status, TimeSpan age) => new()
    {
        Host = host, Platform = "Vinted", Status = status.ToString(), Evidence = ["e"], CheckedAt = Now - age
    };

    [TestCase(47, ImageRightsStatus.Allowed, ImageRightsStatus.Allowed)]
    [TestCase(49, ImageRightsStatus.Allowed, ImageRightsStatus.Unknown)]
    [TestCase(49, ImageRightsStatus.Revoked, ImageRightsStatus.Unknown)]
    [TestCase(1, ImageRightsStatus.Revoked, ImageRightsStatus.Revoked)]
    public void EffectiveStatus_StaleAfter48Hours(int hours, ImageRightsStatus stored, ImageRightsStatus expected) =>
        Assert.That(ImageRightsPolicy.EffectiveStatus(stored, Now.AddHours(-hours), Now), Is.EqualTo(expected));

    [Test]
    public void Resolve_StaleRow_IsUnknownWithReason()
    {
        var decision = ImageRightsPolicy.Resolve("images1.vinted.net", Row("images1.vinted.net", ImageRightsStatus.Allowed, TimeSpan.FromHours(50)),
            Now, ImageRightsOptions.Parse("", null));

        Assert.That(decision.Status, Is.EqualTo(ImageRightsStatus.Unknown));
        Assert.That(decision.Evidence[0], Does.Contain("older than 48 hours"));
    }

    [Test]
    public void Resolve_FreshAllowedRow_IsAllowed() =>
        Assert.That(ImageRightsPolicy.Resolve("i.ebayimg.com", Row("i.ebayimg.com", ImageRightsStatus.Allowed, TimeSpan.FromHours(2)),
            Now, ImageRightsOptions.Parse("", null)).Status, Is.EqualTo(ImageRightsStatus.Allowed));

    [Test]
    public void Resolve_MissingRowOrUnmappedHost_IsUnknown()
    {
        var options = ImageRightsOptions.Parse("", null);
        Assert.That(ImageRightsPolicy.Resolve("i.ebayimg.com", null, Now, options).Status, Is.EqualTo(ImageRightsStatus.Unknown));
        Assert.That(ImageRightsPolicy.Resolve("cdn.example.org", Row("cdn.example.org", ImageRightsStatus.Allowed, TimeSpan.Zero), Now, options).Status,
            Is.EqualTo(ImageRightsStatus.Unknown));
        Assert.That(ImageRightsPolicy.Resolve(null, null, Now, options).Status, Is.EqualTo(ImageRightsStatus.Unknown));
    }

    [Test]
    public void Options_DefaultDeniesWillhaben_EmptyListDeniesNothing()
    {
        Assert.That(ImageRightsOptions.Parse(null, null).DenyPlatforms, Is.EquivalentTo(new[] { Platform.Willhaben }));
        Assert.That(ImageRightsOptions.Parse("", null).DenyPlatforms, Is.Empty);
        Assert.That(ImageRightsOptions.Parse("vinted, Ebay,nonsense", null).DenyPlatforms,
            Is.EquivalentTo(new[] { Platform.Vinted, Platform.Ebay }));
    }

    [Test]
    public void Override_DeniesPlatformEvenWhenStoredAllowed()
    {
        var decision = ImageRightsPolicy.Resolve("cache.willhaben.at", Row("cache.willhaben.at", ImageRightsStatus.Allowed, TimeSpan.Zero),
            Now, ImageRightsOptions.Parse(null, null));

        Assert.That(decision.Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(decision.Evidence[0], Does.StartWith("denied by configuration"));
    }

    [Test]
    public void Override_DeniesHost_CaseInsensitive()
    {
        var options = ImageRightsOptions.Parse("", "I.EbayImg.com, other.example");
        var decision = ImageRightsPolicy.Resolve("i.ebayimg.com", Row("i.ebayimg.com", ImageRightsStatus.Allowed, TimeSpan.Zero), Now, options);

        Assert.That(decision.Status, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(decision.Evidence[0], Does.Contain("denied by configuration"));
    }

    [TestCase("images1.vinted.net", "www.vinted.de", Platform.Vinted)]
    [TestCase("images2.vinted.net", "www.vinted.de", Platform.Vinted)]
    [TestCase("img.kleinanzeigen.de", "www.kleinanzeigen.de", Platform.Kleinanzeigen)]
    [TestCase("i.ebayimg.com", "www.ebay.de", Platform.Ebay)]
    [TestCase("a.marktplaats.nl", "www.marktplaats.nl", Platform.Marktplaats)]
    [TestCase("cache.willhaben.at", "www.willhaben.at", Platform.Willhaben)]
    [TestCase("I.EBAYIMG.COM", "www.ebay.de", Platform.Ebay)]
    public void HostMapping(string host, string site, Platform platform)
    {
        var entry = ImageHostMapping.TryGet(host);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.SiteHost, Is.EqualTo(site));
        Assert.That(entry.Platform, Is.EqualTo(platform));
    }

    [Test]
    public void HostMapping_UnknownHost_IsNull()
    {
        Assert.That(ImageHostMapping.TryGet("cdn.example.org"), Is.Null);
        Assert.That(ImageHostMapping.TryGet(""), Is.Null);
        Assert.That(ImageHostMapping.HostOf("https://images1.vinted.net/t/01/x.jpg"), Is.EqualTo("images1.vinted.net"));
        Assert.That(ImageHostMapping.HostOf("//images.marktplaats.nl/img.jpg"), Is.EqualTo("images.marktplaats.nl"));
        Assert.That(ImageHostMapping.HostOf("not a url"), Is.Null);
    }

    [Test]
    public async Task Refresher_WritesRevokedForVinted_AllowedForKleinanzeigen_AndDenialWithoutFetch()
    {
        var handler = new FakeHandler(url => url switch
        {
            "https://images1.vinted.net/robots.txt" => (200, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "robots", "images1.vinted.net.txt"))),
            "https://www.vinted.de/robots.txt" => (200, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "robots", "www.vinted.de.txt"))),
            "https://www.kleinanzeigen.de/robots.txt" => (200, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "robots", "www.kleinanzeigen.de.txt"))),
            "https://www.ebay.de/robots.txt" => (503, ""),
            _ => (404, "")
        });
        var store = new InMemoryImageRightsStore();
        var refresher = new ImageRightsRefresher(store, ImageRightsOptions.Parse(null, null), NullLogger<ImageRightsRefresher>.Instance,
            new HttpClient(handler));

        await refresher.RefreshAllAsync(CancellationToken.None);

        Assert.That((await store.GetAsync("images1.vinted.net"))!.StatusValue, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That((await store.GetAsync("img.kleinanzeigen.de"))!.StatusValue, Is.EqualTo(ImageRightsStatus.Allowed));
        Assert.That((await store.GetAsync("i.ebayimg.com"))!.StatusValue, Is.EqualTo(ImageRightsStatus.Unknown));
        var willhaben = (await store.GetAsync("cache.willhaben.at"))!;
        Assert.That(willhaben.StatusValue, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(willhaben.Evidence[0], Does.StartWith("denied by configuration"));
        Assert.That(handler.Requested, Has.None.Contains("willhaben"));
        Assert.That(handler.UserAgents, Is.All.EqualTo("ane.deals crawler/1.0"));
        Assert.That((await store.GetAsync("images1.vinted.net"))!.RobotsHash, Has.Length.EqualTo(64));
    }

    [Test]
    public void StoreMapping_HostIsTheTextPartitionKey_AndDerivedStatusIsNotAColumn()
    {
        var definition = CassandraImageRightsStore.BuildMapping().Get<ImageRightsRecord>();
        Assert.That(definition.TableName, Is.EqualTo("image_rights_status"));
        Assert.That(definition.PartitionKeys, Is.EquivalentTo(new[] { "host" }));
        Assert.That(definition.GetColumnDefinition(typeof(ImageRightsRecord).GetProperty(nameof(ImageRightsRecord.StatusValue))!).Ignore, Is.True);
    }

    private sealed class FakeHandler(Func<string, (int Status, string Body)> respond) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];
        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            var (status, body) = respond(url);
            return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StringContent(body) });
        }
    }
}
