using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

/// <summary>
/// Visual-similarity reference index for clothing listing photos embedded with the fashion-clip model
/// (see Embeddings/ListingImageEmbedding.cs for the Cassandra-side copy of the same vectors). A single,
/// non-rollover index: one document per embedded photo.
///
/// The k-NN plugin is optional infrastructure - if the cluster does not have it, creating this index's
/// <c>knn_vector</c> mapping fails. <see cref="OpenSearchVisualReferenceStore"/> catches that during its
/// own initialization, logs a warning, and marks itself unavailable instead of throwing, so a missing
/// plugin never fails service startup; visual clustering is then simply skipped.
/// </summary>
public class ClothingVisualIndex(
    ILogger<ClothingVisualIndex> logger,
    OpenSearch opBaseClient)
    : OpenSearchIndexBase(opBaseClient, logger)
{
    public override string IndexName() => "ane_clothing_visual_v1";

    protected override bool IsRolloverIndex() => false;

    protected override Time RefreshInterval => "1s";

    /// <summary>Builds this index's document id for one listing photo: <c>{platform}:{listingId}:{imageIndex}</c>.</summary>
    public static string BuildDocumentId(string platform, string listingId, int imageIndex) =>
        $"{platform}:{listingId}:{imageIndex}";

    private IPromise<IIndexSettings> IndexSettings(IndexSettingsDescriptor s) => s
        .Setting("index.knn", true)
        .NumberOfShards(1)
        .NumberOfReplicas(1)
        .RefreshInterval(RefreshInterval);

    public static ITypeMapping Mapping(TypeMappingDescriptor<ClothingVisualDocument> m) => m
        .Properties(p => p
            .Keyword(k => k.Name(n => n.GroupKey))
            .Keyword(k => k.Name(n => n.ClusterSeoId))
            .Boolean(b => b.Name(n => n.Seed))
            .Keyword(k => k.Name(n => n.ListingId))
            .Keyword(k => k.Name(n => n.Platform))
            .Keyword(k => k.Name(n => n.ImageUrl).Index(false))
            .Keyword(k => k.Name(n => n.ColorFamily))
            .Keyword(k => k.Name(n => n.Pattern))
            .Keyword(k => k.Name(n => n.Gender))
            .Keyword(k => k.Name(n => n.ModelId))
            .Date(d => d.Name(n => n.CreatedAt))
            .KnnVector(k => k
                .Name(n => n.Embedding)
                .Dimension(EmbeddingDimensions)
                .Method(mm => mm
                    .Name("hnsw")
                    .Engine("lucene")
                    .SpaceType("cosinesimil")
                    .Parameters(pp => pp
                        .Parameter("m", 16)
                        .Parameter("ef_construction", 128))))
        );

    /// <summary>Matches <c>Coflnet.Ane.Embeddings.EmbeddingBlobCodec.Dimensions</c> (fashion-clip output size).</summary>
    public const int EmbeddingDimensions = 512;

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        c => c.Settings(IndexSettings).Map<ClothingVisualDocument>(Mapping);

    // Rollover-only machinery - this is a single, non-rollover index (see IsRolloverIndex above) so these
    // are never invoked; implemented to satisfy the abstract base like the rollover indices do the reverse
    // (e.g. ProductIndex.IndexFunc()).
    protected override string RetentionPolicyId() => throw new NotImplementedException();
    protected override string IndexTemplateName() => throw new NotImplementedException();
    protected override string BootstrapIndexName() => throw new NotImplementedException();
    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() => throw new NotImplementedException();
    protected override PostData RetentionPolicy() => throw new NotImplementedException();
}

/// <summary>
/// One clothing listing photo in the visual reference index. Document id is
/// <see cref="ClothingVisualIndex.BuildDocumentId"/> (<c>platform:listingId:imageIndex</c>) - the image
/// index is therefore recoverable from the id and is not duplicated as a field here.
/// </summary>
/// <param name="Seed">True for curated/reference photos seeded into the index, false for regular scraped listings.</param>
/// <param name="Embedding">512-dim, L2-normalised fashion-clip vector.</param>
public record ClothingVisualDocument(
    string ListingId,
    string Platform,
    string GroupKey,
    string? ClusterSeoId,
    bool Seed,
    string? ImageUrl,
    string? ColorFamily,
    string? Pattern,
    string? Gender,
    string ModelId,
    DateTime CreatedAt,
    float[] Embedding
);
