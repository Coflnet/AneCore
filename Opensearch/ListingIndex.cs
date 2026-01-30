using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

public class ListingIndex(
    ILogger<ListingIndex> logger,
    OpenSearch opBaseClient)
    : OpenSearchIndexBase(opBaseClient, logger)
{
    public override string IndexName() => "ane_listings";

    protected override string RetentionPolicyId() => "ane_listing_retention_policy";

    protected override string IndexTemplateName() => "ane-listing_index_template";

    protected override string BootstrapIndexName() => "ane-listings-000001";

    protected override bool IsRolloverIndex() => true;

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>
        t => t
            .Settings(s => s
                .NumberOfShards(3)
                .NumberOfReplicas(0)
                .RefreshInterval(TimeSpan.FromSeconds(10))
            )
            .Map<ListingDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.Id))
                    .Text(t => t.Name(n => n.Title))
                    .Text(t => t.Name(n => n.Description))
                    .Text(t => t.Name(n => n.DescriptionShort))
                    .Number(k => k.Name(n => n.Price).Type(NumberType.Double))
                    .Keyword(k => k.Name(n => n.ImageUrls))
                    .Keyword(k => k.Name(n => n.Currency))
                    .Keyword(k => k.Name(n => n.Contact))
                    .Keyword(k => k.Name(n => n.UserId))
                    .GeoPoint(g => g.Name(n => n.Location))
                    .Keyword(k => k.Name(n => n.Shipping))
                    .Keyword(k => k.Name(n => n.PriceFlags))
                    .Object<ListingAttribute>(n => n
                        .Name(n => n.Attributes)
                        .Properties(p2 => p2
                            .Keyword(k2 => k2.Name(n2 => n2.Name))
                            .Keyword(k2 => k2.Name(n2 => n2.Value))
                        )
                    )
                    .Date(d => d.Name(n => n.FoundAt))
                    .Date(d => d.Name(n => n.CreatedAt))
                    .Date(d => d.Name(n => n.SoldBefore))
                    .Boolean(b => b.Name(n => n.Commercial))
                    .Object<ListingMetadata>(n => n
                        .Name(n => n.Metadata)
                        .Properties(p2 => p2
                            .Keyword(k2 => k2.Name(n2 => n2.Name))
                            .Keyword(k2 => k2.Name(n2 => n2.Value))
                        )
                    )
                    .Keyword(k => k.Name(n => n.Platform))
                )
            );

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        throw new NotImplementedException();

    protected override PostData RetentionPolicy() =>
        PostData.Serializable(new
        {
            policy = new
            {
                description = "Listings retention policy",
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
                                    min_index_age = "1d"
                                }
                            }
                        },
                        transitions = new[]
                        {
                            new
                            {
                                state_name = "delete",
                                conditions = new { min_rollover_age = "7d" }
                            }
                        }
                    },
                    new
                    {
                        name = "delete",
                        transitions = Array.Empty<object>(),
                        actions = new[]
                        {
                            new { delete = new { } }
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

public record ListingDocument(
    string Id,
    string? Title,
    string? DescriptionShort,
    string? Description,
    List<string>? ImageUrls,
    double? Price,
    string? Currency,
    string? Contact,
    string? UserId,
    GeoLocation? Location,
    string? Shipping,
    List<string> PriceFlags,
    List<ListingAttribute> Attributes,
    DateTime FoundAt,
    DateTime? CreatedAt,
    DateTime? SoldBefore,
    bool? Commercial,
    List<ListingMetadata> Metadata,
    string Platform
);

public record ListingAttribute(
    string Name,
    string Value
);

public record ListingMetadata(
    string Name,
    string Value
);