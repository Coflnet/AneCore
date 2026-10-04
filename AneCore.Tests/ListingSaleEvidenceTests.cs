namespace Coflnet.Ane;

public sealed class ListingSaleEvidenceTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static RecheckResult Observation(RecheckStatus status, int hours, string? sale = null) =>
        new() { Platform = Platform.OLX, ListingId = "pl:ID1cy22y", Status = status, CheckedAt = Now.AddHours(hours), SaleEvidence = sale };

    [Test]
    public void WithdrawalIsNotSaleAndUnknownDoesNotEraseHistory()
    {
        var available = ListingSaleEvidence.Apply(null, Observation(RecheckStatus.Available, 0));
        var withdrawn = ListingSaleEvidence.Apply(available, Observation(RecheckStatus.Gone, 1));
        Assert.Multiple(() =>
        {
            Assert.That(withdrawn.Status, Is.EqualTo("withdrawn"));
            Assert.That(withdrawn.SoldObservedAt, Is.Null);
            Assert.That(withdrawn.LastAvailableAt, Is.EqualTo(Now));
            Assert.That(withdrawn.FirstUnavailableAt, Is.EqualTo(Now.AddHours(1)));
            Assert.That(ListingSaleEvidence.Apply(withdrawn, Observation(RecheckStatus.Unknown, 2)), Is.EqualTo(withdrawn));
            Assert.That(ListingSaleEvidence.Apply(withdrawn, Observation(RecheckStatus.Available, -1)), Is.EqualTo(withdrawn));
        });
    }

    [Test]
    public void ExplicitDeclarationSurvivesDeletionWithoutRefreshingObservationAgeAndReactivationClearsIt()
    {
        var sold = ListingSaleEvidence.Apply(null, Observation(RecheckStatus.Gone, 0, "seller_declared_sold"));
        var deleted = ListingSaleEvidence.Apply(sold, Observation(RecheckStatus.Gone, 10));
        Assert.Multiple(() =>
        {
            Assert.That(deleted.Status, Is.EqualTo("reported_sold"));
            Assert.That(deleted.Evidence, Is.EqualTo("seller_declared_sold"));
            Assert.That(deleted.SoldObservedAt, Is.EqualTo(Now));
            Assert.That(ListingSaleEvidence.Apply(deleted, Observation(RecheckStatus.Gone, 20, "seller_declared_sold")).SoldObservedAt, Is.EqualTo(Now));
        });
        var reactivated = ListingSaleEvidence.Apply(deleted, Observation(RecheckStatus.Available, 11));
        Assert.Multiple(() =>
        {
            Assert.That(reactivated.Status, Is.EqualTo("available"));
            Assert.That(reactivated.Evidence, Is.Null);
            Assert.That(reactivated.SoldObservedAt, Is.Null);
            Assert.That(reactivated.FirstUnavailableAt, Is.Null);
        });
    }

    [TestCase("ebay_sold_search_card")]
    [TestCase("sold_out_marker")]
    [TestCase("seller_declared_sold")]
    public void OnlyExplicitAllowlistedSignalsAreEligible(string signal) =>
        Assert.That(ListingSaleEvidence.Apply(null, Observation(RecheckStatus.Gone, 0, signal)).Status, Is.EqualTo("reported_sold"));

    [Test]
    public void ArbitrarySaleMetadataIsNeverConfirmation() =>
        Assert.That(ListingSaleEvidence.Apply(null, Observation(RecheckStatus.Gone, 0, "probably_sold")).Status, Is.EqualTo("withdrawn"));
    [Test]
    public void MessagePackReadsLegacyFiveFieldsAndRoundTripsNewEvidence()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePack.MessagePackWriter(buffer);
        writer.WriteArrayHeader(5);
        writer.Write("pl:ID1cy22y"); writer.Write((int)Platform.OLX); writer.Write((int)RecheckStatus.Gone);
        writer.WriteNil(); writer.Write(Now); writer.Flush();
        var old = MessagePack.MessagePackSerializer.Deserialize<RecheckResult>(buffer.WrittenMemory);
        Assert.Multiple(() => { Assert.That(old.CheckedAt, Is.EqualTo(Now)); Assert.That(old.SaleEvidence, Is.Null); Assert.That(old.ListingId, Is.EqualTo("pl:ID1cy22y")); });
        old.SaleEvidence = "seller_declared_sold";
        var current = MessagePack.MessagePackSerializer.Deserialize<RecheckResult>(MessagePack.MessagePackSerializer.Serialize(old));
        Assert.That(current.SaleEvidence, Is.EqualTo("seller_declared_sold"));
    }

    [Test]
    public void InferredRemovalIsSeparateAndCannotDowngradeExplicitDeclaration()
    {
        var observation = Observation(RecheckStatus.Gone, 0, SaleSignalPolicy.Inferred);
        observation.AgeBasis = "first_seen";
        var inferred = ListingSaleEvidence.Apply(null, observation);
        Assert.That(inferred.Status, Is.EqualTo("inferred_sold"));
        Assert.That(inferred.AgeBasis, Is.EqualTo("first_seen"));
        var explicitSale = ListingSaleEvidence.Apply(inferred, Observation(RecheckStatus.Gone, 1, "seller_declared_sold"));
        observation.CheckedAt = Now.AddHours(2);
        Assert.That(ListingSaleEvidence.Apply(explicitSale, observation).Evidence, Is.EqualTo("seller_declared_sold"));
        Assert.That(ListingSaleEvidence.Apply(explicitSale, observation).Status, Is.EqualTo("reported_sold"));
    }

}
