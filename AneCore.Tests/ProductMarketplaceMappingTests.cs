using System.Reflection;
using System.Text;
using System.Text.Json;
using Coflnet.Ane.Opensearch;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task ExistingIndexMigration_RequiresAllMetadataFields_AndPutsOnlyAdditiveMappings(string fixture)
    {
        var connection = new MappingConnection(fixture);
        var index = new ProductIndex(NullLogger<ProductIndex>.Instance, null!);
        var migration = typeof(ProductIndex).GetMethod("UpdateExistingIndexMappings",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)migration.Invoke(index, [Client(connection), CancellationToken.None])!;

        Assert.That(connection.Calls.Count(call => call.Method == "GET"), Is.EqualTo(1));
        var puts = connection.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.That(puts, Has.Count.EqualTo(fixture == "complete" ? 0 : 1));
        if (fixture == "complete") return;

        Assert.That(puts[0].Path, Is.EqualTo("/ane-products-index-000001/_mapping"));
        using var body = JsonDocument.Parse(puts[0].Body!);
        Assert.That(body.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "properties" }));
        var properties = body.RootElement.GetProperty("properties");
        Assert.That(properties.EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "offersFound", "storeReferences", "marketplaceSourcesVersion", "marketplaceSources" }));
        Assert.That(properties.GetProperty("offersFound").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(properties.GetProperty("marketplaceSourcesVersion").GetProperty("type").GetString(), Is.EqualTo("integer"));
        AssertMarketplaceSources(properties);
        AssertStoreReferences(properties);
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

        public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData,
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
            Calls.Add((method, requestData.Uri.AbsolutePath, body));
            var properties = "\"offersFound\":{\"type\":\"integer\"}";
            if (fixture == "legacy-flat")
                properties += ""","platforms":{"type":"keyword"},"platformsUpdatedAt":{"type":"date"}""";
            if (fixture != "offers-only")
                properties += ""","marketplaceSources":{"type":"nested","properties":{"platform":{"type":"keyword"},"country":{"type":"keyword"}}}""";
            if (fixture is "presence-v1" or "complete" or "marketplace-complete")
                properties += ""","marketplaceSourcesVersion":{"type":"integer"}""";
            if (fixture is "complete" or "marketplace-complete")
                properties = properties.Replace("\"country\":{\"type\":\"keyword\"}",
                    "\"country\":{\"type\":\"keyword\"},\"sourceCurrency\":{\"type\":\"keyword\"},\"minPrice\":{\"type\":\"double\"},\"maxPrice\":{\"type\":\"double\"}");
            if (fixture == "complete")
                properties += ",\"storeReferences\":{\"type\":\"nested\",\"properties\":" + JsonSerializer.Serialize(StoreFields()) + "}";
            var response = method == "GET"
                ? "{\"ane-products-index-000001\":{\"mappings\":{\"properties\":{\"name\":{\"type\":\"text\"}," + properties + "}}}}"
                : "{\"acknowledged\":true}";
            return new InMemoryConnection(Encoding.UTF8.GetBytes(response), 200)
                .RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
