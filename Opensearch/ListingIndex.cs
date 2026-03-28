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

    protected override string IndexTemplateName() => "ane_listing_index_template";

    protected override string BootstrapIndexName() => "ane-listings-000001";

    protected override bool IsRolloverIndex() => true;

    public async Task<IReadOnlyCollection<ListingDocument>> SearchListings(
        string query,
        double? minPrice = null,
        double? maxPrice = null,
        double? latitude = null,
        double? longitude = null,
        string? distance = null,
        int limit = 10,
        List<Platform>? platforms = null,
        CancellationToken cancellationToken = default)
    {
        var client = await Client(cancellationToken);

        List<Func<QueryContainerDescriptor<ListingDocument>, QueryContainer>> matchQuery =
        [
            m => m.Match(t => t.Field(f => f.Title).Query(query).Operator(Operator.And))
        ];

        minPrice ??= 0;
        if (maxPrice != null)
            matchQuery.Add(m => m.Range(t => t.Field(f => f.Price)
                .GreaterThanOrEquals(minPrice)
                .LessThanOrEquals(maxPrice)));

        if (platforms is { Count: > 0 })
        {
            var platformStrings = platforms.Select(p => p.ToString()).ToList();
            matchQuery.Add(m => m.Terms(t => t.Field(f => f.Platform).Terms(platformStrings)));
        }

        Func<QueryContainerDescriptor<ListingDocument>, QueryContainer> locationQuery =
            f => latitude == null || longitude == null
                ? f.MatchAll()
                : f.GeoDistance(g => g
                    .Field(f => f.Location)
                    .DistanceType(GeoDistanceType.Arc)
                    .Location(latitude.Value, longitude.Value)
                    .Distance(distance));

        var searchResponse = await client.SearchAsync<ListingDocument>(s =>
                s.Query(q => q
                        .Bool(b => b
                            .Must(matchQuery)
                            .Filter(locationQuery)))
                    .Size(limit)
                    .Sort(s => s.Descending(d => d.FoundAt)),
            cancellationToken);

        return searchResponse.Documents;
    }

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>
        t => t
            .IndexPatterns(IndexPattern())
            .Settings(s => s
                .Setting("plugins.index_state_management.rollover_alias", IndexName())
                .NumberOfShards(2)
                .NumberOfReplicas(0)
                .RefreshInterval(TimeSpan.FromSeconds(20))
                .Setting("index.codec", "best_compression")
            )
            .Map<ListingDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.Id))
                    .Text(t => t.Name(n => n.Title))
                    .Text(t => t.Name(n => n.DescriptionShort))
                    .Number(k => k.Name(n => n.Price).Type(NumberType.Double))
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
                                conditions = new { min_rollover_age = "14d" }
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
    string Platform
);

public record ListingAttribute(
    string Name,
    string Value
);

