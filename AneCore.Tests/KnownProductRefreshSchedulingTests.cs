using System.Diagnostics;
using System.Threading.Channels;
using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

public class KnownProductRefreshSchedulingTests
{
    [Test]
    public async Task DefaultStart_StillReadsImmediately()
    {
        var store = new SignalingStore();
        using var catalog = new KnownProductCatalog(store, refreshInterval: TimeSpan.FromMinutes(1));
        catalog.StartPeriodicRefresh();
        await store.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task AlreadyRefreshed_DeferredStart_WaitsForIntervalAndCannotStartADuplicateTimer()
    {
        var store = new SignalingStore();
        using var catalog = new KnownProductCatalog(store, refreshInterval: TimeSpan.FromMilliseconds(150));
        await catalog.RefreshAsync();
        await store.Reads.Reader.ReadAsync();
        var started = Stopwatch.GetTimestamp();
        catalog.StartPeriodicRefresh(refreshImmediately: false);
        catalog.StartPeriodicRefresh(); // already started: must not schedule a second immediate read
        var readAt = await store.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(Stopwatch.GetElapsedTime(started, readAt), Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(125)));
    }

    private sealed class SignalingStore : IKnownProductStore
    {
        public readonly Channel<long> Reads = Channel.CreateUnbounded<long>();
        public Task<IReadOnlyList<KnownProduct>> GetAllAsync()
        {
            Reads.Writer.TryWrite(Stopwatch.GetTimestamp());
            return Task.FromResult<IReadOnlyList<KnownProduct>>([]);
        }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<KnownProduct?> GetAsync(string id) => Task.FromResult<KnownProduct?>(null);
        public Task UpsertAsync(KnownProduct product) => throw new NotSupportedException();
        public Task DeleteAsync(string id) => throw new NotSupportedException();
    }
}
