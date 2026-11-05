using Coflnet.Ane.ImageRights;

namespace AneCore.Tests;

[TestFixture]
public class ImageRightsCanonicalStoreTests
{
    [TestCase("PHOTOS.EXAMPLE.", "photos.example")]
    [TestCase("photos.example", "PHOTOS.EXAMPLE.")]
    [TestCase("café.example.", "xn--caf-dma.example")]
    [TestCase("xn--caf-dma.example", "café.example.")]
    public async Task SupportedWriteAndReadShareCanonicalPartitionIdentity(string written, string read)
    {
        IImageRightsStore store = new InMemoryImageRightsStore();
        var record = new ImageRightsRecord { Host = written, Status = "Revoked", Evidence = ["owner opted out"] };
        await store.UpsertAsync(record);
        var found = await store.GetAsync(read);
        Assert.That(found, Is.SameAs(record));
        Assert.That(found!.StatusValue, Is.EqualTo(ImageRightsStatus.Revoked));
        Assert.That(found.Host, Is.EqualTo(ImageHostMapping.HostOf("https://" + read)));
        Assert.That(await store.GetAllAsync(), Has.Count.EqualTo(1));
        Assert.That(await store.GetAsync("photos.example.evil"), Is.Null);
    }

    [Test]
    public async Task CanonicalWriteDoesNotRewriteOrGrantLegacyContextProof()
    {
        var store = new InMemoryImageRightsStore();
        const string context = "{\"unrecognizedLegacyContext\":true}";
        await store.UpsertAsync(new() { Host = "PHOTOS.EXAMPLE.", Status = "Revoked", Evidence = ["owner opted out"], PolicyContext = context });
        var record = (await store.GetAsync("photos.example"))!;
        Assert.That(record.PolicyContext, Is.EqualTo(context));
        Assert.That(ImageRightsPolicy.ResolveImage("https://photos.example/a.jpg", record, DateTime.UtcNow,
            ImageRightsOptions.Parse("", null), ImageRightsPurpose.Comparison).Status, Is.EqualTo(ImageRightsStatus.Revoked));
    }
}
