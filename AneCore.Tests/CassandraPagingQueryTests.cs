using System.Reflection;
using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class CassandraPagingQueryTests
{
    [Test]
    public void IdentityProjection_PreservesCqlAndIsolatesPagingOptionsAcrossScans()
    {
        // Construct the real driver query provider without connecting to Cassandra.
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
        var session = DispatchProxy.Create<ISession, SessionProxy>();
        ((SessionProxy)(object)session).Cluster = cluster;
        var table = new Table<KnownProduct>(session, new MappingConfiguration(), "known_products");
        // Driver setters mutate their receiver: applying them directly to a shared Table leaks cursors.
        Assert.That(table.SetPageSize(500), Is.SameAs(table));
        var first = table.Select(p => p).SetPageSize(500).SetPagingState(new byte[] { 1 });
        var repeated = table.Select(p => p).SetPageSize(500);
        var concurrent = table.Select(p => p).SetPageSize(100).SetPagingState(new byte[] { 2 });

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.SameAs(table).And.Not.SameAs(repeated).And.Not.SameAs(concurrent));
            Assert.That(first.PagingState, Is.EqualTo(new byte[] { 1 }));
            Assert.That(concurrent.PagingState, Is.EqualTo(new byte[] { 2 }));
            Assert.That(repeated.PagingState, Is.Null);
            Assert.That(table.PagingState, Is.Null);
            Assert.That(first.PageSize, Is.EqualTo(500));
            Assert.That(concurrent.PageSize, Is.EqualTo(100));
            Assert.That(table.PageSize, Is.EqualTo(500));
            Assert.That(first.ToString(), Is.EqualTo(table.ToString()));
        });
    }

    public class SessionProxy : DispatchProxy
    {
        public ICluster Cluster = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Cluster"
            ? Cluster : throw new InvalidOperationException($"Unexpected Cassandra session operation: {method?.Name}");
    }
}
