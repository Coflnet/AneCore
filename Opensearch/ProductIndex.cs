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
    public override string IndexName() => "ane_products";

    protected override string RetentionPolicyId() => "ane_product_retention_policy";

    protected override string IndexTemplateName() => "ane_product_index_template";

    protected override string BootstrapIndexName() => "ane-products-000001";

    protected override bool IsRolloverIndex() => false;

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>
        throw new NotImplementedException();

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        t => t
            .Settings(s => s
                .NumberOfShards(3)
                .NumberOfReplicas(0)
                .RefreshInterval(TimeSpan.FromSeconds(10))
            )
            .Map<ProductDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.SeoId))
                    .Keyword(k => k.Name(n => n.Category))
                    .Text(t => t.Name(n => n.Name))
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
                    .Text(t => t.Name(n => n.SampleTitles))
                    .Keyword(k => k.Name(n => n.ImageUrl))
                    .Keyword(k => k.Name(n => n.CanonicalSeoId))
                    .Keyword(k => k.Name(n => n.RelatedSeoIds))
                )
            );

    protected override PostData RetentionPolicy() =>
        throw new NotImplementedException();
}

public record ProductDocument(
    string? SeoId,
    string? Category,
    string? Name,
    string? NormalizedName,
    string? Brand,
    string? Model,
    string IdentifierType,
    string? Condition,
    double? AveragePrice,
    double? MedianPrice,
    double? MinPrice,
    double? MaxPrice,
    int? ListingCount,
    DateTime CreatedAt,
    DateTime LastUpdated,
    List<string> SampleTitles,
    string? ImageUrl,
    string? CanonicalSeoId,
    List<string> RelatedSeoIds
);