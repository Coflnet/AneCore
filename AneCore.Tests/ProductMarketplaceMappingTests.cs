using System.Reflection;
using System.Text;
using System.Text.Json;
using Coflnet.Ane.Opensearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AneCore.Tests;

[TestFixture]
public class ProductMarketplaceMappingTests
{
    [Test]
    public void ProductMapping_IndexesMembershipAndVersion_WithoutExcludingThemFromSource()
    {
        var client = Client(new InMemoryConnection());
        using var json = JsonDocument.Parse(client.RequestResponseSerializer.SerializeToString(
            ProductIndex.Mapping(new TypeMappingDescriptor<ProductDocument>())));
        var properties = json.RootElement.GetProperty("properties");
        Assert.That(properties.TryGetProperty("platforms", out _), Is.False);
        Assert.That(properties.TryGetProperty("platformsUpdatedAt", out _), Is.False);
        Assert.That(ProductIndex.MarketplaceSourcesPresenceVersion, Is.EqualTo(1));
        Assert.That(ProductIndex.MarketplaceSourcesVersion, Is.EqualTo(2));
        Assert.That(properties.GetProperty("marketplaceSourcesVersion").GetProperty("type").GetString(), Is.EqualTo("integer"));
        AssertMarketplaceSources(properties);
        AssertStoreReferences(properties);
        AssertNativeEurMinimum(properties);
        Assert.That(ProductIndex.SourceExcludes, Does.Not.Contain("marketplaceSources"));
        Assert.That(ProductIndex.SourceExcludes, Does.Not.Contain("marketplaceSourcesVersion"));
        Assert.That(ProductIndex.SourceExcludes, Does.Not.Contain("storeReferences"));
    }

    [Test]
    public void LegacyProductSource_DeserializesWithoutMarketplaceMetadata_AndNewMetadataRoundTrips()
    {
        var client = Client(new InMemoryConnection());
        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes("""
            {"seoId":"legacy-camera","name":"Camera","offersFound":3,"createdAt":"2026-10-01T00:00:00Z",
             "lastUpdated":"2026-10-02T00:00:00Z","attributes":[],"sampleTitles":[],"relatedSeoIds":[]}
            """));
        var product = client.SourceSerializer.Deserialize<ProductDocument>(legacy);
        Assert.That(typeof(ProductDocument).GetProperty("NativeEurMin")?.GetValue(product), Is.Null);
        Assert.That(typeof(ProductDocument).GetProperty("NativeEurMinVersion")?.GetValue(product), Is.Null);
        using var defaultJson = JsonDocument.Parse(JsonSerializer.Serialize(product, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        foreach (var field in new[] { "nativeEurMin", "nativeEurMinVersion" })
            Assert.That(!defaultJson.RootElement.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null, Is.True);
        Assert.That(product.SeoId, Is.EqualTo("legacy-camera"));
        Assert.That(product.OffersFound, Is.EqualTo(3));
        Assert.That(product.MarketplaceSourcesVersion, Is.Null);
        Assert.That(product.MarketplaceSources, Is.Null);
        Assert.That(product.ScopedMinPrice, Is.Null);
        Assert.That(product.ScopedPriceCurrency, Is.Null);

        var updated = product with
        {
            MarketplaceSourcesVersion = ProductIndex.MarketplaceSourcesVersion,
            MarketplaceSources = [new ProductMarketplaceSource("Ebay", "DE", "EUR", 12, 99),
                new ProductMarketplaceSource("Vinted", "FR"), new ProductMarketplaceSource("Vinted", null)]
        };
        using var source = new MemoryStream();
        client.SourceSerializer.Serialize(updated, source);
        source.Position = 0;
        var restored = client.SourceSerializer.Deserialize<ProductDocument>(source);
        Assert.That(restored.MarketplaceSourcesVersion, Is.EqualTo(ProductIndex.MarketplaceSourcesVersion));
        Assert.That(restored.MarketplaceSources, Is.EqualTo(updated.MarketplaceSources));
        Assert.That(restored.Name, Is.EqualTo(product.Name));
        Assert.That(restored.OffersFound, Is.EqualTo(product.OffersFound));
    }

    [TestCase(" eur ", "EUR")]
    [TestCase("gbp", "GBP")]
    [TestCase("SEK", "SEK")]
    [TestCase("PLN", "PLN")]
    [TestCase("ABC", null)]
    [TestCase("XXX", null)]
    [TestCase("€", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void NativeCurrencyRequiresAnExplicitKnownCode(string? input, string? expected) =>
        Assert.That(Coflnet.Ane.NativePriceCurrency.Normalize(input), Is.EqualTo(expected));

    [Test]
    public void PresenceVersionOneSourceRemainsReadableWithoutPrices()
    {
        var client = Client(new InMemoryConnection());
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("""
            {"seoId":"v1","marketplaceSourcesVersion":1,"marketplaceSources":[{"platform":"Ebay","country":"DE"}]}
            """));
        var product = client.SourceSerializer.Deserialize<ProductDocument>(source);
        Assert.That(product.MarketplaceSourcesVersion, Is.EqualTo(ProductIndex.MarketplaceSourcesPresenceVersion));
        Assert.That(product.MarketplaceSources, Is.EqualTo(new[] { new ProductMarketplaceSource("Ebay", "DE") }));
    }

    [TestCase("offers-only")]
    [TestCase("legacy-flat")]
    [TestCase("presence-v1")]
    [TestCase("marketplace-complete")]
    [TestCase("complete")]
    [TestCase("without-numeric")]
    [TestCase("without-value")]
    [TestCase("without-version")]
    public async Task ExistingIndexMigration_RequiresAllMetadataFields_AndPutsOnlyAdditiveMappings(string fixture)
    {
        var connection = new MappingConnection(fixture);
        var index = new ProductIndex(NullLogger<ProductIndex>.Instance, null!);
        var migration = typeof(ProductIndex).GetMethod("UpdateExistingIndexMappings",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)migration.Invoke(index, [Client(connection), CancellationToken.None])!;

        Assert.That(connection.Calls.Count(call => call.Method == "GET"), Is.EqualTo(fixture == "complete" ? 1 : 2));
        var puts = connection.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.That(puts, Has.Count.EqualTo(fixture == "complete" ? 0 : 1));
        if (fixture == "complete") return;

        Assert.That(puts[0].Path, Is.EqualTo("/ane-products-index-000001/_mapping"));
        using var body = JsonDocument.Parse(puts[0].Body!);
        Assert.That(body.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "properties" }));
        var properties = body.RootElement.GetProperty("properties");
        Assert.That(properties.EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "offersFound", "storeReferences", "marketplaceSourcesVersion", "marketplaceSources", "nativeEurMin", "nativeEurMinVersion" }));
        Assert.That(properties.GetProperty("offersFound").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(properties.GetProperty("marketplaceSourcesVersion").GetProperty("type").GetString(), Is.EqualTo("integer"));
        AssertMarketplaceSources(properties);
        AssertStoreReferences(properties);
        AssertNativeEurMinimum(properties);
    }

    [Test]
    public void NumericProjectionIsOptionalAtConstructorEndAndRoundTripsInSource()
    {
        var parameters = typeof(ProductDocument).GetConstructors().Single().GetParameters();
        Assert.That(parameters.TakeLast(2).Select(p => p.Name), Is.EqualTo(new[] { "NativeEurMin", "NativeEurMinVersion" }));
        Assert.That(parameters.TakeLast(2).All(p => p.IsOptional && p.DefaultValue == null), Is.True);
        Assert.That(parameters[^2].ParameterType, Is.EqualTo(typeof(double?)));
        Assert.That(parameters[^1].ParameterType, Is.EqualTo(typeof(int?)));
        Assert.That(typeof(ProductIndex).GetField("NativeEurMinVersion")?.GetRawConstantValue(), Is.EqualTo(1));
        var client = Client(new InMemoryConnection());
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("""
            {"seoId":"numeric","nativeEurMin":12.25,"nativeEurMinVersion":1}
            """));
        var product = client.SourceSerializer.Deserialize<ProductDocument>(input);
        using var output = new MemoryStream();
        client.SourceSerializer.Serialize(product, output);
        using var json = JsonDocument.Parse(output.ToArray());
        Assert.That(json.RootElement.GetProperty("nativeEurMin").GetDouble(), Is.EqualTo(12.25));
        Assert.That(json.RootElement.GetProperty("nativeEurMinVersion").GetInt32(), Is.EqualTo(1));
    }

    [TestCase("wrong-value-type")]
    [TestCase("wrong-version-type")]
    [TestCase("value-no-docvalues")]
    [TestCase("version-no-docvalues")]
    [TestCase("value-indexed")]
    public void ExistingNumericMappingMustBeCompatibleBeforeMigration(string fixture)
    {
        var connection = new MappingConnection(fixture);
        var index = new ProductIndex(NullLogger<ProductIndex>.Instance, null!);
        var migration = typeof(ProductIndex).GetMethod("UpdateExistingIndexMappings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await (Task)migration.Invoke(index, [Client(connection), CancellationToken.None])!);
        Assert.That(connection.Calls.Count(call => call.Method == "PUT"), Is.Zero);
    }

    [TestCase("get-401")]
    [TestCase("get-403")]
    [TestCase("put-401")]
    [TestCase("put-403")]
    [TestCase("verify-403")]
    [TestCase("unacknowledged")]
    [TestCase("unverified")]
    [TestCase("verify-wrong-type")]
    [TestCase("empty")]
    public void RequiredProjectionMappingCannotBeSkippedOnFailure(string fixture)
    {
        var connection = new MappingConnection(fixture);
        var index = new ProductIndex(NullLogger<ProductIndex>.Instance, null!);
        var migration = typeof(ProductIndex).GetMethod("UpdateExistingIndexMappings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await (Task)migration.Invoke(index, [Client(connection), CancellationToken.None])!);
    }

    [Test]
    public async Task ClientCannotEscapePendingSchemaOrCacheFailedInitializationAndCanRetry()
    {
        var connection = new MappingConnection("complete")
        {
            MappingGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            AllowMapping = false
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OPENSEARCH:URL"] = "http://localhost:9200", ["OPENSEARCH:USERNAME"] = "test", ["OPENSEARCH:PASSWORD"] = "test"
        }).Build();
        var index = new ProductIndex(NullLogger<ProductIndex>.Instance, new TestOpenSearch(config, connection));
        var first = index.Client();
        await connection.MappingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrent = index.Client();
        var escaped = concurrent.IsCompletedSuccessfully;
        connection.MappingGate.SetResult(true);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await concurrent);
        Assert.That(escaped, Is.False, "no client can be published while its typed schema is pending");
        connection.AllowMapping = true;
        var ready = await index.Client();
        var readCount = connection.MappingReads;
        Assert.That(await index.Client(), Is.SameAs(ready));
        Assert.That(connection.MappingReads, Is.EqualTo(readCount), "only a fully initialized client is cached");
        Assert.That(readCount, Is.GreaterThanOrEqualTo(3), "failed initialization must retry schema validation");
    }

    private sealed class TestOpenSearch(IConfiguration config, IConnection connection)
        : Coflnet.Ane.Opensearch.OpenSearch(config, NullLogger<Coflnet.Ane.Opensearch.OpenSearch>.Instance)
    {
        public override ConnectionSettings CreateConnectionSettings() =>
            new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200")), connection).DisablePing();
    }

    private static void AssertNativeEurMinimum(JsonElement properties)
    {
        var minimum = properties.GetProperty("nativeEurMin");
        Assert.That(minimum.GetProperty("type").GetString(), Is.EqualTo("double"));
        Assert.That(minimum.GetProperty("index").GetBoolean(), Is.False);
        Assert.That(minimum.GetProperty("doc_values").GetBoolean(), Is.True);
        var version = properties.GetProperty("nativeEurMinVersion");
        Assert.That(version.GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(version.GetProperty("doc_values").GetBoolean(), Is.True);
        Assert.That(ProductIndex.SourceExcludes, Does.Not.Contain("nativeEurMin"));
        Assert.That(ProductIndex.SourceExcludes, Does.Not.Contain("nativeEurMinVersion"));
    }

    private static void AssertMarketplaceSources(JsonElement properties)
    {
        var sources = properties.GetProperty("marketplaceSources");
        Assert.That(sources.GetProperty("type").GetString(), Is.EqualTo("nested"));
        var fields = sources.GetProperty("properties");
        Assert.That(fields.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "platform", "country", "sourceCurrency", "minPrice", "maxPrice" }));
        Assert.That(fields.GetProperty("platform").GetProperty("type").GetString(), Is.EqualTo("keyword"));
        Assert.That(fields.GetProperty("country").GetProperty("type").GetString(), Is.EqualTo("keyword"));
        Assert.That(fields.GetProperty("sourceCurrency").GetProperty("type").GetString(), Is.EqualTo("keyword"));
        Assert.That(fields.GetProperty("minPrice").GetProperty("type").GetString(), Is.EqualTo("double"));
        Assert.That(fields.GetProperty("maxPrice").GetProperty("type").GetString(), Is.EqualTo("double"));
    }

    private static Dictionary<string, object> StoreFields() => new[]
        { "knownProductId", "provider", "url", "merchantSku", "brand", "mpn", "gtin", "price", "currency", "availability", "condition", "observedAt", "expiresAt" }
        .ToDictionary(key => key, key => (object)new { type = key is "observedAt" or "expiresAt" ? "date" : key == "price" ? "double" : "keyword" });

    private static void AssertStoreReferences(JsonElement properties)
    {
        var references = properties.GetProperty("storeReferences");
        Assert.That(references.GetProperty("type").GetString(), Is.EqualTo("nested"));
        var fields = references.GetProperty("properties");
        Assert.That(fields.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(StoreFields().Keys));
        Assert.That(fields.GetProperty("price").GetProperty("type").GetString(), Is.EqualTo("double"));
        Assert.That(fields.GetProperty("expiresAt").GetProperty("type").GetString(), Is.EqualTo("date"));
        Assert.That(fields.GetProperty("observedAt").GetProperty("type").GetString(), Is.EqualTo("date"));
        Assert.That(fields.GetProperty("knownProductId").GetProperty("type").GetString(), Is.EqualTo("keyword"));
    }

    private static OpenSearchClient Client(IConnection connection) => new(
        new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200")), connection)
            .DisablePing().DisableDirectStreaming());

    private sealed class MappingConnection(string fixture) : InMemoryConnection
    {
        public List<(string Method, string Path, byte[]? Body)> Calls { get; } = [];
        public TaskCompletionSource<bool>? MappingGate { get; init; }
        public TaskCompletionSource<bool> MappingStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool AllowMapping { get; set; } = true;
        public int MappingReads;
        private bool applied;

        public override async Task<TResponse> RequestAsync<TResponse>(RequestData requestData,
            CancellationToken cancellationToken = default)
        {
            byte[]? body = null;
            if (requestData.PostData != null)
            {
                using var stream = new MemoryStream();
                requestData.PostData.Write(stream, requestData.ConnectionSettings);
                body = stream.ToArray();
            }
            var method = requestData.Method.ToString();
            lock (Calls) Calls.Add((method, requestData.Uri.AbsolutePath, body));
            var mapping = requestData.Uri.AbsolutePath.EndsWith("/_mapping", StringComparison.Ordinal);
            if (mapping && method == "GET")
            {
                Interlocked.Increment(ref MappingReads);
                MappingStarted.TrySetResult(true);
                if (MappingGate != null) await MappingGate.Task;
            }
            var denied = mapping && ((!AllowMapping && method == "GET")
                || (fixture.StartsWith("get-") && method == "GET")
                || (fixture.StartsWith("put-") && method == "PUT")
                || (fixture == "verify-403" && applied));
            if (denied)
                return await new InMemoryConnection(Encoding.UTF8.GetBytes("""{"error":{"type":"security_exception","reason":"SYNTHETIC denied"},"status":403}"""),
                    fixture.EndsWith("401") ? 401 : 403).RequestAsync<TResponse>(requestData, cancellationToken);
            var properties = "\"offersFound\":{\"type\":\"integer\"}";
            if (fixture == "legacy-flat")
                properties += ""","platforms":{"type":"keyword"},"platformsUpdatedAt":{"type":"date"}""";
            if (fixture != "offers-only")
                properties += ""","marketplaceSources":{"type":"nested","properties":{"platform":{"type":"keyword"},"country":{"type":"keyword"}}}""";
            if (fixture is not ("offers-only" or "legacy-flat"))
                properties += ""","marketplaceSourcesVersion":{"type":"integer"}""";
            if (fixture is not ("offers-only" or "legacy-flat" or "presence-v1"))
                properties = properties.Replace("\"country\":{\"type\":\"keyword\"}",
                    "\"country\":{\"type\":\"keyword\"},\"sourceCurrency\":{\"type\":\"keyword\"},\"minPrice\":{\"type\":\"double\"},\"maxPrice\":{\"type\":\"double\"}");
            if (fixture is not ("offers-only" or "legacy-flat" or "presence-v1" or "marketplace-complete"))
                properties += ",\"storeReferences\":{\"type\":\"nested\",\"properties\":" + JsonSerializer.Serialize(StoreFields()) + "}";
            if (fixture is "complete" or "wrong-value-type" or "wrong-version-type" or "value-no-docvalues" or "version-no-docvalues" or "value-indexed" or "without-value" or "without-version" || (applied && fixture != "unverified"))
            {
                var valueType = fixture == "wrong-value-type" || (fixture == "verify-wrong-type" && applied) ? "keyword" : "double";
                var versionType = fixture == "wrong-version-type" ? "long" : "integer";
                if (fixture != "without-value" || applied) properties += ",\"nativeEurMin\":" + JsonSerializer.Serialize(new { type = valueType, index = fixture == "value-indexed", doc_values = fixture != "value-no-docvalues" });
                if (fixture != "without-version" || applied) properties += ",\"nativeEurMinVersion\":" + JsonSerializer.Serialize(new { type = versionType, doc_values = fixture != "version-no-docvalues" });
            }
            var response = method == "GET" && mapping
                ? "{\"ane-products-index-000001\":{\"mappings\":{\"properties\":{\"name\":{\"type\":\"text\"}," + properties + "}}}}"
                : fixture == "unacknowledged" && mapping ? "{\"acknowledged\":false}" : "{\"acknowledged\":true}";
            if (fixture == "empty" && mapping && method == "GET") response = "{}";
            if (method == "GET" && requestData.Uri.AbsolutePath.StartsWith("/_plugins/")) response = "{\"_seq_no\":1,\"_primary_term\":1}";
            if (mapping && method == "PUT") applied = true;
            return await new InMemoryConnection(Encoding.UTF8.GetBytes(response), 200)
                .RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
