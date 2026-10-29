using System.Reflection;
using System.Text.RegularExpressions;
using Cassandra;
using Cassandra.Data.Linq;
using Coflnet.Ane;

namespace AneCore.Tests;

public class NativePriceHistoryTests
{
    private static PricePoint Point(string? currency = "SEK", double price = 500, DateTime? day = null) => new()
    {
        ProductSeoId = "tool-used", Date = day ?? DateTime.UtcNow.Date, Currency = currency,
        AveragePrice = price, MedianPrice = price, MinPrice = price, MaxPrice = price, SampleCount = 1
    };

    [Test]
    public async Task LazySchemaAndRealDriverMappingAgreeOnDateAndCurrencyKeys()
    {
        using var database = new Database();
        var store = new ProductTableService(database.Session);
        await store.GetNativePriceHistoryAsync("tool-used");
        var schema = database.Commands.Single(c => c.Sql.StartsWith("CREATE"));
        Assert.That(Compact(schema.Sql), Does.Contain("PRIMARYKEY((product_seo_id),date,currency)")
            .And.Contain("CLUSTERINGORDERBY(dateDESC,currencyASC)")
            .And.Contain("default_time_to_live=" + ProductDataRetention.PriceHistoryTtlSeconds));
        database.Commands.Clear();
        // Emit DDL from the actual private Table mapping as well: a correct literal alone cannot prove its key mapping.
        var table = (Table<PricePoint>)typeof(ProductTableService)
            .GetField("nativePriceHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
        await table.CreateIfNotExistsAsync();
        var driverSchema = Compact(database.Commands.Single().Sql);
        Assert.That(driverSchema.Contains("PRIMARYKEY((product_seo_id),date,currency)")
            || driverSchema.Contains("PRIMARYKEY(product_seo_id,date,currency)"), Is.True, driverSchema);
        Assert.That(driverSchema, Does.Contain("CLUSTERINGORDERBY(dateDESC,currencyASC)"));
    }

    [Test]
    public async Task NativeWritesKeepSameDayCurrenciesSeparateAndNormalizeWithoutMutatingTheCaller()
    {
        using var database = new Database();
        var store = new ProductTableService(database.Session);
        var input = Point(" sek ");
        await store.InsertNativePricePointAsync(input);
        await store.InsertNativePricePointAsync(Point("EUR", 40));
        await store.InsertNativePricePointAsync(Point("SEK", 550));
        var rows = await store.GetNativePriceHistoryAsync("tool-used", 1);
        Assert.Multiple(() =>
        {
            Assert.That(input.Currency, Is.EqualTo(" sek "));
            Assert.That(rows.Select(p => (p.Currency, p.MedianPrice)), Is.EquivalentTo(new[] { ("EUR", 40d), ("SEK", 550d) }));
            Assert.That(database.Commands.Count(c => c.Sql.StartsWith("CREATE")), Is.EqualTo(1));
            Assert.That(database.Commands.Where(c => c.Sql.StartsWith("INSERT")).All(c => c.Sql.Contains("price_history_native")), Is.True);
            Assert.That(database.Legacy, Is.Empty);
        });
    }

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(90, 90)]
    [TestCase(999, 365)]
    public async Task ReadBindsBothDateBoundsAndClampsDaysWithoutACurrencyOrRowLimit(int days, int expectedDays)
    {
        using var database = new Database();
        await new ProductTableService(database.Session).GetNativePriceHistoryAsync("literal-product", days);
        var query = database.Commands.Single(c => c.Sql.StartsWith("SELECT"));
        Assert.Multiple(() =>
        {
            Assert.That(Compact(query.Sql), Does.Contain("product_seo_id=?").And.Contain("date>=?").And.Contain("date<=?"));
            Assert.That(query.Sql.ToUpperInvariant(), Does.Not.Contain("LIMIT").And.Not.Contain("ALLOW FILTERING"));
            Assert.That(Compact(query.Sql), Does.Not.Contain("currency=?"));
            Assert.That(query.Values[0], Is.EqualTo("literal-product"));
            Assert.That(Date(query.Values[2]).Subtract(Date(query.Values[1])).TotalDays, Is.EqualTo(expectedDays));
        });
    }

    [Test]
    public async Task ReadReturnsEveryCurrencyInTheWindowAndExcludesOtherProductsAndFutureDays()
    {
        using var database = new Database();
        foreach (var code in new[] { "SEK", "EUR", "PLN" }) database.Native.Add(("tool-used", DateTime.UtcNow.Date, code), Point(code));
        database.Native.Add(("other", DateTime.UtcNow.Date, "SEK"), Point());
        database.Native.Add(("tool-used", DateTime.UtcNow.Date.AddDays(1), "SEK"), Point(day: DateTime.UtcNow.Date.AddDays(1)));
        database.Native.Add(("tool-used", DateTime.UtcNow.Date.AddDays(-400), "SEK"), Point(day: DateTime.UtcNow.Date.AddDays(-400)));
        var rows = await new ProductTableService(database.Session).GetNativePriceHistoryAsync("tool-used", 1);
        Assert.That(rows.Select(p => p.Currency), Is.EquivalentTo(new[] { "SEK", "EUR", "PLN" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("XXX")]
    [TestCase("ZZZ")]
    [TestCase("SE")]
    public void UnknownCurrencyIsRejectedBeforeSchemaOrWrite(string? currency)
    {
        using var database = new Database();
        Assert.ThrowsAsync<ArgumentException>(() => new ProductTableService(database.Session).InsertNativePricePointAsync(Point(currency)));
        Assert.That(database.Commands, Is.Empty);
    }

    [TestCase("product")]
    [TestCase("date")]
    [TestCase("unspecified_date")]
    [TestCase("intraday")]
    [TestCase("samples")]
    [TestCase("average_nan")]
    [TestCase("median_infinite")]
    [TestCase("minimum_zero")]
    [TestCase("maximum_negative")]
    public void InvalidObservationIsRejectedBeforeSchemaOrWrite(string invalid)
    {
        using var database = new Database();
        var point = Point();
        switch (invalid)
        {
            case "product": point.ProductSeoId = " "; break;
            case "date": point.Date = default; break;
            case "unspecified_date": point.Date = DateTime.SpecifyKind(point.Date, DateTimeKind.Unspecified); break;
            case "intraday": point.Date = point.Date.AddHours(1); break;
            case "samples": point.SampleCount = 0; break;
            case "average_nan": point.AveragePrice = double.NaN; break;
            case "median_infinite": point.MedianPrice = double.PositiveInfinity; break;
            case "minimum_zero": point.MinPrice = 0; break;
            case "maximum_negative": point.MaxPrice = -1; break;
        }
        Assert.ThrowsAsync<ArgumentException>(() => new ProductTableService(database.Session).InsertNativePricePointAsync(point));
        Assert.That(database.Commands, Is.Empty);
    }

    [TestCase(-365)]
    [TestCase(-1460)]
    public async Task InsertUsesExistingDateAnchoredTtlRatherThanRestartingRetention(int ageDays)
    {
        using var database = new Database();
        var point = Point(day: DateTime.UtcNow.Date.AddDays(ageDays));
        var before = ProductDataRetention.GetPriceHistoryTtlSeconds(point);
        await new ProductTableService(database.Session).InsertNativePricePointAsync(point);
        var after = ProductDataRetention.GetPriceHistoryTtlSeconds(point);
        var insert = database.Commands.Single(c => c.Sql.StartsWith("INSERT"));
        var ttlMatch = Regex.Match(insert.Sql, @"USING TTL (\?|\d+)");
        Assert.That(ttlMatch.Success, Is.True, insert.Sql);
        var ttl = ttlMatch.Groups[1].Value == "?" ? Convert.ToInt32(insert.Values[^1]) : int.Parse(ttlMatch.Groups[1].Value);
        Assert.That(ttl, Is.InRange(after, before));
        Assert.That(ttl, Is.LessThan(ProductDataRetention.PriceHistoryTtlSeconds));
    }

    [Test]
    public async Task ConcurrentFirstReadAndWriteWaitForOneSuccessfulSchemaCreation()
    {
        using var database = new Database();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        database.BeforeCreate = async () => { entered.SetResult(); await release.Task; };
        var store = new ProductTableService(database.Session);
        var read = store.GetNativePriceHistoryAsync("tool-used");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var write = store.InsertNativePricePointAsync(Point());
        try
        {
            Assert.That(database.Commands, Has.Count.EqualTo(1));
            Assert.That(read.IsCompleted || write.IsCompleted, Is.False);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(read, write).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(database.Commands.Count(c => c.Sql.StartsWith("CREATE")), Is.EqualTo(1));
    }

    [Test]
    public async Task FailedSchemaIsRetriedByTheNextWriterAndReadsDoNotHideFailures()
    {
        using var database = new Database { CreateFailures = 1 };
        var store = new ProductTableService(database.Session);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.GetNativePriceHistoryAsync("tool-used"));
        await store.InsertNativePricePointAsync(Point());
        database.ReadFailure = true;
        Assert.ThrowsAsync<InvalidOperationException>(() => store.GetNativePriceHistoryAsync("tool-used"));
        database.ReadFailure = false;
        Assert.That(await store.GetNativePriceHistoryAsync("tool-used"), Has.Count.EqualTo(1));
        Assert.That(database.Commands.Count(c => c.Sql.StartsWith("CREATE")), Is.EqualTo(2));
    }

    [Test]
    public async Task LegacyAccessorAndReadWriteRemainOnLegacyTableWithUnknownCurrencyIntact()
    {
        using var database = new Database();
        var store = new ProductTableService(database.Session);
        await store.InsertPricePointAsync(Point(null, 40));
        await store.InsertNativePricePointAsync(Point());
        var rows = await store.GetPriceHistoryAsync("tool-used");
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Currency, Is.Null);
        Assert.That(rows[0].MedianPrice, Is.EqualTo(40));
        Assert.That(store.PriceHistory.ToString(), Does.Contain("price_history").And.Not.Contain("price_history_native"));
    }

    [Test, NonParallelizable]
    public async Task NormalInitializationIncludesNativeTableWithoutReenteringTheSchemaSemaphore()
    {
        using var database = new Database();
        var initialized = typeof(ProductTableService).GetField("tablesInitialized", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = initialized.GetValue(null);
        initialized.SetValue(null, false);
        try
        {
            var store = new ProductTableService(database.Session);
            await store.InitializeTablesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await store.GetNativePriceHistoryAsync("tool-used");
            Assert.That(database.Commands.Count(c => c.Sql.StartsWith("CREATE") && c.Sql.Contains("price_history_native")), Is.EqualTo(1));
        }
        finally { initialized.SetValue(null, previous); }
    }

    private static string Compact(string sql) => Regex.Replace(sql.Replace("\"", ""), @"\s+", "");
    private static DateTime Date(object? value) => value is DateTimeOffset offset ? offset.UtcDateTime : (DateTime)value!;
    public sealed record Command(string Sql, object?[] Values);

    private sealed class Database : IDisposable
    {
        private readonly Cluster cluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
        private readonly ICluster clusterView;
        private readonly Dictionary<PreparedStatement, string> prepared = [];
        public ISession Session { get; }
        public List<Command> Commands = [];
        public Dictionary<(string Product, DateTime Date, string? Currency), PricePoint> Native = [];
        public Dictionary<(string Product, DateTime Date), PricePoint> Legacy = [];
        public Func<Task>? BeforeCreate;
        public int CreateFailures;
        public bool ReadFailure;

        public Database()
        {
            clusterView = DispatchProxy.Create<ICluster, ClusterProxy>();
            ((ClusterProxy)(object)clusterView).Cluster = cluster;
            Session = DispatchProxy.Create<ISession, SessionProxy>();
            ((SessionProxy)(object)Session).Owner = this;
        }

        private async Task<RowSet> Execute(object statement)
        {
            var sql = statement switch { string text => text, SimpleStatement simple => simple.QueryString, BoundStatement bound => prepared[bound.PreparedStatement], _ => throw new NotSupportedException(statement.GetType().Name) };
            var values = statement is string ? [] : (object?[])statement.GetType()
                .GetProperty("QueryValues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(statement)!;
            lock (Commands) Commands.Add(new(sql, values));
            if (sql.StartsWith("CREATE") && sql.Contains("price_history_native"))
            {
                if (BeforeCreate != null) await BeforeCreate();
                if (CreateFailures-- > 0) throw new InvalidOperationException("synthetic schema failure");
            }
            if (sql.StartsWith("SELECT column_name")) return new Rows([new CqlColumn { Name = "column_name", Index = 0, Type = typeof(string), TypeCode = ColumnTypeCode.Varchar }], [new object[] { "currency" }]);
            if (sql.StartsWith("INSERT") && sql.Contains("price_history"))
            {
                var columns = Regex.Match(sql, @"\(([^)]+)\)\s*VALUES", RegexOptions.IgnoreCase).Groups[1].Value.Split(',').Select(c => c.Trim().Trim('"')).ToList();
                var data = columns.Select((name, i) => (name, values[i])).ToDictionary(p => p.name, p => p.Item2);
                var row = new PricePoint { ProductSeoId = (string)data["product_seo_id"]!, Date = Date(data["date"]), Currency = (string?)data["currency"], AveragePrice = (double)data["average_price"]!, MedianPrice = (double)data["median_price"]!, MinPrice = (double)data["min_price"]!, MaxPrice = (double)data["max_price"]!, SampleCount = (int)data["sample_count"]! };
                lock (Native)
                {
                    if (sql.Contains("price_history_native")) Native[(row.ProductSeoId, row.Date, row.Currency)] = row;
                    else Legacy[(row.ProductSeoId, row.Date)] = row;
                }
            }
            if (sql.StartsWith("SELECT") && sql.Contains("price_history"))
            {
                if (ReadFailure) throw new InvalidOperationException("synthetic read failure");
                lock (Native)
                {
                    var rows = sql.Contains("price_history_native") ? Native.Where(p => p.Key.Product == (string)values[0]!).Select(p => p.Value) : Legacy.Where(p => p.Key.Product == (string)values[0]!).Select(p => p.Value);
                    return Rows.Prices(rows.Where(p => p.Date >= Date(values[1]) && (values.Length < 3 || p.Date <= Date(values[2]))).ToList());
                }
            }
            return new RowSet();
        }

        public void Dispose() => cluster.Dispose();

        // The real Metadata getter initializes a control connection. The driver only needs the
        // already-constructed metadata's serializer for DDL, as in Noer's FakeCassandraSession.
        public class ClusterProxy : DispatchProxy
        {
            public Cluster Cluster = null!;
            protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
            {
                "get_Configuration" => Cluster.Configuration,
                "get_Metadata" => typeof(Cluster).GetField("_metadata", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Cluster),
                _ => throw new NotSupportedException("Cluster." + method.Name)
            };
        }

        public class SessionProxy : DispatchProxy
        {
            public Database Owner = null!;
            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                switch (method!.Name)
                {
                    case "get_Cluster": return Owner.clusterView;
                    case "get_Keyspace": return "test";
                    case "PrepareAsync":
                        var statement = new Prepared(); Owner.prepared[statement] = (string)args![0]!;
                        return Task.FromResult<PreparedStatement>(statement);
                    case "ExecuteAsync": return Owner.Execute(args![0]!);
                    default: throw new NotSupportedException(method.Name);
                }
            }
            private sealed class Prepared : PreparedStatement { }
        }
    }

    private sealed class Rows(CqlColumn[] columns, IEnumerable<object?[]> values) : RowSet
    {
        public override CqlColumn[] Columns { get => columns; set { } }
        public override IEnumerator<Row> GetEnumerator() => values.Select(value => (Row)Activator.CreateInstance(typeof(Row), BindingFlags.NonPublic | BindingFlags.Instance, null,
            [value, columns, columns.ToDictionary(c => c.Name, c => c.Index)], null)!).GetEnumerator();
        public static Rows Prices(IEnumerable<PricePoint> points)
        {
            var names = new[] { "product_seo_id", "date", "currency", "average_price", "median_price", "min_price", "max_price", "sample_count" };
            var types = new[] { typeof(string), typeof(DateTimeOffset), typeof(string), typeof(double), typeof(double), typeof(double), typeof(double), typeof(int) };
            var codes = new[] { ColumnTypeCode.Varchar, ColumnTypeCode.Timestamp, ColumnTypeCode.Varchar, ColumnTypeCode.Double, ColumnTypeCode.Double, ColumnTypeCode.Double, ColumnTypeCode.Double, ColumnTypeCode.Int };
            var columns = names.Select((name, i) => new CqlColumn { Name = name, Index = i, Type = types[i], TypeCode = codes[i] }).ToArray();
            return new(columns, points.Select(p => new object?[] { p.ProductSeoId, new DateTimeOffset(p.Date), p.Currency, p.AveragePrice, p.MedianPrice, p.MinPrice, p.MaxPrice, p.SampleCount }));
        }
    }
}
