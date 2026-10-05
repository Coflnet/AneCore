using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Parts;

/// <summary>
/// Cassandra <see cref="IPartStore"/>: seven small tables, all scalar columns plus <c>list&lt;text&gt;</c>, created additively with <c>CreateIfNotExistsAsync</c> (nothing existing is altered)
/// by <see cref="InitializeAsync"/>, which the writing service calls once at startup; a reading service (AneApi) passes <c>createTables: false</c> and never issues DDL. Every row is written
/// with a TTL (<see cref="TtlSeconds"/>, default 120 days) so a page that stops being indexed disappears on its own; the recorder renews it by rewriting. Platforms are stored as int
/// (the driver cannot map enums).
/// </summary>
public sealed class CassandraPartStore : IPartStore
{
    public const int DefaultTtlSeconds = 120 * 24 * 3600;

    private readonly ISession session;
    private readonly bool createTables;
    private readonly Table<PartPageState> pages;
    private readonly Table<PartMeasurement> measurements;
    private readonly Table<PartSignatureRow> signatures;
    private readonly Table<PartNumberRow> partNumbers;
    private readonly Table<PartFitRow> fitsByMachine;
    private readonly Table<PartFitByPartRow> fitsByPart;
    private readonly Table<PartSameEdge> edges;

    public int TtlSeconds { get; }

    public CassandraPartStore(ISession session, bool createTables = true, int ttlSeconds = DefaultTtlSeconds)
    {
        this.session = session;
        this.createTables = createTables;
        TtlSeconds = Math.Max(3600, ttlSeconds);
        var mapping = BuildMapping();
        pages = new Table<PartPageState>(session, mapping);
        measurements = new Table<PartMeasurement>(session, mapping);
        signatures = new Table<PartSignatureRow>(session, mapping);
        partNumbers = new Table<PartNumberRow>(session, mapping);
        fitsByMachine = new Table<PartFitRow>(session, mapping);
        fitsByPart = new Table<PartFitByPartRow>(session, mapping);
        edges = new Table<PartSameEdge>(session, mapping);
    }

    /// <summary>The reverse fit table maps the same row class with another key, so it has a class of its own.</summary>
    public class PartFitByPartRow : PartFitRow
    {
        public static PartFitByPartRow From(PartFitRow fit) => new()
        {
            MachineKey = fit.MachineKey, SeoId = fit.SeoId, MachineName = fit.MachineName, PartClass = fit.PartClass, PartName = fit.PartName,
            Source = fit.Source, Confidence = fit.Confidence, Evidence = fit.Evidence, UpdatedAt = fit.UpdatedAt,
        };
    }

    /// <summary>Table mapping, separate from the constructor so tests can inspect it without a session.</summary>
    public static MappingConfiguration BuildMapping() => new MappingConfiguration()
        .Define(new Map<PartPageState>()
            .TableName("part_pages")
            .PartitionKey(p => p.SeoId)
            .Column(p => p.SeoId, cm => cm.WithName("seo_id"))
            .Column(p => p.PartClass, cm => cm.WithName("part_class"))
            .Column(p => p.PartKey, cm => cm.WithName("part_key"))
            .Column(p => p.Signature, cm => cm.WithName("signature"))
            .Column(p => p.PartNumberKeys, cm => cm.WithName("part_number_keys").WithDbType<List<string>>())
            .Column(p => p.FitKeys, cm => cm.WithName("fit_keys").WithDbType<List<string>>())
            .Column(p => p.EdgeIds, cm => cm.WithName("edge_ids").WithDbType<List<string>>())
            .Column(p => p.UpdatedAt, cm => cm.WithName("updated_at")))
        .Define(new Map<PartMeasurement>()
            .TableName("part_measurements")
            .PartitionKey(m => m.SeoId)
            .ClusteringKey(m => m.AttributeKey)
            .ClusteringKey(m => m.Source)
            .ClusteringKey(m => m.ListingKey)
            .Column(m => m.SeoId, cm => cm.WithName("seo_id"))
            .Column(m => m.AttributeKey, cm => cm.WithName("attribute_key"))
            .Column(m => m.Source, cm => cm.WithName("source"))
            .Column(m => m.ListingKey, cm => cm.WithName("listing_key"))
            .Column(m => m.Value, cm => cm.WithName("value"))
            .Column(m => m.Numeric, cm => cm.WithName("numeric"))
            .Column(m => m.Unit, cm => cm.WithName("unit"))
            .Column(m => m.Confidence, cm => cm.WithName("confidence"))
            .Column(m => m.Evidence, cm => cm.WithName("evidence"))
            .Column(m => m.ListingPlatform, cm => cm.WithName("listing_platform"))
            .Column(m => m.ListingId, cm => cm.WithName("listing_id"))
            .Column(m => m.UpdatedAt, cm => cm.WithName("updated_at")))
        .Define(new Map<PartSignatureRow>()
            .TableName("parts_by_signature")
            .PartitionKey(s => s.PartClass, s => s.PartKey)
            .ClusteringKey(s => s.SeoId)
            .Column(s => s.PartClass, cm => cm.WithName("part_class"))
            .Column(s => s.PartKey, cm => cm.WithName("part_key"))
            .Column(s => s.SeoId, cm => cm.WithName("seo_id"))
            .Column(s => s.Name, cm => cm.WithName("name"))
            .Column(s => s.Teeth, cm => cm.WithName("teeth"))
            .Column(s => s.OuterDiameterMm, cm => cm.WithName("outer_mm"))
            .Column(s => s.BoreMm, cm => cm.WithName("bore_mm"))
            .Column(s => s.InnerDiameterMm, cm => cm.WithName("inner_mm"))
            .Column(s => s.WidthMm, cm => cm.WithName("width_mm"))
            .Column(s => s.Module, cm => cm.WithName("module"))
            .Column(s => s.LengthMm, cm => cm.WithName("length_mm"))
            .Column(s => s.Profile, cm => cm.WithName("profile"))
            .Column(s => s.BoreToOuterRatio, cm => cm.WithName("bore_ratio"))
            .Column(s => s.Material, cm => cm.WithName("material"))
            .Column(s => s.PartNumberKey, cm => cm.WithName("part_number_key"))
            .Column(s => s.PartNumber, cm => cm.WithName("part_number"))
            .Column(s => s.PartNumberLabelled, cm => cm.WithName("part_number_labelled"))
            .Column(s => s.Fits, cm => cm.WithName("fits").WithDbType<List<string>>())
            .Column(s => s.KeyConfidence, cm => cm.WithName("key_confidence"))
            .Column(s => s.UpdatedAt, cm => cm.WithName("updated_at"))
            .Column(s => s.HasKey, cm => cm.Ignore()))
        .Define(new Map<PartNumberRow>()
            .TableName("part_numbers")
            .PartitionKey(p => p.PartNumberKey)
            .ClusteringKey(p => p.SeoId)
            .Column(p => p.PartNumberKey, cm => cm.WithName("part_number_key"))
            .Column(p => p.SeoId, cm => cm.WithName("seo_id"))
            .Column(p => p.PartClass, cm => cm.WithName("part_class"))
            .Column(p => p.PartNumber, cm => cm.WithName("part_number"))
            .Column(p => p.Name, cm => cm.WithName("name"))
            .Column(p => p.Confidence, cm => cm.WithName("confidence"))
            .Column(p => p.Labelled, cm => cm.WithName("labelled"))
            .Column(p => p.Primary, cm => cm.WithName("is_primary"))
            .Column(p => p.UpdatedAt, cm => cm.WithName("updated_at")))
        .Define(new Map<PartFitRow>()
            .TableName("part_fits")
            .PartitionKey(f => f.MachineKey)
            .ClusteringKey(f => f.SeoId)
            .Column(f => f.MachineKey, cm => cm.WithName("machine_key"))
            .Column(f => f.SeoId, cm => cm.WithName("seo_id"))
            .Column(f => f.MachineName, cm => cm.WithName("machine_name"))
            .Column(f => f.PartClass, cm => cm.WithName("part_class"))
            .Column(f => f.PartName, cm => cm.WithName("part_name"))
            .Column(f => f.Source, cm => cm.WithName("source"))
            .Column(f => f.Confidence, cm => cm.WithName("confidence"))
            .Column(f => f.Evidence, cm => cm.WithName("evidence"))
            .Column(f => f.UpdatedAt, cm => cm.WithName("updated_at")))
        .Define(new Map<PartFitByPartRow>()
            .TableName("part_fits_by_part")
            .PartitionKey(f => f.SeoId)
            .ClusteringKey(f => f.MachineKey)
            .Column(f => f.MachineKey, cm => cm.WithName("machine_key"))
            .Column(f => f.SeoId, cm => cm.WithName("seo_id"))
            .Column(f => f.MachineName, cm => cm.WithName("machine_name"))
            .Column(f => f.PartClass, cm => cm.WithName("part_class"))
            .Column(f => f.PartName, cm => cm.WithName("part_name"))
            .Column(f => f.Source, cm => cm.WithName("source"))
            .Column(f => f.Confidence, cm => cm.WithName("confidence"))
            .Column(f => f.Evidence, cm => cm.WithName("evidence"))
            .Column(f => f.UpdatedAt, cm => cm.WithName("updated_at")))
        .Define(new Map<PartSameEdge>()
            .TableName("part_same_edges")
            .PartitionKey(e => e.SeoId)
            .ClusteringKey(e => e.OtherSeoId)
            .Column(e => e.SeoId, cm => cm.WithName("seo_id"))
            .Column(e => e.OtherSeoId, cm => cm.WithName("other_seo_id"))
            .Column(e => e.Kind, cm => cm.WithName("kind"))
            .Column(e => e.Status, cm => cm.WithName("status"))
            .Column(e => e.Confidence, cm => cm.WithName("confidence"))
            .Column(e => e.Evidence, cm => cm.WithName("evidence"))
            .Column(e => e.UpdatedAt, cm => cm.WithName("updated_at")));

    public async Task InitializeAsync()
    {
        if (!createTables) return;
        await pages.CreateIfNotExistsAsync();
        await measurements.CreateIfNotExistsAsync();
        await signatures.CreateIfNotExistsAsync();
        await partNumbers.CreateIfNotExistsAsync();
        await fitsByMachine.CreateIfNotExistsAsync();
        await fitsByPart.CreateIfNotExistsAsync();
        await edges.CreateIfNotExistsAsync();
    }

    public async Task<PartPageState?> GetPageStateAsync(string seoId) =>
        (await pages.Where(p => p.SeoId == seoId).ExecuteAsync()).FirstOrDefault();

    public async Task UpsertPageStateAsync(PartPageState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        await pages.Insert(state).SetTTL(TtlSeconds).ExecuteAsync();
    }

    public Task DeletePageStateAsync(string seoId) => pages.Where(p => p.SeoId == seoId).Delete().ExecuteAsync();

    public async Task UpsertMeasurementsAsync(IEnumerable<PartMeasurement> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        var now = DateTime.UtcNow;
        var batch = session.CreateBatch(BatchType.Unlogged);
        foreach (var row in list)
        {
            row.UpdatedAt = now;
            batch.Append(measurements.Insert(row).SetTTL(TtlSeconds));
        }
        await batch.ExecuteAsync();
    }

    public async Task DeleteMeasurementsAsync(string seoId, IEnumerable<string> attributeKeys)
    {
        foreach (var key in attributeKeys)
            await measurements.Where(m => m.SeoId == seoId && m.AttributeKey == key).Delete().ExecuteAsync();
    }

    public Task DeleteMeasurementsAsync(string seoId) => measurements.Where(m => m.SeoId == seoId).Delete().ExecuteAsync();

    public Task DeleteMeasurementRowsAsync(string seoId, string attributeKey, string source, string listingKey) =>
        measurements.Where(m => m.SeoId == seoId && m.AttributeKey == attributeKey && m.Source == source && m.ListingKey == listingKey).Delete().ExecuteAsync();

    public async Task<IReadOnlyList<PartMeasurement>> GetMeasurementsAsync(string seoId) =>
        (await measurements.Where(m => m.SeoId == seoId).ExecuteAsync()).ToList();

    public async Task UpsertSignatureAsync(PartSignatureRow row)
    {
        row.UpdatedAt = DateTime.UtcNow;
        await signatures.Insert(row).SetTTL(TtlSeconds).ExecuteAsync();
    }

    public Task DeleteSignatureAsync(string partClass, string partKey, string seoId) =>
        signatures.Where(s => s.PartClass == partClass && s.PartKey == partKey && s.SeoId == seoId).Delete().ExecuteAsync();

    public async Task<IReadOnlyList<PartSignatureRow>> GetBySignatureAsync(string partClass, string partKey, int limit) =>
        (await signatures.Where(s => s.PartClass == partClass && s.PartKey == partKey).Take(limit).ExecuteAsync()).ToList();

    public async Task UpsertPartNumberAsync(PartNumberRow row)
    {
        row.UpdatedAt = DateTime.UtcNow;
        await partNumbers.Insert(row).SetTTL(TtlSeconds).ExecuteAsync();
    }

    public Task DeletePartNumberAsync(string partNumberKey, string seoId) =>
        partNumbers.Where(p => p.PartNumberKey == partNumberKey && p.SeoId == seoId).Delete().ExecuteAsync();

    public async Task<IReadOnlyList<PartNumberRow>> GetByPartNumberAsync(string partNumberKey, int limit) =>
        (await partNumbers.Where(p => p.PartNumberKey == partNumberKey).Take(limit).ExecuteAsync()).ToList();

    public async Task UpsertFitAsync(PartFitRow fit)
    {
        fit.UpdatedAt = DateTime.UtcNow;
        await fitsByMachine.Insert(fit).SetTTL(TtlSeconds).ExecuteAsync();
        await fitsByPart.Insert(PartFitByPartRow.From(fit)).SetTTL(TtlSeconds).ExecuteAsync();
    }

    public async Task DeleteFitAsync(string machineKey, string seoId)
    {
        await fitsByMachine.Where(f => f.MachineKey == machineKey && f.SeoId == seoId).Delete().ExecuteAsync();
        await fitsByPart.Where(f => f.SeoId == seoId && f.MachineKey == machineKey).Delete().ExecuteAsync();
    }

    public async Task<IReadOnlyList<PartFitRow>> GetPartsForMachineAsync(string machineKey, int limit) =>
        (await fitsByMachine.Where(f => f.MachineKey == machineKey).Take(limit).ExecuteAsync()).ToList();

    public async Task<IReadOnlyList<PartFitRow>> GetMachinesForPartAsync(string seoId) =>
        (await fitsByPart.Where(f => f.SeoId == seoId).ExecuteAsync()).Cast<PartFitRow>().ToList();

    public async Task UpsertSameEdgeAsync(PartSameEdge edge)
    {
        edge.UpdatedAt = DateTime.UtcNow;
        await edges.Insert(edge).SetTTL(TtlSeconds).ExecuteAsync();
        await edges.Insert(edge.Reverse()).SetTTL(TtlSeconds).ExecuteAsync();
    }

    public async Task DeleteSameEdgeAsync(string seoId, string otherSeoId)
    {
        await edges.Where(e => e.SeoId == seoId && e.OtherSeoId == otherSeoId).Delete().ExecuteAsync();
        await edges.Where(e => e.SeoId == otherSeoId && e.OtherSeoId == seoId).Delete().ExecuteAsync();
    }

    public async Task<IReadOnlyList<PartSameEdge>> GetSameEdgesAsync(string seoId, int limit = IPartStore.MaxEdges) =>
        (await edges.Where(e => e.SeoId == seoId).Take(limit).ExecuteAsync()).ToList();
}
