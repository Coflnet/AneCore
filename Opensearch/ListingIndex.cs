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

    /// <summary>
    /// Fields kept in <c>_source</c>. Readers only need the id/platform to load the listing from Scylla
    /// (FilterMatcher backtests, blacklist cleanup) and the existence check only counts; everything else is
    /// indexed for search but not stored. Changing this is non-additive: it applies to the next rolled index.
    /// </summary>
    public static readonly string[] SourceFields = ["id", "platform", "foundAt"];

    /// <summary>Listing attribute keys that are indexed (keyword); other attributes are not sent.</summary>
    public static readonly IReadOnlySet<string> IndexedAttributeKeys =
        new HashSet<string>(["brand", "model", "size", "condition", "color"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum characters of an indexed attribute value.</summary>
    public const int MaxAttributeValueLength = 64;

    protected override Time RefreshInterval => "30s";

    private IPromise<IIndexSettings> IndexSettings(IndexSettingsDescriptor s) => s
        .Setting("plugins.index_state_management.rollover_alias", IndexName())
        .NumberOfShards(1)
        .NumberOfReplicas(0)
        .RefreshInterval(RefreshInterval)
        .Setting("index.codec", "best_compression");

    /// <summary>
    /// Minimal listing mapping: only fields that are queried are indexed (title, id, platform, price, location,
    /// foundAt, seller identifiers for blacklist cleanup); doc values only where sorting/geo needs them.
    /// Unmapped fields are ignored (dynamic false).
    /// </summary>
    public static ITypeMapping Mapping(TypeMappingDescriptor<ListingDocument> m) => m
        .Dynamic(false)
        .SourceField(s => s.Includes(SourceFields))
        .Properties(p => p
            .Keyword(k => k.Name(n => n.Id).DocValues(false))
            .Text(t => t.Name(n => n.Title).Norms(false).IndexOptions(IndexOptions.Freqs))
            .Text(t => t.Name(n => n.DescriptionShort).Index(false))
            .Number(k => k.Name(n => n.Price).Type(NumberType.Double).DocValues(false))
            .Keyword(k => k.Name(n => n.Currency).Index(false).DocValues(false))
            .Keyword(k => k.Name(n => n.Contact).DocValues(false))
            .Keyword(k => k.Name(n => n.UserId).DocValues(false))
            .Keyword(k => k.Name(n => n.SellerHash).DocValues(false))
            .GeoPoint(g => g.Name(n => n.Location))
            .Keyword(k => k.Name(n => n.Shipping).Index(false).DocValues(false))
            .Keyword(k => k.Name(n => n.PriceFlags).Index(false).DocValues(false))
            .Object<ListingAttribute>(n => n
                .Name(n => n.Attributes)
                .Properties(p2 => p2
                    .Keyword(k2 => k2.Name(n2 => n2.Name).DocValues(false))
                    .Keyword(k2 => k2.Name(n2 => n2.Value).DocValues(false))
                )
            )
            .Date(d => d.Name(n => n.FoundAt))
            .Date(d => d.Name(n => n.CreatedAt).Index(false).DocValues(false))
            .Date(d => d.Name(n => n.SoldBefore).Index(false).DocValues(false))
            .Boolean(b => b.Name(n => n.Commercial).Index(false).DocValues(false))
            .Keyword(k => k.Name(n => n.Platform).DocValues(false))
        );

    protected override Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc() =>
        t => t
            .IndexPatterns(IndexPattern())
            .Settings(IndexSettings)
            .Map<ListingDocument>(Mapping);

    protected override CreateIndexDescriptor ConfigureNewIndex(CreateIndexDescriptor descriptor) =>
        descriptor.Settings(IndexSettings).Map<ListingDocument>(Mapping);

    protected override RolloverIndexDescriptor ConfigureRollover(RolloverIndexDescriptor descriptor) =>
        descriptor.Settings(IndexSettings).Map<ListingDocument>(Mapping);

    protected override Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc() =>
        throw new NotImplementedException();

    /// <summary>
    /// Additive migrations only (the existing backing indices keep their old mapping until they expire):
    /// adds <c>sellerHash</c> to indices that predate it.
    /// </summary>
    protected override async Task UpdateExistingIndexMappings(
        OpenSearchClient client,
        CancellationToken stoppingToken)
    {
        var mappings = await client.Indices.GetMappingAsync<ListingDocument>(m => m.Index(IndexPattern()), stoppingToken);
        if (!mappings.IsValid)
        {
            if (IsPermissionDenied(mappings))
            {
                WarnMissingPermissionOnce("get mapping", mappings.ServerError?.Error?.Reason);
                return;
            }
            throw new InvalidOperationException(
                $"Failed to read listing index mappings: {mappings.DebugInformation}");
        }

        foreach (var (index, state) in mappings.Indices)
        {
            if (state.Mappings?.Properties?.ContainsKey("sellerHash") == true)
                continue;
            var response = await client.Indices.PutMappingAsync<ListingDocument>(m => m
                .Index(index)
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.SellerHash).DocValues(false))),
                stoppingToken);
            if (response.IsValid)
                continue;
            if (IsPermissionDenied(response))
            {
                WarnMissingPermissionOnce("put mapping", response.ServerError?.Error?.Reason);
                return;
            }
            throw new InvalidOperationException(
                $"Failed to add seller hash mapping to listing index {index.Name}: {response.DebugInformation}");
        }
    }

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
                                conditions = new { min_rollover_age = $"{ListingRetention.Days}d" }
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
    string? SellerHash,
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
