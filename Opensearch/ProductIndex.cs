using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

public class ProductIndex(
    ILogger<ProductIndex> logger,
    OpenSearch opBaseClient)
    : OpenSearchIndexBase(opBaseClient, logger)
{
    public override string IndexName() => "ane_products_index";

    protected override string RetentionPolicyId() => "ane_products_retention_policy";

    protected override string IndexTemplateName() => "ane_products_index_template";

    protected override string BootstrapIndexName() => "ane-products-index-000001";

    protected override bool IsRolloverIndex() => true;

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        throw new NotImplementedException();

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>

        t => t
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(1)
                .RefreshInterval(TimeSpan.FromSeconds(10))
                .Setting("plugins.index_state_management.rollover_alias", IndexName())
                .Setting("index.knn", true)
            )
            .Map<ProductDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.SeoId))
                    .Keyword(k => k.Name(n => n.Categories))
                    .Text(k => k.Name(n => n.Name))
                    .Text(k => k.Name(n => n.NormalizedName))
                    .Keyword(k => k.Name(n => n.Brand))
                    .Keyword(k => k.Name(n => n.Model))
                    .Keyword(k => k.Name(n => n.IdentifierType))
                    .Keyword(k => k.Name(n => n.Condition))
                    .Number(k => k.Name(n => n.AveragePrice).Type(NumberType.Double))
                    .Number(k => k.Name(n => n.MedianPrice).Type(NumberType.Double))
                    .Number(k => k.Name(n => n.MinPrice).Type(NumberType.Double))
                    .Number(k => k.Name(n => n.MaxPrice).Type(NumberType.Double))
                    .Number(k => k.Name(n => n.ListingCount).Type(NumberType.Integer))
                    .Date(d => d.Name(n => n.CreatedAt))
                    .Date(d => d.Name(n => n.LastUpdated))
                    .Object<ProductAttribute>(o => o
                        .Name(n2 => n2.Attributes)
                        .Properties(p2 => p2
                            .Keyword(k2 => k2.Name(n2 => n2.Key))
                            .Keyword(k2 => k2.Name(n2 => n2.Value))
                        )
                    )
                    .Text(k => k.Name(n => n.SampleTitles))
                    .Keyword(k => k.Name(n => n.ImageUrl))
                    .Keyword(k => k.Name(n => n.CanonicalSeoId))
                    .Keyword(k => k.Name(n => n.RelatedSeoIds))
                )
            );

    protected override PostData RetentionPolicy() =>
        PostData.Serializable(new
        {
            policy = new
            {
                description = "Product index retention policy",
                default_state = "hot",
                states = new object[]
                {
                    new
                    {
                        name = "hot",
                        actions = new[]
                        {
                            new
                            {
                                rollover = new
                                {
                                    min_size = "20gb",
                                }
                            }
                        },
                        transitions = new[]
                        {
                            new
                            {
                                state_name = "warm",
                                conditions = new { min_rollover_age = "7d" }
                            }
                        }
                    },
                    new
                    {
                        name = "warm",
                        transitions = Array.Empty<object>(),
                        actions = new object[]
                        {
                            new { read_only = new { } },
                            new { force_merge = new { max_num_segments = 1 } }
                        }
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

public record ProductDocument(
    string? SeoId,
    List<string>? Categories,
    string? Name,
    string? NormalizedName,
    string? Brand,
    string? Model,
    string? IdentifierType,
    string? Condition,
    double? AveragePrice,
    double? MedianPrice,
    double? MinPrice,
    double? MaxPrice,
    int? ListingCount,
    DateTime CreatedAt,
    DateTime LastUpdated,
    List<ProductAttribute> Attributes,
    List<string> SampleTitles,
    string? ImageUrl,
    string? CanonicalSeoId,
    List<string> RelatedSeoIds
);

public record ProductAttribute(
    string Key,
    string Value
);