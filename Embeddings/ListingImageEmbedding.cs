namespace Coflnet.Ane.Embeddings;

/// <summary>
/// One fashion-clip embedding for a single photo of a clothing listing. A listing usually has several
/// photos, each embedded separately (<see cref="ImageIndex"/> 0, 1, 2, ...). See
/// <see cref="EmbeddingBlobCodec"/> for the <see cref="Embedding"/> blob layout and
/// <see cref="IListingEmbeddingStore"/> for the storage API.
/// </summary>
public class ListingImageEmbedding
{
    /// <summary>Together with <see cref="Platform"/>, the partition key - all photos of one listing sit in the same partition.</summary>
    public string ListingId { get; set; } = "";

    /// <summary>Not a column: see <see cref="PlatformValue"/>.</summary>
    public Platform Platform
    {
        get => (Platform)PlatformValue;
        set => PlatformValue = (int)value;
    }

    /// <summary>
    /// Column <c>platform</c>, part of the partition key. Kept as <c>int</c> because the Cassandra driver
    /// serializes partition key values unconverted when it computes the routing key and throws
    /// "Unknown Cassandra target type" for an enum.
    /// </summary>
    public int PlatformValue { get; set; }

    /// <summary>Clustering key: 0-based position of the photo within the listing's image list.</summary>
    public int ImageIndex { get; set; }

    public string ImageUrl { get; set; } = "";

    /// <summary>Identifies the embedding model/version (e.g. "fashion-clip-v1") so a future model change can be detected and the photo re-embedded.</summary>
    public string ModelId { get; set; } = "";

    /// <summary>512-dim, L2-normalised float32 vector, little-endian - see <see cref="EmbeddingBlobCodec"/>.</summary>
    public byte[] Embedding { get; set; } = [];

    /// <summary>Grouping key used to scope visual neighbour search (e.g. category+gender) so unrelated clothing never matches.</summary>
    public string GroupKey { get; set; } = "";

    /// <summary>SEO id of the visual cluster this photo was assigned to, once clustering has run; null before that.</summary>
    public string? ClusterSeoId { get; set; }

    public DateTime CreatedAt { get; set; }
}
