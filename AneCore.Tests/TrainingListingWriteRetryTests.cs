using Cassandra;
using Coflnet.Ane;
using Coflnet.Ane.TrainingListings;

namespace AneCore.Tests;

/// <summary>An in-memory table behind the statement seam that throws on demand, like a Scylla node that times out.</summary>
internal sealed class FlakyCql : ITrainingListingCql
{
    public readonly HashSet<(string Day, int Shard, int Platform, string Id)> Rows = new();
    public readonly Dictionary<(string Day, int Shard), int> Counters = new();
    public readonly List<(string Day, int Shard, int Platform, string Id)> ByTimeRows = new();
    public int Inserts, Counts;
    /// <summary>Statement kind (SELECT, INSERT, UPDATE) to the exceptions thrown by its next calls.</summary>
    public readonly Dictionary<string, Queue<Exception>> Failures = new();
    /// <summary>When set, a failing INSERT is applied before it throws (the unknown outcome of a write timeout).</summary>
    public bool ApplyBeforeThrowing = true;

    public void Fail(string kind, int times, Func<Exception> make)
    {
        Failures[kind] = new Queue<Exception>(Enumerable.Range(0, times).Select(_ => make()));
    }

    public static WriteTimeoutException Timeout() => new(ConsistencyLevel.LocalOne, 0, 1, "SIMPLE");

    private static string Kind(SimpleStatement s) => s.QueryString.Split(' ')[0];

    public Task<bool> AnyRowAsync(SimpleStatement statement)
    {
        MaybeThrow(statement);
        var v = statement.QueryValues;
        return Task.FromResult(Rows.Contains(((string)v[0], (int)v[1], (int)v[2], (string)v[3])));
    }

    public Task ExecuteAsync(SimpleStatement statement)
    {
        var kind = Kind(statement);
        var v = statement.QueryValues;
        if (statement.QueryString.StartsWith("INSERT INTO training_listings_by_time"))
        {
            ByTimeRows.Add(((string)v[1], (int)v[2], (int)v[3], (string)v[4]));
            return Task.CompletedTask;
        }
        if (kind == "INSERT")
        {
            var failing = Failures.TryGetValue(kind, out var q) && q.Count > 0;
            if (!failing || ApplyBeforeThrowing)
            {
                Inserts++;
                Rows.Add(((string)v[0], (int)v[1], (int)v[2], (string)v[3]));
            }
            MaybeThrow(statement);
        }
        else
        {
            MaybeThrow(statement);
            Counts++;
            Counters[((string)v[0], (int)v[1])] = Counters.GetValueOrDefault(((string)v[0], (int)v[1])) + 1;
        }
        return Task.CompletedTask;
    }

    private void MaybeThrow(SimpleStatement statement)
    {
        if (Failures.TryGetValue(Kind(statement), out var q) && q.Count > 0)
            throw q.Dequeue();
    }
}

[TestFixture]
public class TrainingListingWriteRetryTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 23, 59, 58, DateTimeKind.Utc);
    private List<TimeSpan> waits = null!;
    private TrainingRetryPolicy policy = null!;

    [SetUp]
    public void SetUp()
    {
        waits = new();
        policy = new TrainingRetryPolicy([TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400)], 0.5,
            (wait, _) => { waits.Add(wait); return Task.CompletedTask; });
    }

    private static TrainingListing Row(string id, DateTime? firstSeen) => new()
    {
        Platform = Platform.Kleinanzeigen, ListingId = id, Title = "t", ImageUrls = ["https://img/a.jpg"], FirstSeenAt = firstSeen, Scope = TrainingListingScope.Kept
    };

    private CassandraTrainingListingStore Store(FlakyCql cql, Func<DateTime>? clock = null) => new(cql, retry: policy, clock: clock ?? (() => Now));

    [Test]
    public async Task InsertTimeouts_AreRetried_ListingStoredOnce_NothingThrows()
    {
        var cql = new FlakyCql();
        cql.Fail("INSERT", 2, FlakyCql.Timeout); // both timeouts happened after the write was applied: unknown outcome

        Assert.That(await Store(cql).AddAsync(Row("1", Now)), Is.True);

        Assert.That(cql.Rows, Has.Count.EqualTo(1));
        Assert.That(cql.ByTimeRows, Is.Not.Empty);
        Assert.That(cql.Counters.Values.Sum(), Is.EqualTo(1));
        Assert.That(waits, Has.Count.EqualTo(2));
        Assert.That(waits[0], Is.InRange(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)));
    }

    [Test]
    public async Task ExistsCheckTimeouts_AreRetried()
    {
        var cql = new FlakyCql();
        cql.Fail("SELECT", 3, () => new ReadTimeoutException(ConsistencyLevel.LocalOne, 0, 1, false));

        Assert.That(await Store(cql).AddAsync(Row("1", Now)), Is.True);
        Assert.That(cql.Rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void ExhaustedRetries_ThrowTheLastError()
    {
        var cql = new FlakyCql();
        cql.Fail("INSERT", 10, FlakyCql.Timeout);

        Assert.ThrowsAsync<WriteTimeoutException>(() => Store(cql).AddAsync(Row("1", Now)));
        Assert.That(waits, Has.Count.EqualTo(3)); // policy delays used up
        Assert.That(cql.Counters, Is.Empty);
    }

    [Test]
    public void NonTransientErrors_AreNotRetried()
    {
        var cql = new FlakyCql();
        cql.Fail("INSERT", 1, () => new InvalidQueryException("bad"));

        Assert.ThrowsAsync<InvalidQueryException>(() => Store(cql).AddAsync(Row("1", Now)));
        Assert.That(waits, Is.Empty);
    }

    [Test]
    public async Task CounterTimeout_IsNotRetried_AndDoesNotFailTheListing()
    {
        var cql = new FlakyCql();
        cql.Fail("UPDATE", 1, FlakyCql.Timeout);

        Assert.That(await Store(cql).AddAsync(Row("1", Now)), Is.True);

        Assert.That(cql.Rows, Has.Count.EqualTo(1));
        Assert.That(waits, Is.Empty); // a counter write of unknown outcome is not repeated: never counted twice
        Assert.That(cql.Counts, Is.EqualTo(0));
    }

    [Test]
    public async Task CounterUnavailable_IsRetried()
    {
        var cql = new FlakyCql();
        cql.Fail("UPDATE", 1, () => new UnavailableException(ConsistencyLevel.LocalOne, 1, 0));

        await Store(cql).AddAsync(Row("1", Now));
        Assert.That(cql.Counters.Values.Sum(), Is.EqualTo(1));
    }

    [Test]
    public async Task Redelivery_SameMessage_SameKey_NoSecondRowNoSecondCount()
    {
        var cql = new FlakyCql();
        var store = Store(cql);
        Assert.That(await store.AddAsync(Row("1", Now)), Is.True);
        Assert.That(await store.AddAsync(Row("1", Now)), Is.False);

        Assert.That(cql.Inserts, Is.EqualTo(1));
        Assert.That(cql.Rows.Single().Day, Is.EqualTo("2026-09-30"));
        Assert.That(cql.Counters.Values.Sum(), Is.EqualTo(1));
    }

    [Test]
    public async Task LaterMessageOfSameKey_DoesNotOverwriteTheFirstWrite()
    {
        var cql = new FlakyCql();
        var store = Store(cql);
        await store.AddAsync(Row("1", Now));
        var changed = Row("1", Now);
        changed.Title = "changed";
        Assert.That(await store.AddAsync(changed), Is.False);
        Assert.That(cql.Inserts, Is.EqualTo(1));
    }

    [Test]
    public async Task NoFirstSeen_RedeliveryAfterMidnight_DoesNotCreateARowInTheNextDay()
    {
        var cql = new FlakyCql();
        var clock = Now; // 23:59:58
        var store = Store(cql, () => clock);
        Assert.That(await store.AddAsync(Row("1", null)), Is.True);
        clock = Now.AddSeconds(5); // redelivered after midnight UTC

        Assert.That(await store.AddAsync(Row("1", null)), Is.False);

        Assert.That(cql.Rows.Select(r => r.Day), Is.EqualTo(new[] { "2026-09-30" }));
    }

    [Test]
    public async Task NoFirstSeen_RetryAcrossMidnight_NoSecondRow()
    {
        var cql = new FlakyCql();
        var clock = Now;
        // the insert is applied, times out, and the retry happens after midnight
        cql.Fail("INSERT", 1, FlakyCql.Timeout);
        policy = policy with { Delay = (_, _) => { clock = Now.AddSeconds(5); return Task.CompletedTask; } };

        Assert.That(await Store(cql, () => clock).AddAsync(Row("1", null)), Is.True);
        Assert.That(cql.Rows, Has.Count.EqualTo(1));
        Assert.That(cql.Inserts, Is.EqualTo(1).Or.EqualTo(2));
        Assert.That(cql.Rows.Select(r => r.Day).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task InMemoryStore_NoFirstSeen_MidnightRedelivery_NoSecondRow()
    {
        var clock = Now;
        var store = new InMemoryTrainingListingStore(() => clock);
        await store.AddAsync(Row("1", null));
        clock = Now.AddSeconds(5);
        Assert.That(await store.AddAsync(Row("1", null)), Is.False);
        Assert.That((await store.ListDaysAsync()).Sum(d => d.Rows), Is.EqualTo(1));
    }

    [Test]
    public void Insert_IsAPlainWrite_NoLightweightTransaction()
    {
        var statement = CassandraTrainingListingStore.BuildInsertStatement(Row("1", Now), "2026-09-30", 1, TimeSpan.FromDays(14));
        Assert.That(statement.QueryString, Does.Not.Contain("IF NOT EXISTS"));
        Assert.That(statement.QueryString, Does.Contain("USING TTL 1209600"));
    }

    [Test]
    public void Jitter_StaysWithinTheConfiguredBand()
    {
        for (var i = 0; i < 200; i++)
            Assert.That(TrainingRetryPolicy.Default.WaitFor(2), Is.InRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5)));
    }
}
