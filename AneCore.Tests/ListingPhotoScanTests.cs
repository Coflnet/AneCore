using Coflnet.Ane;
using Coflnet.Ane.Embeddings;
using MessagePack;

namespace AneCore.Tests;

[TestFixture]
public class ListingPhotoScanTests
{
    [Test]
    public void Statement_ReadsOnePageWithoutEmbeddingBlob()
    {
        var statement = (Cassandra.SimpleStatement)ListingPhotoScanStatements.Build(200, new byte[] { 1, 2 });

        Assert.That(statement.AutoPage, Is.False);
        Assert.That(statement.PageSize, Is.EqualTo(200));
        Assert.That(statement.PagingState, Is.EqualTo(new byte[] { 1, 2 }));
        Assert.That(statement.QueryString, Does.Not.Contain(" embedding,").And.Not.Contain(", embedding"));
        Assert.That(statement.QueryString, Does.Contain("platform"));
    }

    [Test]
    public async Task InMemoryScan_ReturnsFirstPhotosOnlyAndWalksToTheEnd()
    {
        var store = new InMemoryListingEmbeddingStore();
        await store.UpsertEmbeddingsAsync(Enumerable.Range(0, 5).SelectMany(i => new[] { 0, 1 }.Select(idx =>
            new ListingImageEmbedding { ListingId = $"l{i}", Platform = Platform.Kleinanzeigen, ImageIndex = idx, ImageUrl = $"u{i}_{idx}", CreatedAt = DateTime.UtcNow })));

        var seen = new List<string>();
        byte[]? cursor = null;
        var pages = 0;
        do
        {
            var page = await store.ScanFirstPhotosAsync(2, cursor);
            seen.AddRange(page.Photos.Select(p => p.ImageUrl));
            cursor = page.NextCursor;
            pages++;
        } while (cursor != null && pages < 10);

        Assert.That(seen, Is.EquivalentTo(new[] { "u0_0", "u1_0", "u2_0", "u3_0", "u4_0" }));
        Assert.That(pages, Is.EqualTo(3));
    }

    [Test]
    public void RecheckResult_RoundTripsWithIntPlatformAndStatus()
    {
        var original = new RecheckResult { ListingId = "1", Platform = Platform.Vinted, Status = RecheckStatus.Unknown, UnknownReason = "paused", CheckedAt = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc) };

        var back = MessagePackSerializer.Deserialize<RecheckResult>(MessagePackSerializer.Serialize(original));

        Assert.That(back.Platform, Is.EqualTo(Platform.Vinted));
        Assert.That(back.Status, Is.EqualTo(RecheckStatus.Unknown));
        Assert.That(back.UnknownReason, Is.EqualTo("paused"));
        Assert.That(back.CheckedAt, Is.EqualTo(original.CheckedAt));
    }
}
