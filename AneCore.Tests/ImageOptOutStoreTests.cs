using System.Reflection;
using Cassandra;
using Coflnet.Ane;
using Coflnet.Ane.ImageRights;

namespace AneCore.Tests;

[TestFixture]
public class ImageOptOutStoreTests
{
    [Test]
    public async Task ExactListingWithdrawalIsMonotonicPlatformScopedAndNeverARightsDefault()
    {
        var store = new InMemoryImageOptOutStore();
        Assert.That(await store.IsOptedOutAsync(Platform.Kleinanzeigen, "same"), Is.False);
        await store.MarkOptedOutAsync(Platform.Kleinanzeigen, "same");
        await store.MarkOptedOutAsync(Platform.Kleinanzeigen, "same");
        Assert.That(await store.IsOptedOutAsync(Platform.Kleinanzeigen, "same"), Is.True);
        Assert.That(await store.IsOptedOutAsync(Platform.Vinted, "same"), Is.False);
        Assert.That(await store.IsOptedOutAsync(Platform.Kleinanzeigen, "another"), Is.False);
    }

    [Test]
    public async Task CassandraMarkerSurvivesNewStoreAndUsesQuorumNoExpiryWithoutNegativeCache()
    {
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
        var session = DispatchProxy.Create<Cassandra.ISession, RegistrySession>();
        var backend = (RegistrySession)(object)session; backend.Cluster = cluster;
        var first = new CassandraImageOptOutStore(session, createTable: false);
        Assert.That(await first.IsOptedOutAsync(Platform.Kleinanzeigen, "same"), Is.False);
        await new CassandraImageOptOutStore(session, false).MarkOptedOutAsync(Platform.Kleinanzeigen, "same");
        Assert.That(await first.IsOptedOutAsync(Platform.Kleinanzeigen, "same"), Is.True, "an earlier negative must not mask a withdrawal from another process");
        Assert.That(await new CassandraImageOptOutStore(session, false).IsOptedOutAsync(Platform.Kleinanzeigen, "same"), Is.True);
        Assert.That(backend.Queries.Single(query => query.QueryString.StartsWith("INSERT")).QueryString, Does.EndWith("USING TTL 0"));
        Assert.That(backend.Queries.All(query => query.ConsistencyLevel == ConsistencyLevel.LocalQuorum), Is.True);
        Assert.That(backend.Queries.All(query => query.QueryValues[0] is int), Is.True);
        Assert.That(backend.Queries.Any(query => query.QueryString.Contains("DELETE") || query.QueryString.Contains("image_url")), Is.False);
    }

    [Test]
    public void RegistryReadAndWriteFailuresPropagateRatherThanGrantPermission()
    {
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
        var session = DispatchProxy.Create<Cassandra.ISession, RegistrySession>();
        var backend = (RegistrySession)(object)session; backend.Cluster = cluster; backend.Fail = true;
        var store = new CassandraImageOptOutStore(session, false);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.IsOptedOutAsync(Platform.Kleinanzeigen, "same"));
        Assert.ThrowsAsync<InvalidOperationException>(() => store.MarkOptedOutAsync(Platform.Kleinanzeigen, "same"));
    }

    [TestCase(Platform.Unknown, "same")]
    [TestCase(Platform.Kleinanzeigen, "")]
    public void MissingScopeCannotCreateOrClearAnOptOut(Platform platform, string id)
    {
        var store = new InMemoryImageOptOutStore();
        Assert.ThrowsAsync<ArgumentException>(() => store.MarkOptedOutAsync(platform,id));
        Assert.ThrowsAsync<ArgumentException>(() => store.IsOptedOutAsync(platform,id));
    }

    public class RegistrySession : DispatchProxy
    {
        public ICluster Cluster = null!;
        public bool Fail;
        public List<SimpleStatement> Queries = [];
        private readonly HashSet<(int,string)> rows = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_Cluster") return Cluster;
            if (method.Name == "get_Keyspace") return "synthetic";
            if (method.Name == "ExecuteAsync")
            {
                var query = (SimpleStatement)args![0]!; Queries.Add(query);
                if (Fail) return Task.FromException<RowSet>(new InvalidOperationException("synthetic unavailable"));
                var key = ((int)query.QueryValues[0], (string)query.QueryValues[1]);
                if (query.QueryString.StartsWith("INSERT")) { rows.Add(key); return Task.FromResult<RowSet>(new Rows([])); }
                return Task.FromResult<RowSet>(new Rows(rows.Contains(key) ? [Marker()] : []));
            }
            throw new NotSupportedException(method.Name);
        }
        private static Row Marker()
        {
            var column = new CqlColumn { Name = "opted_out_at", Index = 0, Type = typeof(DateTime) };
            return (Row)Activator.CreateInstance(typeof(Row), BindingFlags.Instance | BindingFlags.NonPublic, null,
                [new object[] { DateTime.UtcNow }, new[] { column }, new Dictionary<string,int> { [column.Name] = 0 }], null)!;
        }
    }
    private sealed class Rows(Row[] rows) : RowSet
    { public override IEnumerator<Row> GetEnumerator() => ((IEnumerable<Row>)rows).GetEnumerator(); }
}
