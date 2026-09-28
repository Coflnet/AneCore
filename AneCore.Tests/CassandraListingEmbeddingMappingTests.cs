using System.Reflection;
using Coflnet.Ane;
using Coflnet.Ane.Embeddings;

namespace AneCore.Tests;

[TestFixture]
public class CassandraListingEmbeddingMappingTests
{
    [Test]
    public void KeyColumns_AreBackedByPrimitiveProperties()
    {
        // Regression: the partition key (listing_id, platform) was mapped on the Platform enum.
        // Every read threw "Unknown Cassandra target type for CLR type Coflnet.Ane.Platform" when the
        // driver computed the routing key, so visual grouping fell back for every listing.
        var definition = CassandraListingEmbeddingStore.BuildMapping().Get<ListingImageEmbedding>();
        var keyColumns = definition.PartitionKeys
            .Concat(definition.ClusteringKeys.Select(k => k.Item1))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.That(keyColumns, Is.EquivalentTo(new[] { "listing_id", "platform", "image_index" }));
        foreach (var property in typeof(ListingImageEmbedding).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var column = definition.GetColumnDefinition(property);
            if (column.Ignore || !keyColumns.Contains(column.ColumnName))
                continue;
            Assert.That(property.PropertyType.IsEnum, Is.False, $"key column {column.ColumnName} must not be an enum");
        }
    }

    [Test]
    public void PlatformEnum_IsNotAColumn_AndRoundTripsThroughTheIntColumn()
    {
        var definition = CassandraListingEmbeddingStore.BuildMapping().Get<ListingImageEmbedding>();
        var enumProperty = typeof(ListingImageEmbedding).GetProperty(nameof(ListingImageEmbedding.Platform))!;
        Assert.That(definition.GetColumnDefinition(enumProperty).Ignore, Is.True);

        var embedding = new ListingImageEmbedding { Platform = Platform.Vinted };
        Assert.That(embedding.PlatformValue, Is.EqualTo((int)Platform.Vinted));
        Assert.That(new ListingImageEmbedding { PlatformValue = (int)Platform.Kleinanzeigen }.Platform,
            Is.EqualTo(Platform.Kleinanzeigen));
    }
}
