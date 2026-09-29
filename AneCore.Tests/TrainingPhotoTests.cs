using System.Reflection;
using Coflnet.Ane;
using Coflnet.Ane.Embeddings;
using Coflnet.Ane.ImageRights;
using Coflnet.Ane.TrainingPhotos;
using Coflnet.Ane.VisualPairs;

namespace AneCore.Tests;

[TestFixture]
public class TrainingPhotoTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static TrainingPhoto Photo(string listing = "1", int index = 0, string host = "img.kleinanzeigen.de",
        Platform platform = Platform.Kleinanzeigen, DateTime? archivedAt = null) => new()
    {
        Platform = platform, ListingId = listing, ImageIndex = index, ImageUrl = "https://" + host + "/x.jpg", Host = host,
        Jpeg = [1, 2, 3], Width = 10, Height = 12, Sha256 = "ab", ModelId = "m", Source = TrainingPhotoSources.Pair,
        FirstSeenAt = Now.AddDays(-3), VerifiedOnlineAt = Now.AddDays(-1), ArchivedAt = archivedAt ?? Now.AddHours(-2),
        RightsStatus = "Allowed", RightsCheckedAt = Now
    };

    // ---- rules --------------------------------------------------------------------------------------

    [TestCase(23, false)]
    [TestCase(24, true)]
    [TestCase(25, true)]
    public void IsOldEnough_Default24Hours(int hours, bool expected) =>
        Assert.That(TrainingPhotoRules.IsOldEnough(Now.AddHours(-hours), Now), Is.EqualTo(expected));

    [Test]
    public void IsOldEnough_CustomMinAge() =>
        Assert.That(TrainingPhotoRules.IsOldEnough(Now.AddHours(-2), Now, TimeSpan.FromHours(1)), Is.True);

    [Test]
    public void IsExportable_VerifiedAtLeast24HoursAfterFirstSeen()
    {
        var p = Photo();
        p.FirstSeenAt = Now.AddHours(-30);
        p.VerifiedOnlineAt = Now.AddHours(-6);
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Allowed), Is.True);
        p.VerifiedOnlineAt = Now.AddHours(-7);   // 23 hours after first seen
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Allowed), Is.False);
        p.VerifiedOnlineAt = Now.AddHours(-30);  // verified before it even was 24 hours old
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Allowed), Is.False);
    }

    [Test]
    public void IsExportable_UsesTheRightsStatusOfNow()
    {
        var p = Photo();
        p.RightsStatus = "Allowed"; // at archive time
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Allowed), Is.True);
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Revoked), Is.False);
        Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Unknown), Is.False);
    }

    [Test]
    public void IsExportable_NeedsArchivedVerifiedAndFirstSeen()
    {
        var a = Photo(); a.ArchivedAt = null;
        var b = Photo(); b.VerifiedOnlineAt = null;
        var c = Photo(); c.FirstSeenAt = null;
        foreach (var p in new[] { a, b, c })
            Assert.That(TrainingPhotoRules.IsExportable(p, ImageRightsStatus.Allowed), Is.False);
    }

    // ---- in-memory store ----------------------------------------------------------------------------

    [Test]
    public async Task AddAsync_IsIdempotent_AndGetHonoursIncludeJpeg()
    {
        var store = new InMemoryTrainingPhotoStore();
        Assert.That(await store.AddAsync(Photo()), Is.True);
        var changed = Photo(); changed.Sha256 = "other";
        Assert.That(await store.AddAsync(changed), Is.False);

        Assert.That((await store.GetAsync(Platform.Kleinanzeigen, "1", 0))!.Sha256, Is.EqualTo("ab"));
        Assert.That((await store.GetAsync(Platform.Kleinanzeigen, "1", 0))!.Jpeg, Is.Null);
        Assert.That((await store.GetAsync(Platform.Kleinanzeigen, "1", 0, includeJpeg: true))!.Jpeg, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(await store.ExistsAsync(Platform.Kleinanzeigen, "1", 0), Is.True);
        Assert.That(await store.ExistsAsync(Platform.Kleinanzeigen, "1", 1), Is.False);
        Assert.That(await store.ExistsAsync(Platform.Vinted, "1", 0), Is.False);
    }

    [Test]
    public async Task GetForListing_ListsByImageIndex_AndDayIndexCounts()
    {
        var store = new InMemoryTrainingPhotoStore();
        await store.AddAsync(Photo("1", 2));
        await store.AddAsync(Photo("1", 0));
        await store.AddAsync(Photo("2", 0, "a.marktplaats.nl", Platform.Marktplaats, Now.AddDays(-1)));
        Assert.That((await store.GetForListingAsync(Platform.Kleinanzeigen, "1")).Select(p => p.ImageIndex), Is.EqualTo(new[] { 0, 2 }));

        var today = DateOnly.FromDateTime(Now);
        Assert.That(await store.CountDayAsync(today), Is.EqualTo(2));
        Assert.That(await store.CountDayAsync(today.AddDays(-1)), Is.EqualTo(1));
        Assert.That((await store.ListDayAsync(today, 1)), Has.Count.EqualTo(1));
        var summary = await store.GetDaySummaryAsync(today, Now.AddHours(-1));
        Assert.That(summary.Total, Is.EqualTo(2));
        Assert.That(summary.Since, Is.EqualTo(0));
        Assert.That(summary.ByHost["img.kleinanzeigen.de"], Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteListing_RemovesPhotosAndIsIdempotent()
    {
        var store = new InMemoryTrainingPhotoStore();
        await store.AddAsync(Photo("1", 0));
        await store.AddAsync(Photo("1", 1));
        await store.AddAsync(Photo("2", 0));
        Assert.That(await store.DeleteListingAsync(Platform.Kleinanzeigen, "1"), Is.EqualTo(2));
        Assert.That(await store.DeleteListingAsync(Platform.Kleinanzeigen, "1"), Is.EqualTo(0));
        Assert.That(await store.CountDayAsync(DateOnly.FromDateTime(Now)), Is.EqualTo(1));
        Assert.That(await store.ExistsAsync(Platform.Kleinanzeigen, "2", 0), Is.True);
    }

    [Test]
    public async Task DeleteHost_OnlyThatHostAndOnlyOlderPhotos()
    {
        var store = new InMemoryTrainingPhotoStore();
        await store.AddAsync(Photo("1", 0, "a.marktplaats.nl", Platform.Marktplaats, Now.AddDays(-2)));
        await store.AddAsync(Photo("2", 0, "a.marktplaats.nl", Platform.Marktplaats, Now.AddHours(-1)));
        await store.AddAsync(Photo("3", 0, "img.kleinanzeigen.de", Platform.Kleinanzeigen, Now.AddDays(-2)));
        Assert.That(await store.DeleteHostAsync("A.Marktplaats.nl", Now.AddDays(-1)), Is.EqualTo(1));
        Assert.That(await store.ExistsAsync(Platform.Marktplaats, "1", 0), Is.False);
        Assert.That(await store.ExistsAsync(Platform.Marktplaats, "2", 0), Is.True);
        Assert.That(await store.ExistsAsync(Platform.Kleinanzeigen, "3", 0), Is.True);
        Assert.That(await store.DeleteHostAsync("a.marktplaats.nl", Now.AddDays(1)), Is.EqualTo(1));
    }

    [Test]
    public void Embedding_RoundTripsThroughTheSharedCodec()
    {
        var v = new float[EmbeddingBlobCodec.Dimensions];
        v[3] = 1f;
        var p = Photo();
        p.SetEmbedding(v);
        Assert.That(p.Embedding, Has.Length.EqualTo(EmbeddingBlobCodec.ByteLength));
        Assert.That(p.EmbeddingVector, Is.EqualTo(v));
        Assert.That(Photo().EmbeddingVector, Is.Null);
    }

    // ---- mapping ------------------------------------------------------------------------------------

    [Test]
    public void Mapping_HasNoEnumOrByteColumns_AndTheRightKeys()
    {
        var mapping = CassandraTrainingPhotoStore.BuildMapping();
        foreach (var (type, definition) in new (Type, Cassandra.Mapping.ITypeDefinition)[]
                 { (typeof(TrainingPhoto), mapping.Get<TrainingPhoto>()), (typeof(TrainingPhotoDayEntry), mapping.Get<TrainingPhotoDayEntry>()) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var column = definition.GetColumnDefinition(property);
                if (column.Ignore) continue;
                var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.That(t.IsEnum, Is.False, $"{property.Name} must not be an enum");
                Assert.That(t, Is.Not.EqualTo(typeof(byte)), $"{property.Name} must not be a byte");
            }
        }
        var photo = mapping.Get<TrainingPhoto>();
        Assert.That(photo.PartitionKeys, Is.EqualTo(new[] { "platform", "listing_id" }));
        Assert.That(photo.ClusteringKeys.Select(k => k.Item1), Is.EqualTo(new[] { "image_index" }));
        var day = mapping.Get<TrainingPhotoDayEntry>();
        Assert.That(day.PartitionKeys, Is.EqualTo(new[] { "day", "shard" }));
        Assert.That(day.ClusteringKeys.Select(k => k.Item1), Is.EqualTo(new[] { "platform", "listing_id", "image_index" }));
    }

    [Test]
    public void Shard_IsStableAndInRange()
    {
        var s = TrainingPhotoKeys.ShardOf(3, "abc");
        Assert.That(s, Is.InRange(0, TrainingPhotoKeys.ShardCount - 1));
        Assert.That(TrainingPhotoKeys.ShardOf(3, "abc"), Is.EqualTo(s));
    }

    // ---- purge and status_since ---------------------------------------------------------------------

    private static ImageRightsRecord Row(string host, ImageRightsStatus status, double sinceDaysAgo, double checkedHoursAgo = 1, bool noSince = false) => new()
    {
        Host = host, Status = status.ToString(), CheckedAt = Now.AddHours(-checkedHoursAgo),
        StatusSince = noSince ? null : Now.AddDays(-sinceDaysAgo)
    };

    [Test]
    public void Purge_OnlyRevokedForLongerThanTheGracePeriod()
    {
        var rows = new[]
        {
            Row("old-revoked", ImageRightsStatus.Revoked, 8),
            Row("fresh-revoked", ImageRightsStatus.Revoked, 6),
            Row("exactly-seven", ImageRightsStatus.Revoked, 7),
            Row("unknown-long", ImageRightsStatus.Unknown, 30),
            Row("allowed", ImageRightsStatus.Allowed, 30),
            Row("no-since-recent-check", ImageRightsStatus.Revoked, 0, checkedHoursAgo: 2, noSince: true),
            Row("no-since-old-check", ImageRightsStatus.Revoked, 0, checkedHoursAgo: 24 * 9, noSince: true),
        };
        Assert.That(TrainingPhotoPurge.HostsToPurge(rows, Now, TimeSpan.FromDays(7)), Is.EqualTo(new[] { "no-since-old-check", "old-revoked" }));
    }

    [Test]
    public void StatusSince_IsKeptOnAnUnchangedStatus_AndResetOnChange()
    {
        var since = Now.AddDays(-5);
        var previous = new ImageRightsRecord { Host = "h", Status = "Revoked", CheckedAt = Now.AddDays(-1), StatusSince = since };
        Assert.That(ImageRightsRefresher.ResolveStatusSince(previous, "Revoked", Now), Is.EqualTo(since));
        Assert.That(ImageRightsRefresher.ResolveStatusSince(previous, "Allowed", Now), Is.EqualTo(Now));
        Assert.That(ImageRightsRefresher.ResolveStatusSince(null, "Allowed", Now), Is.EqualTo(Now));
        // a row from before the column existed: the last check is the best knowledge
        var legacy = new ImageRightsRecord { Host = "h", Status = "Revoked", CheckedAt = Now.AddDays(-1) };
        Assert.That(ImageRightsRefresher.ResolveStatusSince(legacy, "Revoked", Now), Is.EqualTo(Now.AddDays(-1)));
    }

    // ---- takedown in the pair store -----------------------------------------------------------------

    [Test]
    public async Task PairStore_DeleteForListing_RemovesCandidatesAndLabelsOfEitherSideAndIsIdempotent()
    {
        var store = new InMemoryVisualPairStore();
        VisualPairCandidate Cand(string a, string b) => new()
        {
            PairId = VisualPairId.Compute(Platform.Vinted, a, 0, Platform.Vinted, b, 0), CreatedAt = Now,
            A = new(Platform.Vinted, a, 0, "u"), B = new(Platform.Vinted, b, 0, "u"), Decision = "join"
        };
        var ab = Cand("A", "B"); var ca = Cand("C", "A"); var cd = Cand("C", "D");
        foreach (var c in new[] { ab, ca, cd }) await store.AddCandidateAsync(c);
        await store.AddLabelAsync(VisualPairLabel.From(ab, "u1", "different", Now));
        await store.AddLabelAsync(VisualPairLabel.From(ab, "u2", "different", Now));
        await store.AddLabelAsync(VisualPairLabel.From(cd, "u1", "same_item", Now));

        Assert.That(await store.DeleteForListingAsync(Platform.Vinted, "A"), Is.EqualTo((2, 2)));
        Assert.That(await store.DeleteForListingAsync(Platform.Vinted, "A"), Is.EqualTo((0, 0)));
        Assert.That(await store.GetCandidateAsync(cd.PairId), Is.Not.Null);
        Assert.That(await store.GetLabelsForPairAsync(cd.PairId), Has.Count.EqualTo(1));
        Assert.That(await store.DeleteForListingAsync(Platform.Ebay, "A"), Is.EqualTo((0, 0)));
    }
}
