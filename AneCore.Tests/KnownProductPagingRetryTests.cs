using System.Reflection;
using Cassandra;
using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

public class KnownProductPagingRetryTests
{
    [Test]
    public async Task ReadTimeout_RetriesOnlyFailedPageWithoutDuplicateRows_AndNextScanStartsFresh()
    {
        using var database = new PagedSession { Failures = 2 };
        var store = new CassandraKnownProductStore(database.Session);
        var products = await store.GetAllAsync();
        Assert.That(products.Select(p => p.Id), Is.EqualTo(new[] { "one", "two", "three", "four", "five", "six" }));
        Assert.That(database.Cursors, Is.EqualTo(new[] { 0, 1, 1, 1, 2 }));
        Assert.That(database.PageSizes, Is.All.EqualTo(500));

        database.Cursors.Clear();
        var repeated = await store.GetAllAsync();
        Assert.That(repeated.Select(p => p.Id), Is.EqualTo(products.Select(p => p.Id)));
        Assert.That(database.Cursors, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void ExhaustedPageReadTimeout_PropagatesAfterThreeAttempts()
    {
        using var database = new PagedSession { Failures = 3 };
        var store = new CassandraKnownProductStore(database.Session);
        Assert.ThrowsAsync<ReadTimeoutException>(async () => await store.GetAllAsync());
        Assert.That(database.Cursors, Is.EqualTo(new[] { 0, 1, 1, 1 }));
    }

    [Test]
    public async Task ExhaustedRefresh_KeepsPreviousCompleteCatalogue()
    {
        using var database = new PagedSession();
        using var catalog = new KnownProductCatalog(new CassandraKnownProductStore(database.Session));
        await catalog.RefreshAsync();
        var complete = catalog.Snapshot;
        database.Failures = 3;
        await catalog.RefreshAsync();
        Assert.That(catalog.Snapshot, Is.SameAs(complete));
        Assert.That(catalog.Snapshot, Has.Count.EqualTo(6));
    }

    [Test]
    public void NoHostAvailable_IsNotRetriedByTheReadTimeoutPolicy()
    {
        using var database = new PagedSession { Failures = 1, Failure = () => new NoHostAvailableException(new Dictionary<System.Net.IPEndPoint, Exception>()) };
        var store = new CassandraKnownProductStore(database.Session);
        Assert.ThrowsAsync<NoHostAvailableException>(async () => await store.GetAllAsync());
        Assert.That(database.Cursors, Is.EqualTo(new[] { 0, 1 }));
    }

    private sealed class PagedSession : IDisposable
    {
        private readonly Cluster cluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
        public ISession Session { get; }
        public readonly List<int> Cursors = [];
        public readonly List<int> PageSizes = [];
        public int Failures;
        public Func<Exception> Failure = () => new ReadTimeoutException(ConsistencyLevel.LocalOne, 0, 1, false);

        public PagedSession()
        {
            Session = DispatchProxy.Create<ISession, SessionProxy>();
            ((SessionProxy)Session).Owner = this;
        }

        public Task<RowSet> Execute(IStatement statement)
        {
            var cursor = statement.PagingState?.Single() ?? 0;
            Cursors.Add(cursor);
            PageSizes.Add(statement.PageSize);
            if (cursor == 1 && Failures-- > 0)
                return Task.FromException<RowSet>(Failure());
            return Task.FromResult<RowSet>(new Page(cursor switch
            {
                0 => ["one", "two"], 1 => ["three", "four"], 2 => ["five", "six"],
                _ => throw new InvalidOperationException("Unexpected cursor")
            }, cursor == 2 ? null : [(byte)(cursor + 1)]));
        }

        public void Dispose() => cluster.Dispose();

        public class SessionProxy : DispatchProxy
        {
            public PagedSession Owner = null!;
            protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
            {
                "get_Cluster" => Owner.cluster,
                "get_Keyspace" => "test",
                "PrepareAsync" => Task.FromResult<PreparedStatement>(new Prepared()),
                "ExecuteAsync" => Owner.Execute((IStatement)args![0]!),
                _ => throw new NotSupportedException(method.Name)
            };
            private sealed class Prepared : PreparedStatement { }
        }

        private sealed class Page : RowSet
        {
            private readonly CqlColumn[] columns = [new() { Name = "id", Index = 0, Type = typeof(string), TypeCode = ColumnTypeCode.Varchar }];
            private readonly List<Row> rows;
            public Page(string[] ids, byte[]? next)
            {
                PagingState = next;
                rows = ids.Select(id => (Row)Activator.CreateInstance(typeof(Row), BindingFlags.NonPublic | BindingFlags.Instance, null,
                    [new object[] { id }, columns, new Dictionary<string, int> { ["id"] = 0 }], null)!).ToList();
            }
            public override CqlColumn[] Columns { get => columns; set { } }
            public override IEnumerator<Row> GetEnumerator() => rows.GetEnumerator();
        }
    }
}
