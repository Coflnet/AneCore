using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

/// <summary>
/// Small inspection index for sampled listings of the <c>next</c> verticals (see Categories/CategoryScope.json).
/// Samples are not extracted or grouped into products; they are kept for 14 days so the next verticals can be
/// evaluated without counting against the product catalogue.
/// </summary>
public class ListingSampleIndex(
    ILogger<ListingSampleIndex> logger,
    OpenSearch opBaseClient)
    : OpenSearchIndexBase(opBaseClient, logger)
{
    public override string IndexName() => "ane_listing_samples";

    protected override string RetentionPolicyId() => "ane_listing_samples_retention_policy";

    protected override string IndexTemplateName() => "ane_listing_samples_index_template";

    protected override string BootstrapIndexName() => "ane-listing-samples-000001";

    protected override bool IsRolloverIndex() => true;

    protected override Time RefreshInterval => "30s";

    private IPromise<IIndexSettings> IndexSettings(IndexSettingsDescriptor s) => s
        .Setting("plugins.index_state_management.rollover_alias", IndexName())
        .NumberOfShards(1)
        .NumberOfReplicas(0)
        .RefreshInterval(RefreshInterval)
        .Setting("index.codec", "best_compression");

    public static ITypeMapping Mapping(TypeMappingDescriptor<ListingSampleDocument> m) => m
        .Dynamic(false)
        .Properties(p => p
            .Keyword(k => k.Name(n => n.Id).DocValues(false))
            .Keyword(k => k.Name(n => n.Platform))
            .Keyword(k => k.Name(n => n.Vertical))
            .Text(t => t.Name(n => n.Title).Norms(false).IndexOptions(IndexOptions.Freqs))
            .Text(t => t.Name(n => n.DescriptionExcerpt).Index(false))
            .Number(n => n.Name(d => d.Price).Type(NumberType.Double))
            .Keyword(k => k.Name(n => n.Currency).Index(false).DocValues(false))
            .Keyword(k => k.Name(n => n.Category))
            .Keyword(k => k.Name(n => n.Country))
            .Keyword(k => k.Name(n => n.ImageUrl).Index(false).DocValues(false))
            .Keyword(k => k.Name(n => n.SellerHash).DocValues(false))
            .Date(d => d.Name(n => n.FoundAt))
        );

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>
        t => t
            .IndexPatterns(IndexPattern())
            .Settings(IndexSettings)
            .Map<ListingSampleDocument>(Mapping);

    protected override CreateIndexDescriptor ConfigureNewIndex(CreateIndexDescriptor descriptor) =>
        descriptor.Settings(IndexSettings).Map<ListingSampleDocument>(Mapping);

    protected override RolloverIndexDescriptor ConfigureRollover(RolloverIndexDescriptor descriptor) =>
        descriptor.Settings(IndexSettings).Map<ListingSampleDocument>(Mapping);

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        throw new NotImplementedException();

    protected override PostData RetentionPolicy() =>
        PostData.Serializable(new
        {
            policy = new
            {
                description = "Next-vertical listing samples retention policy",
                default_state = "hot",
                states = new object[]
                {
                    new
                    {
                        name = "hot",
                        actions = new[] { new { rollover = new { min_index_age = "1d" } } },
                        transitions = new[]
                        {
                            new
                            {
                                state_name = "delete",
                                conditions = new { min_rollover_age = $"{ListingRetention.Days}d" }
                            }
                        }
                    },
                    new
                    {
                        name = "delete",
                        transitions = Array.Empty<object>(),
                        actions = new[] { new { delete = new { } } }
                    }
                },
                ism_template = new
                {
                    index_patterns = new[] { IndexPattern() },
                    priority = 100
                }
            }
        });
}

/// <summary>Minimal document for a sampled next-vertical listing.</summary>
/// <param name="Vertical">Key of the next vertical (watches, lego, ...)</param>
/// <param name="DescriptionExcerpt">Plain-text excerpt, stored only (not indexed)</param>
/// <param name="ImageUrl">First image only</param>
/// <param name="SellerHash">Keyed seller hash, only for blacklist cleanup</param>
public record ListingSampleDocument(
    string Id,
    string Platform,
    string? Vertical,
    string? Title,
    string? DescriptionExcerpt,
    double? Price,
    string? Currency,
    string? Category,
    string? Country,
    string? ImageUrl,
    string? SellerHash,
    DateTime FoundAt
);
