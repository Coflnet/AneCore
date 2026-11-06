using System.Text;
using Coflnet.Ane.Opensearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AneCore.Tests;

public class RegularIndexInitializationTests
{
    [Test]
    public async Task ConcurrentClientsThatBothObserveMissingRegularIndexAcceptTheWinningCreation()
    {
        var connection = new CreateConnection();
        var index = Index(connection);
        var first = index.Client();
        await connection.FirstMissingRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = index.Client();
        try
        {
            await connection.BothMissingReads.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { connection.ReleaseMissingReads.TrySetResult(true); }

        var clients = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clients, Has.Length.EqualTo(2));
        Assert.That(connection.Creates, Is.EqualTo(2));
        Assert.That(connection.AlreadyExistsResponses, Is.EqualTo(1), "the losing create must encounter the real OpenSearch error type");
        var cached = await index.Client();
        Assert.That(clients, Does.Contain(cached));
        Assert.That(connection.Creates, Is.EqualTo(2), "a successful initialization remains cached");
    }

    [Test]
    public void OtherRegularIndexCreateErrorsStillFailInitialization()
    {
        var connection = new CreateConnection("mapper_parsing_exception");
        connection.ReleaseMissingReads.SetResult(true);
        var index = Index(connection);
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await index.Client());
        Assert.That(error!.Message, Does.Contain("Failed to create index"));
        Assert.That(connection.Creates, Is.EqualTo(1));
        Assert.That(connection.AlreadyExistsResponses, Is.Zero);
    }

    private static ClothingVisualIndex Index(IConnection connection) =>
        new(NullLogger<ClothingVisualIndex>.Instance, new TestOpenSearch(connection));

    private sealed class TestOpenSearch(IConnection connection) : Coflnet.Ane.Opensearch.OpenSearch(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OPENSEARCH:URL"] = "http://unused.invalid:9200", ["OPENSEARCH:USERNAME"] = "synthetic", ["OPENSEARCH:PASSWORD"] = "synthetic"
        }).Build(), NullLogger<Coflnet.Ane.Opensearch.OpenSearch>.Instance)
    {
        public override ConnectionSettings CreateConnectionSettings() =>
            new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://unused.invalid:9200")), connection).DisablePing();
    }

    private sealed class CreateConnection(string? otherCreateError = null) : InMemoryConnection
    {
        public TaskCompletionSource<bool> FirstMissingRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> BothMissingReads { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseMissingReads { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Creates, AlreadyExistsResponses;
        private int missingReads;

        public override async Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken = default)
        {
            var method = requestData.Method.ToString();
            var path = requestData.Uri.AbsolutePath;
            int status;
            string response;
            if (method == "HEAD" && path == "/") { status = 200; response = ""; }
            else if (method == "HEAD" && path == "/ane_clothing_visual_v1")
            {
                var reads = Interlocked.Increment(ref missingReads);
                FirstMissingRead.TrySetResult(true);
                if (reads == 2) BothMissingReads.TrySetResult(true);
                await ReleaseMissingReads.Task.WaitAsync(cancellationToken);
                status = 404; response = "{}";
            }
            else if (method == "PUT" && path == "/ane_clothing_visual_v1")
            {
                var creates = Interlocked.Increment(ref Creates);
                var type = otherCreateError ?? (creates > 1 ? "resource_already_exists_exception" : null);
                status = type == null ? 200 : 400;
                response = type == null ? "{\"acknowledged\":true,\"shards_acknowledged\":true,\"index\":\"ane_clothing_visual_v1\"}"
                    : "{\"error\":{\"type\":\"" + type + "\",\"reason\":\"SYNTHETIC index create outcome\"},\"status\":400}";
                if (type == "resource_already_exists_exception") Interlocked.Increment(ref AlreadyExistsResponses);
            }
            else throw new AssertionException($"Unexpected fake request {method} {path}");
            return await new InMemoryConnection(Encoding.UTF8.GetBytes(response), status).RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
