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
    private const string GoToolsUrl = "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-160-mm-poliert-kunststoffueberzug_144938_35930";
    private static ProductStoreReference GoToolsQuote(DateTimeOffset observed) => Quote(observed) with
    { Provider = "GoTools", Url = GoToolsUrl, MerchantSku = "35930", Gtin = "4003773013235" };
    private const string CobraUrl = "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-250-mm-spannweite-46-mm-poliert_127452_18450";
    private const string WrenchUrl = "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-250-mm-spannweite-52-mm-verchromt-kunststoffueberzug_127591_18589";

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

    [Test]
    public void BothRetailersProveTheSameVerifiedArticleIndependently()
    {
        var now = DateTimeOffset.UtcNow;
        var product = new Product { SeoId = Tool.Id, Name = Tool.Name, Brand = Tool.Brand, Model = Tool.Model,
            IdentifierType = "catalogued_goods", IdentifierValue = Tool.Id };
        foreach (var quote in new[] { Quote(now.AddMinutes(-1)), GoToolsQuote(now.AddMinutes(-1)) })
        {
            Assert.That(ProductStoreReferencePolicy.IsFresh(quote, now), Is.True, quote.Provider);
            Assert.That(ProductStoreReferencePolicy.IsEligible(quote, product, [Tool], now), Is.True, quote.Provider);
        }
        product.Model = "03 01 160 EAN";
        Assert.That(ProductStoreReferencePolicy.IsEligible(GoToolsQuote(now.AddMinutes(-1)), product, [Tool], now), Is.False,
            "the base article quote cannot prove a differently packaged manufacturer article");
    }

    [TestCase("different GTIN")]
    [TestCase("missing GTIN")]
    [TestCase("different SKU")]
    [TestCase("other manufacturer article")]
    [TestCase("other brand")]
    [TestCase("unknown provider")]
    [TestCase("different product path")]
    [TestCase("query")]
    [TestCase("fragment")]
    [TestCase("credentials")]
    [TestCase("port")]
    [TestCase("HTTP")]
    [TestCase("merchant mismatch")]
    [TestCase("expired")]
    [TestCase("future")]
    [TestCase("extended TTL")]
    [TestCase("non-EUR")]
    [TestCase("stock unknown")]
    [TestCase("used")]
    public void GoToolsCoverageCannotEscapeItsSingleVerifiedArticleAndFreshOffer(string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var quote = GoToolsQuote(now.AddMinutes(-1));
        quote = reason switch
        {
            "different GTIN" => quote with { Gtin = "4003773022787" },
            "missing GTIN" => quote with { Gtin = null },
            "different SKU" => quote with { MerchantSku = "35931" },
            "other manufacturer article" => quote with { Mpn = "03 01 160 EAN" },
            "other brand" => quote with { Brand = "Bahco" },
            "unknown provider" => quote with { Provider = "Unknown" },
            "different product path" => quote with { Url = GoToolsUrl.Replace("_144938_35930", "_144938_35931") },
            "query" => quote with { Url = GoToolsUrl + "?referrer=1" },
            "fragment" => quote with { Url = GoToolsUrl + "#offer" },
            "credentials" => quote with { Url = GoToolsUrl.Replace("https://", "https://someone@") },
            "port" => quote with { Url = GoToolsUrl.Replace("www.gotools.de", "www.gotools.de:444") },
            "HTTP" => quote with { Url = GoToolsUrl.Replace("https://", "http://") },
            "merchant mismatch" => quote with { Url = GoToolsUrl.Replace("www.gotools.de", "www.lefeld.de") },
            "expired" => quote with { ObservedAt = now.AddMinutes(-15), ExpiresAt = now },
            "future" => GoToolsQuote(now.AddSeconds(1)),
            "extended TTL" => quote with { ExpiresAt = quote.ExpiresAt.AddSeconds(1) },
            "non-EUR" => quote with { Currency = "SEK" },
            "stock unknown" => quote with { Availability = "Unknown" },
            "used" => quote with { Condition = "used" },
            _ => throw new ArgumentException(reason)
        };
        Assert.That(ProductStoreReferencePolicy.IsFresh(quote, now), Is.False, reason);
    }

    [TestCase("knipex-2612200", "26 12 200", "4003773023142", "18690", "https://www.gotools.de/marken/knipex/knipex-flachrundzange-laenge-200-mm-poliert-gerade-mehrkomponenten-huellen_127692_18690")]
    [TestCase("knipex-2615200", "26 15 200", "4003773035022", "51163", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/flachrundzangen-storchschnabelzangen/flachrundzange-storchschnabelzange/flachrundzange-knipex/knipex-flachrundzange-laenge-200-mm-verchromt-gerade-mehrkomponenten-huellen_160171_51163")]
    [TestCase("knipex-1106160", "11 06 160", "4003773021933", "18576", "https://www.gotools.de/marken/knipex/knipex-abisolierzange-laenge-160-mm-vde-verchromt-mehrkomponenten-huellen_127578_18576")]
    [TestCase("knipex-1242195", "12 42 195", "4003773054580", "18691", "https://www.gotools.de/marken/knipex/knipex-automatikabisolierzange-multistrip-10-laenge-195-mm-0-03-10-awg-32-7-mm_127693_18691")]
    [TestCase("knipex-975304", "97 53 04", "4003773028017", "18581", "https://www.gotools.de/marken/knipex/knipex-aderendhuelsenzange-gesamtlaenge-180-mm-0-08-10-0-16-0-awg-28-5-mm_127583_18581")]
    [TestCase("knipex-121202", "12 12 02", "4003773048077", "52547", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/abisolierzangen/abisolierzange/praezisionsabisolierzange-knipex/knipex-praezisionsabisolierzange-laenge-195-mm-mehrkomponeten-huellen-0-03-2-08-awg-32-14-mm_161555_52547")]
    [TestCase("knipex-0301180", "03 01 180", "4003773013242", "35540", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-180-mm-poliert-kunststoffueberzug_144548_35540")]
    [TestCase("knipex-0301200", "03 01 200", "4003773013259", "57238", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-200-mm-poliert-kunststoffueberzug_166246_57238")]
    [TestCase("knipex-975314", "97 53 14", "4003773041474", "18659", "https://www.gotools.de/marken/knipex/knipex-aderendhuelsenzange-gesamtlaenge-180-mm-0-08-10-0-awg-28-7-mm-brueniert-mehrkomponenten-huellen_127661_18659")]
    [TestCase("knipex-8701250", "87 01 250", "4003773022022", "18450", CobraUrl)]
    [TestCase("knipex-8603250", "86 03 250", "4003773033837", "18589", WrenchUrl)]
    public void ExpandedGoToolsArticlesRequireTheirOwnCompleteVerifiedTuple(string id, string mpn, string gtin, string sku, string url)
    {
        var now = DateTimeOffset.UtcNow;
        var known = KnownProductSeed.LoadAll(includeExpandedCatalog: true).Single(k => k.Id == id);
        var reference = GoToolsQuote(now.AddMinutes(-1)) with { KnownProductId = id, Mpn = mpn, Gtin = gtin, MerchantSku = sku, Url = url };
        var product = new Product { SeoId = id, Name = known.Name, Brand = known.Brand, Model = known.Model };
        Assert.That(ProductStoreReferencePolicy.IsEligible(reference, product, [known], now), Is.True);
        foreach (var mixed in new[]
        {
            reference with { Url = GoToolsUrl }, reference with { Gtin = "4003773013235" },
            reference with { MerchantSku = "35930" }, reference with { Mpn = "03 01 160" },
            reference with { KnownProductId = Tool.Id }, reference with { Url = url + "?tracking=1" },
            reference with { Mpn = mpn + " SB" }, reference with { Availability = "Unknown" }
        }) Assert.That(ProductStoreReferencePolicy.IsFresh(mixed, now), Is.False, "approved articles cannot borrow one another's identity fields");
    }
}
