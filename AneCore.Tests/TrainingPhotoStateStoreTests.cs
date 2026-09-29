using System.Reflection;
using Coflnet.Ane;
using Coflnet.Ane.TrainingPhotos;

namespace AneCore.Tests;

[TestFixture]
public class TrainingPhotoStateStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Mapping_HasNoEnumOrByteColumns_AndTheRightKeys()
    {
        var mapping = CassandraTrainingPhotoStateStore.BuildMapping();
        foreach (var (type, definition) in new (Type, Cassandra.Mapping.ITypeDefinition)[]
                 {
                     (typeof(TrainingRecheckRow), mapping.Get<TrainingRecheckRow>()),
                     (typeof(TrainingRunStateRow), mapping.Get<TrainingRunStateRow>()),
                     (typeof(TrainingGroupCountRow), mapping.Get<TrainingGroupCountRow>()),
                     (typeof(TrainingAwaitingRow), mapping.Get<TrainingAwaitingRow>())
                 })
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
        Assert.That(mapping.Get<TrainingRecheckRow>().PartitionKeys, Is.EqualTo(new[] { "platform", "listing_id" }));
        Assert.That(mapping.Get<TrainingAwaitingRow>().PartitionKeys, Is.EqualTo(new[] { "day" }));
        Assert.That(mapping.Get<TrainingAwaitingRow>().ClusteringKeys.Select(k => k.Item1), Is.EqualTo(new[] { "platform", "listing_id", "image_index" }));
        Assert.That(mapping.Get<TrainingGroupCountRow>().PartitionKeys, Is.EqualTo(new[] { "day" }));
        Assert.That(mapping.Get<TrainingGroupCountRow>().ClusteringKeys.Select(k => k.Item1), Is.EqualTo(new[] { "group_key" }));
    }

    [Test]
    public async Task Recheck_RoundTrips_AndExpiresAfterTheTtl()
    {
        var now = Now;
        var store = new InMemoryTrainingPhotoStateStore(() => now);
        await store.SaveRecheckAsync(new TrainingRecheckRow
        {
            PlatformValue = (int)Platform.Kleinanzeigen, ListingId = "1", StatusValue = (int)RecheckStatus.Available,
            CheckedAt = Now, Attempts = 2, RequestedAt = Now.AddHours(-1)
        }, TimeSpan.FromHours(12));

        var row = await store.GetRecheckAsync(Platform.Kleinanzeigen, "1");
        Assert.That(row!.Attempts, Is.EqualTo(2));
        Assert.That(row.CheckedAt, Is.EqualTo(Now));
        Assert.That(await store.GetRecheckAsync(Platform.Vinted, "1"), Is.Null);

        now = Now.AddHours(13);
        Assert.That(await store.GetRecheckAsync(Platform.Kleinanzeigen, "1"), Is.Null);
    }

    [Test]
    public async Task LastRun_AndGroupCounts_AreKeptPerDay()
    {
        var store = new InMemoryTrainingPhotoStateStore(() => Now);
        Assert.That(await store.GetLastRunAsync(), Is.Null);
        await store.SetLastRunAsync(Now);
        Assert.That(await store.GetLastRunAsync(), Is.EqualTo(Now));

        var day = DateOnly.FromDateTime(Now);
        await store.SetGroupCountAsync(day, "nike|hoodie", 3);
        await store.SetGroupCountAsync(day, "nike|hoodie", 4);
        await store.SetGroupCountAsync(day.AddDays(-1), "nike|hoodie", 9);
        var counts = await store.GetGroupCountsAsync(day);
        Assert.That(counts["nike|hoodie"], Is.EqualTo(4));
        Assert.That(counts, Has.Count.EqualTo(1));
    }

    private static TrainingAwaitingRow Awaiting(string id, int index, DateTime requestedAt) => new()
    {
        PlatformValue = (int)Platform.Kleinanzeigen, ListingId = id, ImageIndex = index, ImageUrl = $"https://x/{id}_{index}.jpg",
        GroupKey = "nike|hoodie|used", Source = "pair", CreatedAt = requestedAt.AddDays(-2), RequestedAt = requestedAt
    };

    [Test]
    public async Task Awaiting_RoundTrips_IsDeletedAndExpires()
    {
        var now = Now;
        var store = new InMemoryTrainingPhotoStateStore(() => now);
        await store.SaveAwaitingAsync(Awaiting("1", 0, Now.AddHours(-3)), TimeSpan.FromHours(24));
        await store.SaveAwaitingAsync(Awaiting("1", 1, Now.AddHours(-5)), TimeSpan.FromHours(24));
        await store.SaveAwaitingAsync(Awaiting("2", 0, Now.AddHours(-30)), TimeSpan.FromHours(24));   // older than the age limit

        var rows = await store.ListAwaitingAsync(now, TimeSpan.FromHours(24));
        Assert.That(rows.Select(r => (r.ListingId, r.ImageIndex)), Is.EqualTo(new[] { ("1", 1), ("1", 0) }));
        Assert.That(rows[0].ImageUrl, Is.EqualTo("https://x/1_1.jpg"));
        Assert.That(rows[0].Day, Is.EqualTo("2026-09-29"));

        await store.DeleteAwaitingAsync(Awaiting("1", 0, Now.AddHours(-3)));
        Assert.That((await store.ListAwaitingAsync(now, TimeSpan.FromHours(24))).Select(r => r.ImageIndex), Is.EqualTo(new[] { 1 }));

        now = Now.AddHours(25);
        Assert.That(await store.ListAwaitingAsync(now, TimeSpan.FromHours(24)), Is.Empty);
    }
}
