using System.Reflection;
using System.Text.Json;
using Cassandra;
using Coflnet.Ane;
using Coflnet.Ane.KnownProducts;
using Coflnet.Ane.ShopReferences;

namespace AneCore.Tests;

public class ProductStoreReferenceStoreTests
{
    private static readonly KnownProduct Tool = KnownProductSeed.LoadAll(includeExpandedCatalog: true).Single(k => k.Id == "knipex-0301160");
    private static ProductStoreReference Quote(DateTimeOffset observed) => new(Tool.Id, "Lefeld",
        "https://www.lefeld.de/knipex-kombizange-03-01-160", "PUBLIC-FIXTURE", Tool.Brand, Tool.Model, null,
        10.07m, "EUR", "LimitedAvailability", "unknown", observed, observed.AddMinutes(15));
    private static string Payload(ProductStoreReference reference) => JsonSerializer.Serialize(reference);

    // The installed driver's generic GetValue<T>(string) is virtual; no wire serializer is needed.
    private sealed class FixtureRow(string column, object value) : Row
    {
        public override T GetValue<T>(string name) => name == column ? (T)value : throw new KeyNotFoundException(name);
    }
    private static RowSet Rows(string? column = null, object? value = null)
    {
        var rows = new RowSet();
        if (column != null) typeof(RowSet).GetMethod("AddRow", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(rows, [new FixtureRow(column, value!)]);
        return rows;
    }

    public class Session : DispatchProxy
    {
        public List<SimpleStatement> Statements = [];
        public Func<SimpleStatement, Task<RowSet>> Execute = _ => Task.FromResult(Rows());
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != "ExecuteAsync" || args?[0] is not SimpleStatement statement)
                throw new InvalidOperationException("Unexpected driver operation: " + method?.Name);
            Statements.Add(statement);
            return Execute(statement);
        }
    }
    private static (CassandraProductStoreReferenceStore Store, Session Session) Store()
    {
        var session = DispatchProxy.Create<ISession, Session>();
        return (new(session), (Session)(object)session);
    }
    private static object[] Values(SimpleStatement statement) => (object[])typeof(SimpleStatement)
        .GetProperty("QueryValues", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(statement)!;

    [Test]
    public async Task PhysicalRowsPastLogicalExpiryOrWithFutureObservationNeverProveCoverage()
    {
        var (store, session) = Store();
        foreach (var quote in new[] { Quote(DateTimeOffset.UtcNow.AddMinutes(-16)), Quote(DateTimeOffset.UtcNow.AddMinutes(1)) })
        {
            session.Execute = s => Task.FromResult(s.QueryString.StartsWith("SELECT") ? Rows("payload", Payload(quote)) : Rows());
            Assert.That(await store.GetAsync("existing-product"), Is.Empty);
        }
        Assert.That(session.Statements.Count(s => s.QueryString.StartsWith("CREATE")), Is.EqualTo(1));
    }

    [Test]
    public async Task OlderAndEqualQuotesNeverWriteOrRefreshTheirExpiry()
    {
        var (store, session) = Store();
        var accepted = Quote(DateTimeOffset.UtcNow.AddMinutes(-1));
        session.Execute = s => Task.FromResult(s.QueryString.StartsWith("SELECT") ? Rows("payload", Payload(accepted)) : Rows());
        await store.UpsertAsync("existing-product", accepted with { Price = 999 });
        await store.UpsertAsync("existing-product", Quote(accepted.ObservedAt.AddMinutes(-1)));
        Assert.That(session.Statements.Any(s => s.QueryString.StartsWith("UPDATE") || s.QueryString.StartsWith("INSERT")), Is.False);
        var read = (await store.GetAsync("existing-product")).Single();
        Assert.That(read, Is.EqualTo(accepted));
    }

    [Test]
    public async Task FailedPayloadCasRereadsAndCannotOverwriteNewerConcurrentObservation()
    {
        var (store, session) = Store();
        var incoming = Quote(DateTimeOffset.UtcNow.AddMinutes(-1));
        var previous = Payload(Quote(incoming.ObservedAt.AddMinutes(-1)));
        var newer = incoming with { ObservedAt = incoming.ObservedAt.AddSeconds(30), ExpiresAt = incoming.ExpiresAt.AddSeconds(30), Price = 42 };
        var stored = previous;
        session.Execute = s =>
        {
            if (s.QueryString.StartsWith("SELECT")) return Task.FromResult(Rows("payload", stored));
            if (!s.QueryString.StartsWith("UPDATE")) return Task.FromResult(Rows());
            Assert.That(s.QueryString, Does.EndWith("IF payload = ?"));
            Assert.That(Values(s), Is.EqualTo(new object[] { Payload(incoming), "existing-product", "Lefeld", previous }));
            stored = Payload(newer);
            return Task.FromResult(Rows("[applied]", false));
        };
        await store.UpsertAsync("existing-product", incoming);
        Assert.That((await store.GetAsync("existing-product")).Single(), Is.EqualTo(newer));
        Assert.That(session.Statements.Count(s => s.QueryString.StartsWith("UPDATE")), Is.EqualTo(1));
        Assert.That(session.Statements.Where(s => s.QueryString.StartsWith("SELECT")).All(s => s.ConsistencyLevel == ConsistencyLevel.Serial), Is.True,
            "Projection must read the latest accepted LWT value, not a lagging replica after a newer index snapshot.");
    }

    [Test]
    public async Task FirstInsertAndSubsequentCasAreConditionalAndConflictRetriesAreBounded()
    {
        var (store, session) = Store();
        var incoming = Quote(DateTimeOffset.UtcNow.AddMinutes(-1));
        var previous = Payload(Quote(incoming.ObservedAt.AddMinutes(-1)));
        var reads = 0;
        session.Execute = s => Task.FromResult(s.QueryString.StartsWith("SELECT")
            ? ++reads == 1 ? Rows() : Rows("payload", previous)
            : s.QueryString.StartsWith("CREATE") ? Rows() : Rows("[applied]", false));
        Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync("existing-product", incoming));
        var writes = session.Statements.Where(s => s.QueryString.StartsWith("INSERT") || s.QueryString.StartsWith("UPDATE")).ToArray();
        Assert.That(writes, Has.Length.EqualTo(4));
        Assert.That(writes[0].QueryString, Does.EndWith("IF NOT EXISTS"));
        Assert.That(writes.Skip(1).All(s => s.QueryString.EndsWith("IF payload = ?")), Is.True);
        Assert.That(await store.GetAsync("existing-product"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task InitializationSharesPendingSchemaAndCanRetryAfterFailure()
    {
        var (store, session) = Store();
        var pending = new TaskCompletionSource<RowSet>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Execute = _ => pending.Task;
        var first = store.InitializeAsync(); var second = store.InitializeAsync();
        Assert.That(session.Statements, Has.Count.EqualTo(1));
        pending.SetException(new InvalidOperationException("SYNTHETIC schema failure"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await second);
        session.Execute = _ => Task.FromResult(Rows());
        await store.InitializeAsync();
        Assert.That(session.Statements, Has.Count.EqualTo(2));
    }

    [Test]
    public void EligibleIdentityAllowsAbsentIndexIdentifierButRejectsConflictingCanonicalIdentityAndStaleQuote()
    {
        var now = DateTimeOffset.UtcNow;
        var quote = Quote(now.AddMinutes(-1));
        var current = new Product { SeoId = "existing-product", Name = Tool.Name, Brand = Tool.Brand, Model = Tool.Model,
            IdentifierType = "catalogued_goods", Attributes = new() { ["product_kind"] = "hand_tool", ["article_number"] = Tool.Model } };
        Assert.That(ProductStoreReferencePolicy.IsEligible(quote, current, [Tool], now), Is.True);
        current.IdentifierValue = "different-known-product";
        Assert.That(ProductStoreReferencePolicy.IsEligible(quote, current, [Tool], now), Is.False);
        current.IdentifierValue = Tool.Id;
        Assert.That(ProductStoreReferencePolicy.IsEligible(quote, current, [Tool], quote.ExpiresAt), Is.False);
        Assert.That(ProductStoreReferencePolicy.IsEligible(quote with { ExpiresAt = quote.ExpiresAt.AddSeconds(1) }, current, [Tool], now), Is.False);
    }
}
