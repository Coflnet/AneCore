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
    }
}
