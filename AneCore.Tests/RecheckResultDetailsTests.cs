using MessagePack;

namespace Coflnet.Ane;

public sealed class RecheckResultDetailsTests
{
    [Test]
    public void DetailsSurviveTheMessagePackRoundTrip()
    {
        var message = new RecheckResult
        {
            ListingId = "10286893245", Platform = Platform.Vinted, Status = RecheckStatus.Available, CheckedAt = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            Attributes = new Dictionary<string, string> { ["color"] = "Grün, Braun" },
            Description = "Warme Strickjacke"
        };

        var back = MessagePackSerializer.Deserialize<RecheckResult>(MessagePackSerializer.Serialize(message));

        Assert.That(back.Attributes!["color"], Is.EqualTo("Grün, Braun"));
        Assert.That(back.Description, Is.EqualTo("Warme Strickjacke"));
    }

    [Test]
    public void AMessageOfTheOldShapeStillReads()
    {
        // keys 0-6 only, as the deployed scraper writes them: array of 7 elements
        var oldBytes = MessagePackSerializer.Serialize(new object?[] { "10286893245", (int)Platform.Vinted, (int)RecheckStatus.Available, null, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc), null, null });

        var back = MessagePackSerializer.Deserialize<RecheckResult>(oldBytes);

        Assert.That(back.ListingId, Is.EqualTo("10286893245"));
        Assert.That(back.Attributes, Is.Null);
        Assert.That(back.Description, Is.Null);
        Assert.That(back.Snapshot, Is.Null);
        Assert.That(back.SnapshotRecoveryId, Is.Null);
    }

    [Test]
    public void RecoveryIsAdditiveAndCarriesActualFullSource()
    {
        var request = new RecrawlRequest { ListingId = "42", Platform = Platform.Kleinanzeigen, SnapshotRecoveryId = "snapshot-test", SnapshotRecoveryExpiresAt = DateTime.UtcNow.AddMinutes(15) };
        var requestBack = MessagePackSerializer.Deserialize<RecrawlRequest>(MessagePackSerializer.Serialize(request));
        Assert.That(requestBack.SnapshotRecoveryId, Is.EqualTo(request.SnapshotRecoveryId));
        Assert.That(requestBack.SnapshotRecoveryExpiresAt, Is.EqualTo(request.SnapshotRecoveryExpiresAt));
        var old = MessagePackSerializer.Serialize(new object?[] { "42", null, (int)Platform.Kleinanzeigen, "product" });
        Assert.That(MessagePackSerializer.Deserialize<RecrawlRequest>(old).SnapshotRecoveryId, Is.Null);
        var message = new RecheckResult { SnapshotRecoveryId = request.SnapshotRecoveryId,
            Snapshot = new Listing { Id = "42", Platform = Platform.Kleinanzeigen, Description = "Complete original seller text" } };
        var back = MessagePackSerializer.Deserialize<RecheckResult>(MessagePackSerializer.Serialize(message));
        Assert.That(back.SnapshotRecoveryId, Is.EqualTo(request.SnapshotRecoveryId));
        Assert.That(back.Snapshot!.Description, Is.EqualTo(message.Snapshot.Description));
    }
    [Test]
    public void CrowdDiscoveryUsesAppendedKeyAndSurvivesTheMessagePackRoundTrip()
    {
        var bytes = MessagePackSerializer.Serialize(new object?[] { "2656147978", "https://www.kleinanzeigen.de/s-anzeige/shaft/2656147978-306-3062", (int)Platform.Kleinanzeigen, "", null, null, true });
        var request = MessagePackSerializer.Deserialize<RecrawlRequest>(bytes);
        var back = MessagePackSerializer.Deserialize<RecrawlRequest>(MessagePackSerializer.Serialize(request));
        Assert.That(typeof(RecrawlRequest).GetProperty("IsDiscovery")?.GetValue(back), Is.True, "appended key6 must carry explicit discovery intent");
        Assert.That(back.SnapshotRecoveryId, Is.Null);
    }

    [TestCase(4)]
    [TestCase(6)]
    public void LegacyRecrawlRequestsNeverBecomeDiscovery(int fields)
    {
        var bytes = MessagePackSerializer.Serialize(new object?[] { "42", null, (int)Platform.Kleinanzeigen, "product", null, null }.Take(fields).ToArray());
        var request = MessagePackSerializer.Deserialize<RecrawlRequest>(bytes);
        Assert.That(typeof(RecrawlRequest).GetProperty("IsDiscovery")?.GetValue(request), Is.False, "legacy availability and snapshot-recovery shapes must default discovery tofalse");
    }

}
