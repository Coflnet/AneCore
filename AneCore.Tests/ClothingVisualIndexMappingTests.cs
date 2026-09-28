using System.Text;
using System.Text.Json;
using Coflnet.Ane.Opensearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AneCore.Tests;

/// <summary>
/// Verifies the exact JSON <see cref="ClothingVisualIndex"/>/<see cref="OpenSearchVisualReferenceStore"/>
/// send to OpenSearch, using an in-memory <see cref="IConnection"/> instead of a real cluster (per
/// <c>OpenSearch.CreateConnectionSettings</c>'s "virtual so tests can plug in an in-memory connection").
/// Also covers the "cluster lacks the k-NN plugin" degradation path.
/// </summary>
[TestFixture]
public class ClothingVisualIndexMappingTests
{
    private static (ClothingVisualIndex Index, OpenSearchVisualReferenceStore Store, RecordingConnection Connection) Build(
        Func<string, string, (int Status, byte[] Body)>? respond = null)
    {
        var connection = new RecordingConnection(respond);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OPENSEARCH:URL"] = "http://localhost:9200",
            ["OPENSEARCH:USERNAME"] = "test",
            ["OPENSEARCH:PASSWORD"] = "test",
        }).Build();

        var openSearch = new FakeOpenSearch(config, NullLogger<Coflnet.Ane.Opensearch.OpenSearch>.Instance, connection);
        var index = new ClothingVisualIndex(NullLogger<ClothingVisualIndex>.Instance, openSearch);
        var store = new OpenSearchVisualReferenceStore(index, NullLogger<OpenSearchVisualReferenceStore>.Instance);
        return (index, store, connection);
    }

    private static float[] SampleVector()
    {
        var v = new float[512];
        for (var i = 0; i < v.Length; i++) v[i] = 0.1f;
        return v;
    }

    [Test]
    public async Task Initialize_HappyPath_MarksAvailable()
    {
        var (_, store, _) = Build();
        await store.InitializeAsync();
        Assert.That(store.IsAvailable, Is.True);
    }

    [Test]
    public async Task Initialize_CreateIndexRequest_HasExpectedSettingsAndMapping()
    {
        var (_, store, connection) = Build();
        await store.InitializeAsync();

        var body = connection.LastBodyForPath("PUT", "ane_clothing_visual_v1");
        Assert.That(body, Is.Not.Null);
        using var doc = JsonDocument.Parse(body!);
        var root = doc.RootElement;

        var settings = root.GetProperty("settings");
        Assert.That(settings.GetProperty("index.knn").GetBoolean(), Is.True);
        Assert.That(settings.GetProperty("index.number_of_shards").GetInt32(), Is.EqualTo(1));
        Assert.That(settings.GetProperty("index.number_of_replicas").GetInt32(), Is.EqualTo(1));
        Assert.That(settings.GetProperty("index.refresh_interval").GetString(), Is.EqualTo("1s"));

        var props = root.GetProperty("mappings").GetProperty("properties");
        AssertKeyword(props, "groupKey");
        AssertKeyword(props, "clusterSeoId");
        Assert.That(props.GetProperty("seed").GetProperty("type").GetString(), Is.EqualTo("boolean"));
        AssertKeyword(props, "listingId");
        AssertKeyword(props, "platform");
        Assert.That(props.GetProperty("imageUrl").GetProperty("type").GetString(), Is.EqualTo("keyword"));
        Assert.That(props.GetProperty("imageUrl").GetProperty("index").GetBoolean(), Is.False);
        AssertKeyword(props, "colorFamily");
        AssertKeyword(props, "pattern");
        AssertKeyword(props, "gender");
        AssertKeyword(props, "modelId");
        Assert.That(props.GetProperty("createdAt").GetProperty("type").GetString(), Is.EqualTo("date"));

        var embedding = props.GetProperty("embedding");
        Assert.That(embedding.GetProperty("type").GetString(), Is.EqualTo("knn_vector"));
        Assert.That(embedding.GetProperty("dimension").GetInt32(), Is.EqualTo(512));
        var method = embedding.GetProperty("method");
        Assert.That(method.GetProperty("name").GetString(), Is.EqualTo("hnsw"));
        Assert.That(method.GetProperty("engine").GetString(), Is.EqualTo("lucene"));
        Assert.That(method.GetProperty("space_type").GetString(), Is.EqualTo("cosinesimil"));
        var parameters = method.GetProperty("parameters");
        Assert.That(parameters.GetProperty("m").GetInt32(), Is.EqualTo(16));
        Assert.That(parameters.GetProperty("ef_construction").GetInt32(), Is.EqualTo(128));

        return;

        static void AssertKeyword(JsonElement properties, string field) =>
            Assert.That(properties.GetProperty(field).GetProperty("type").GetString(), Is.EqualTo("keyword"), $"field {field}");
    }

    [Test]
    public async Task Initialize_WhenIndexCreationFails_MarksUnavailableAndLogsWarning_DoesNotThrow()
    {
        var (_, store, _) = Build((method, path) =>
            method == "PUT" && !path.Contains("_doc")
                ? (400, Encoding.UTF8.GetBytes("""{"error":{"type":"mapper_parsing_exception","reason":"No handler for type [knn_vector]"}},"status":400}"""))
                : DefaultResponse(method, path));

        Assert.DoesNotThrowAsync(() => store.InitializeAsync());
        await store.InitializeAsync();

        Assert.That(store.IsAvailable, Is.False);
    }

    [Test]
    public async Task WhenUnavailable_SearchReturnsEmptyAndIndexIsSkipped_NoException()
    {
        var (_, store, connection) = Build((method, path) =>
            method == "PUT" && !path.Contains("_doc")
                ? (400, [])
                : DefaultResponse(method, path));
        await store.InitializeAsync();
        Assert.That(store.IsAvailable, Is.False);

        var hits = await store.SearchAsync(SampleVector(), 5, "women/tops", "fashion-clip-v1", "l1", default);
        Assert.That(hits, Is.Empty);

        await store.IndexAsync(0, new ClothingVisualDocument("l1", "Ebay", "women/tops", null, false, null, null, null, null, "fashion-clip-v1", DateTime.UtcNow, SampleVector()), default);
        // Nothing indexed while unavailable - no _doc PUT should have been sent.
        Assert.That(connection.Calls.Any(c => c.Path.Contains("_doc")), Is.False);
    }

    [Test]
    public async Task Search_RequestBody_IsFilteredKnnQueryExcludingListing()
    {
        var (_, store, connection) = Build();
        await store.InitializeAsync();

        await store.SearchAsync(SampleVector(), k: 7, groupKey: "women/tops", modelId: "fashion-clip-v1", excludeListingId: "l1", default);

        var body = connection.LastBodyForPath("POST", "ane_clothing_visual_v1/_search");
        Assert.That(body, Is.Not.Null);
        using var doc = JsonDocument.Parse(body!);
        var knn = doc.RootElement.GetProperty("query").GetProperty("knn").GetProperty("embedding");

        Assert.That(knn.GetProperty("vector").GetArrayLength(), Is.EqualTo(512));
        Assert.That(knn.GetProperty("k").GetInt32(), Is.EqualTo(7));

        var filter = knn.GetProperty("filter").GetProperty("bool");
        var filters = filter.GetProperty("filter").EnumerateArray().ToList();
        Assert.That(filters, Has.Count.EqualTo(2));
        Assert.That(filters[0].GetProperty("term").GetProperty("groupKey").GetProperty("value").GetString(), Is.EqualTo("women/tops"));
        Assert.That(filters[1].GetProperty("term").GetProperty("modelId").GetProperty("value").GetString(), Is.EqualTo("fashion-clip-v1"));

        var mustNot = filter.GetProperty("must_not").EnumerateArray().Single();
        Assert.That(mustNot.GetProperty("term").GetProperty("listingId").GetProperty("value").GetString(), Is.EqualTo("l1"));

        Assert.That(doc.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(7));
    }

    [Test]
    public async Task Index_RequestUsesPlatformListingImageIndexId_AndFullDocument()
    {
        var (_, store, connection) = Build();
        await store.InitializeAsync();
        var document = new ClothingVisualDocument("l1", "Ebay", "women/tops", "cluster-1", true, "https://x/1.jpg", "blue", "solid", "women", "fashion-clip-v1", DateTime.UtcNow, SampleVector());

        await store.IndexAsync(3, document, default);

        var call = connection.Calls.Single(c => c.Path.Contains("_doc"));
        Assert.That(call.Method, Is.EqualTo("PUT"));
        Assert.That(Uri.UnescapeDataString(call.Path), Is.EqualTo("/ane_clothing_visual_v1/_doc/Ebay:l1:3"));

        using var doc = JsonDocument.Parse(call.Body!);
        Assert.That(doc.RootElement.GetProperty("listingId").GetString(), Is.EqualTo("l1"));
        Assert.That(doc.RootElement.GetProperty("clusterSeoId").GetString(), Is.EqualTo("cluster-1"));
        Assert.That(doc.RootElement.GetProperty("seed").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("embedding").GetArrayLength(), Is.EqualTo(512));
    }

    [Test]
    public async Task DeleteByListing_RequestBody_FiltersByListingAndPlatform()
    {
        var (_, store, connection) = Build();
        await store.InitializeAsync();

        await store.DeleteByListingAsync("l1", "Ebay", default);

        var body = connection.LastBodyForPath("POST", "ane_clothing_visual_v1/_delete_by_query");
        Assert.That(body, Is.Not.Null);
        var expected = """{"query":{"bool":{"filter":[{"term":{"listingId":{"value":"l1"}}},{"term":{"platform":{"value":"Ebay"}}}]}}}""";
        Assert.That(Encoding.UTF8.GetString(body!), Is.EqualTo(expected));
    }

    [Test]
    public async Task DeleteOlderThan_RequestBody_FiltersByDateAndSeed()
    {
        var (_, store, connection) = Build();
        await store.InitializeAsync();
        var cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await store.DeleteOlderThanAsync(cutoff, seed: true, default);

        var body = connection.LastBodyForPath("POST", "ane_clothing_visual_v1/_delete_by_query");
        Assert.That(body, Is.Not.Null);
        var expected = """{"query":{"bool":{"filter":[{"range":{"createdAt":{"lt":"2026-01-01T00:00:00Z"}}},{"term":{"seed":{"value":true}}}]}}}""";
        Assert.That(Encoding.UTF8.GetString(body!), Is.EqualTo(expected));
    }

    private static (int Status, byte[] Body) DefaultResponse(string method, string path)
    {
        if (path == "")
            return (200, []); // ping
        if (method == "HEAD")
            return (404, []); // index existence check: always "doesn't exist yet"
        if (method == "PUT" && path.Contains("_doc"))
            return (201, Encoding.UTF8.GetBytes("""{"_index":"ane_clothing_visual_v1","_id":"x","result":"created"}"""));
        if (method == "PUT")
            return (200, Encoding.UTF8.GetBytes("""{"acknowledged":true,"shards_acknowledged":true,"index":"ane_clothing_visual_v1"}"""));
        if (path.EndsWith("_search"))
            return (200, Encoding.UTF8.GetBytes("""{"took":1,"timed_out":false,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0},"hits":{"total":{"value":0,"relation":"eq"},"max_score":null,"hits":[]}}"""));
        if (path.EndsWith("_delete_by_query"))
            return (200, Encoding.UTF8.GetBytes("""{"took":1,"timed_out":false,"total":0,"deleted":0,"batches":0,"version_conflicts":0,"noops":0,"retries":{"bulk":0,"search":0},"throttled_millis":0,"requests_per_second":-1.0,"throttled_until_millis":0,"failures":[]}"""));
        return (200, []);
    }

    /// <summary>Exposes the protected <see cref="OpenSearch.CreateConnectionSettings"/> seam to plug in <see cref="RecordingConnection"/>.</summary>
    private sealed class FakeOpenSearch(IConfiguration configuration, ILogger<Coflnet.Ane.Opensearch.OpenSearch> logger, IConnection connection)
        : Coflnet.Ane.Opensearch.OpenSearch(configuration, logger)
    {
        public override ConnectionSettings CreateConnectionSettings() =>
            new ConnectionSettings(new SingleNodeConnectionPool(OpenSearchUrl()), connection)
                .DisableDirectStreaming();
    }

    /// <summary>
    /// Records every request's method/path/body and answers with a routable, minimally-valid response so
    /// the real client code (ping, exists-check, create, index, search, delete-by-query) runs end to end
    /// without a real cluster.
    /// </summary>
    private sealed class RecordingConnection(Func<string, string, (int Status, byte[] Body)>? respond) : InMemoryConnection
    {
        private readonly List<(string Method, string Path, byte[]? Body)> calls = [];

        public IReadOnlyList<(string Method, string Path, byte[]? Body)> Calls => calls;

        public byte[]? LastBodyForPath(string method, string path) =>
            calls.LastOrDefault(c => c.Method == method && Uri.UnescapeDataString(c.Path.Trim('/')) == path).Body;

        public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken = default)
        {
            var method = requestData.Method.ToString();
            var path = requestData.Uri.AbsolutePath;
            byte[]? body = null;
            if (requestData.PostData != null)
            {
                using var ms = new MemoryStream();
                requestData.PostData.Write(ms, requestData.ConnectionSettings);
                body = ms.ToArray();
            }
            calls.Add((method, path, body));

            var (status, respBody) = (respond ?? DefaultResponse)(method, path.Trim('/'));
            var routed = new InMemoryConnection(respBody, status);
            return routed.RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
