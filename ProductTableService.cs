using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane;

/// <summary>
/// Service for managing Cassandra tables for products, product listings, and price history
/// Provides access to product-related tables with proper mapping configuration
/// </summary>
public class ProductTableService
{
    private readonly ISession session;
    private readonly IMapper mapper;
    private readonly Table<Product> products;
    private readonly Table<ProductListing> productListings;
    private readonly Table<PricePoint> priceHistory;
    private readonly Table<Listing> listings;
    private readonly Table<SitemapEntry> sitemapEntries;
    private readonly Table<FlipReport> flipReports;
    private readonly Table<MigrationRun> migrationRuns;
    private readonly Table<RegroupMove> regroupMoves;
    private static bool tablesInitialized = false;
    private static readonly SemaphoreSlim initSemaphore = new(1, 1);

    /// <summary>
    /// Exposes the Cassandra session for direct queries when needed
    /// </summary>
    public ISession Session => session;

    public ProductTableService(ISession session)
    {
        this.session = session;

        var mapping = new MappingConfiguration()
            .Define(new Map<Product>()
                .TableName("products")
                .PartitionKey(p => p.SeoId) // SEO ID is now the primary key
                .Column(p => p.SeoId, cm => cm.WithName("seo_id"))
                .Column(p => p.Category, cm => cm.WithName("category").WithDbType<List<string>>())
                .Column(p => p.Name, cm => cm.WithName("name"))
                .Column(p => p.NormalizedName, cm => cm.WithName("normalized_name"))
                .Column(p => p.Brand, cm => cm.WithName("brand"))
                .Column(p => p.Model, cm => cm.WithName("model"))
                .Column(p => p.IdentifierType, cm => cm.WithName("identifier_type"))
                .Column(p => p.IdentifierValue, cm => cm.WithName("identifier_value"))
                .Column(p => p.AveragePrice, cm => cm.WithName("average_price"))
                .Column(p => p.MedianPrice, cm => cm.WithName("median_price"))
                .Column(p => p.MinPrice, cm => cm.WithName("min_price"))
                .Column(p => p.MaxPrice, cm => cm.WithName("max_price"))
                .Column(p => p.EstimatedValue, cm => cm.WithName("estimated_value"))
                .Column(p => p.SoldCount, cm => cm.WithName("sold_count"))
                .Column(p => p.ListingCount, cm => cm.WithName("listing_count"))
                .Column(p => p.OffersFound, cm => cm.WithName("offers_found"))
                .Column(p => p.LastUpdated, cm => cm.WithName("last_updated"))
                .Column(p => p.CreatedAt, cm => cm.WithName("created_at"))
                .Column(p => p.SampleTitles, cm => cm.WithName("sample_titles"))
                .Column(p => p.Condition, cm => cm.WithName("condition"))
                .Column(p => p.ImageUrl, cm => cm.WithName("image_url"))
                .Column(p => p.RelatedSeoIds, cm => cm.WithName("related_seo_ids"))
                .Column(p => p.CanonicalSeoId, cm => cm.WithName("canonical_seo_id"))
            )
            .Define(new Map<ProductListing>()
                .TableName("product_listings")
                .PartitionKey(pl => pl.ProductSeoId) // Reference to Product.SeoId
                .ClusteringKey(pl => pl.FoundAt, SortOrder.Descending)
                .ClusteringKey(pl => pl.ListingId)
                .Column(pl => pl.ListingId, cm => cm.WithName("listing_id").WithSecondaryIndex())
                .Column(pl => pl.ProductSeoId, cm => cm.WithName("product_seo_id"))
                .Column(pl => pl.Title, cm => cm.WithName("title"))
                .Column(pl => pl.Price, cm => cm.WithName("price"))
                .Column(pl => pl.Currency, cm => cm.WithName("currency"))
                .Column(pl => pl.Platform, cm => cm.WithDbType<int>().WithName("platform"))
                .Column(pl => pl.FoundAt, cm => cm.WithName("found_at"))
                .Column(pl => pl.IsSold, cm => cm.WithName("is_sold"))
                .Column(pl => pl.Condition, cm => cm.WithName("condition"))
                .Column(pl => pl.Country, cm => cm.WithName("country"))
                .Column(pl => pl.ImageUrl, cm => cm.WithName("image_url"))
                .Column(pl => pl.Url, cm => cm.WithName("url"))
                .Column(pl => pl.IsActive, cm => cm.WithName("is_active"))
                .Column(pl => pl.InactiveSince, cm => cm.WithName("inactive_since"))
                .Column(pl => pl.SellerHash, cm => cm.WithName("seller_hash").WithSecondaryIndex())
            )
            .Define(new Map<PricePoint>()
                .TableName("price_history")
                .PartitionKey(ph => ph.ProductSeoId) // Reference to Product.SeoId
                .ClusteringKey(ph => ph.Date, SortOrder.Descending)
                .Column(ph => ph.ProductSeoId, cm => cm.WithName("product_seo_id"))
                .Column(ph => ph.Date, cm => cm.WithName("date"))
                .Column(ph => ph.AveragePrice, cm => cm.WithName("average_price"))
                .Column(ph => ph.MedianPrice, cm => cm.WithName("median_price"))
                .Column(ph => ph.MinPrice, cm => cm.WithName("min_price"))
                .Column(ph => ph.MaxPrice, cm => cm.WithName("max_price"))
                .Column(ph => ph.SampleCount, cm => cm.WithName("sample_count"))
            )
            .Define(new Map<Listing>()
                .TableName("listings")
                .PartitionKey(pe => pe.Id)
                .ClusteringKey(pe => pe.Platform)
                .Column(pe => pe.Platform, cm => cm.WithDbType<int>())
                .Column(pe => pe.PriceKind, cm => cm.WithDbType<int>())
            )
            .Define(new Map<SitemapEntry>()
                .TableName("sitemap_entries")
                .PartitionKey(e => e.Partition)
                .ClusteringKey(e => e.EntryIndex)
                .Column(e => e.Partition, cm => cm.WithName("partition_id"))
                .Column(e => e.EntryIndex, cm => cm.WithName("entry_index"))
                .Column(e => e.SeoId, cm => cm.WithName("seo_id"))
                .Column(e => e.LastUpdated, cm => cm.WithName("last_updated"))
            )
            .Define(new Map<FlipReport>()
                .TableName("flip_reports")
                .PartitionKey(r => r.Status)
                .ClusteringKey(r => r.CreatedAt, SortOrder.Descending)
                .ClusteringKey(r => r.ReportId)
                .Column(r => r.ReportId, cm => cm.WithName("report_id"))
                .Column(r => r.CreatedAt, cm => cm.WithName("created_at"))
                .Column(r => r.ReportedBy, cm => cm.WithName("reported_by"))
                .Column(r => r.ListingId, cm => cm.WithName("listing_id"))
                .Column(r => r.ListingTitle, cm => cm.WithName("listing_title"))
                .Column(r => r.Platform, cm => cm.WithName("platform"))
                .Column(r => r.Category, cm => cm.WithName("category"))
                .Column(r => r.Price, cm => cm.WithName("price"))
                .Column(r => r.ListingJson, cm => cm.WithName("listing_json"))
                .Column(r => r.Profit, cm => cm.WithName("profit"))
                .Column(r => r.MedianPrice, cm => cm.WithName("median_price"))
                .Column(r => r.RecentSellsJson, cm => cm.WithName("recent_sells_json"))
                .Column(r => r.Reason, cm => cm.WithName("reason"))
                .Column(r => r.CurrentSlug, cm => cm.WithName("current_slug"))
                .Column(r => r.SuggestedSlug, cm => cm.WithName("suggested_slug"))
                .Column(r => r.Status, cm => cm.WithName("status"))
                .Column(r => r.ProductSeoId, cm => cm.WithName("product_seo_id"))
                .Column(r => r.ProductName, cm => cm.WithName("product_name"))
                .Column(r => r.ProductBrand, cm => cm.WithName("product_brand"))
                .Column(r => r.ProductModel, cm => cm.WithName("product_model"))
                .Column(r => r.ProductCategory, cm => cm.WithName("product_category"))
                .Column(r => r.ProductListingCount, cm => cm.WithName("product_listing_count"))
                .Column(r => r.ProductMedianPrice, cm => cm.WithName("product_median_price"))
                .Column(r => r.ContextJson, cm => cm.WithName("context_json"))
                .Column(r => r.ResolvedAt, cm => cm.WithName("resolved_at"))
                .Column(r => r.Resolution, cm => cm.WithName("resolution"))
                .Column(r => r.ResolutionRef, cm => cm.WithName("resolution_ref"))
            )
            .Define(new Map<MigrationRun>()
                .TableName("migration_runs")
                .PartitionKey(r => r.RunId)
                .ClusteringKey(r => r.Mode)
                .Column(r => r.RunId, cm => cm.WithName("run_id"))
                .Column(r => r.Mode, cm => cm.WithName("mode"))
                .Column(r => r.StartedAt, cm => cm.WithName("started_at"))
                .Column(r => r.FinishedAt, cm => cm.WithName("finished_at"))
                .Column(r => r.Status, cm => cm.WithName("status"))
                .Column(r => r.Counters, cm => cm.WithName("counters").WithDbType<Dictionary<string, long>>())
                .Column(r => r.Checkpoint, cm => cm.WithName("checkpoint"))
            )
            .Define(new Map<RegroupMove>()
                .TableName("regroup_moves")
                .PartitionKey(m => m.RunId)
                .ClusteringKey(m => m.ListingPlatform)
                .ClusteringKey(m => m.ListingId)
                .ClusteringKey(m => m.FromSlug)
                .Column(m => m.RunId, cm => cm.WithName("run_id"))
                .Column(m => m.ListingPlatform, cm => cm.WithName("listing_platform"))
                .Column(m => m.ListingId, cm => cm.WithName("listing_id"))
                .Column(m => m.FromSlug, cm => cm.WithName("from_slug"))
                .Column(m => m.Title, cm => cm.WithName("title"))
                .Column(m => m.Price, cm => cm.WithName("price"))
                .Column(m => m.ToSlug, cm => cm.WithName("to_slug"))
                .Column(m => m.Outcome, cm => cm.WithName("outcome"))
                .Column(m => m.DetachReason, cm => cm.WithName("detach_reason"))
                .Column(m => m.TargetName, cm => cm.WithName("target_name"))
                .Column(m => m.Applied, cm => cm.WithName("applied"))
                .Column(m => m.CreatedAt, cm => cm.WithName("created_at"))
            );

        mapper = new Mapper(session, mapping);
        products = new Table<Product>(session, mapping);
        productListings = new Table<ProductListing>(session, mapping);
        priceHistory = new Table<PricePoint>(session, mapping);
        listings = new Table<Listing>(session, mapping);
        sitemapEntries = new Table<SitemapEntry>(session, mapping);
        flipReports = new Table<FlipReport>(session, mapping);
        migrationRuns = new Table<MigrationRun>(session, mapping);
        regroupMoves = new Table<RegroupMove>(session, mapping);
    }

    /// <summary>
    /// Initialize Cassandra tables if they don't exist
    /// </summary>
    public async Task InitializeTablesAsync()
    {
        if (tablesInitialized) return;

        await initSemaphore.WaitAsync();
        try
        {
            if (tablesInitialized) return;

            await products.CreateIfNotExistsAsync();
            await productListings.CreateIfNotExistsAsync();
            await priceHistory.CreateIfNotExistsAsync();
            await listings.CreateIfNotExistsAsync();
            await sitemapEntries.CreateIfNotExistsAsync();
            await flipReports.CreateIfNotExistsAsync();
            await migrationRuns.CreateIfNotExistsAsync();
            await regroupMoves.CreateIfNotExistsAsync();
            await session.ExecuteAsync(new SimpleStatement(
                $"ALTER TABLE regroup_moves WITH default_time_to_live = {RegroupMove.TtlSeconds}"));
            await EnsureRetentionDefaultsAsync();
            await EnsureSellerHashLineageSchemaAsync();
            await EnsureOffersFoundSchemaAsync();
            await EnsureFlipReportColumnsAsync();

            tablesInitialized = true;
        }
        finally
        {
            initSemaphore.Release();
        }
    }

    private async Task EnsureRetentionDefaultsAsync()
    {
        await session.ExecuteAsync(new SimpleStatement(
            $"ALTER TABLE products WITH default_time_to_live = {ProductDataRetention.ProductTtlSeconds}"));
        await session.ExecuteAsync(new SimpleStatement(
            $"ALTER TABLE product_listings WITH default_time_to_live = {ProductDataRetention.ActiveProductListingTtlSeconds}"));
        await session.ExecuteAsync(new SimpleStatement(
            $"ALTER TABLE price_history WITH default_time_to_live = {ProductDataRetention.PriceHistoryTtlSeconds}"));
        // Raw listing snapshots: writers set a shorter explicit TTL, the table default caps anything else at 14 days.
        await session.ExecuteAsync(new SimpleStatement(
            $"ALTER TABLE listings WITH default_time_to_live = {ListingRetention.TableDefaultTtlSeconds}"));
    }

    private async Task EnsureSellerHashLineageSchemaAsync()
    {
        var columns = await session.ExecuteAsync(new SimpleStatement(
            "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ? AND column_name = ?",
            session.Keyspace,
            "product_listings",
            "seller_hash"));
        if (!columns.Any())
            await session.ExecuteAsync(new SimpleStatement(
                "ALTER TABLE product_listings ADD seller_hash text"));

        await session.ExecuteAsync(new SimpleStatement(
            "CREATE INDEX IF NOT EXISTS product_listings_seller_hash_idx ON product_listings (seller_hash)"));
    }

    /// <summary>
    /// Adds <c>offers_found</c> to <c>products</c> for tables created before it existed (existing tables are
    /// not altered by <c>CreateIfNotExistsAsync</c> - same idiom as <see cref="EnsureSellerHashLineageSchemaAsync"/>).
    /// Safe to run on every startup: the column-existence check makes it a no-op once the column is there.
    /// </summary>
    private async Task EnsureOffersFoundSchemaAsync()
    {
        var columns = await session.ExecuteAsync(new SimpleStatement(
            "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ? AND column_name = ?",
            session.Keyspace,
            "products",
            "offers_found"));
        if (!columns.Any())
            await session.ExecuteAsync(new SimpleStatement(
                "ALTER TABLE products ADD offers_found int"));
    }

    /// <summary>New flip_reports columns as (name, cql type), added to tables created before they existed.</summary>
    public static readonly (string Name, string CqlType)[] FlipReportAddedColumns =
    {
        ("product_seo_id", "text"), ("product_name", "text"), ("product_brand", "text"), ("product_model", "text"),
        ("product_category", "text"), ("product_listing_count", "int"), ("product_median_price", "double"),
        ("context_json", "text"), ("resolved_at", "timestamp"), ("resolution", "text"), ("resolution_ref", "text"),
    };

    private volatile bool flipReportSchemaReady;

    /// <summary>
    /// AneApi never calls <see cref="InitializeTablesAsync"/> (only AneNotifier does), so every flip report access ensures the
    /// flip_reports columns itself, once per process. Idempotent and safe next to the notifier's run of the same check.
    /// </summary>
    public async Task EnsureFlipReportSchemaAsync()
    {
        if (flipReportSchemaReady) return;
        await initSemaphore.WaitAsync();
        try
        {
            if (flipReportSchemaReady) return;
            await EnsureFlipReportColumnsAsync();
            flipReportSchemaReady = true;
        }
        finally
        {
            initSemaphore.Release();
        }
    }

    /// <summary>Adds the report enrichment/resolution columns to an existing flip_reports table (same idiom as <see cref="EnsureOffersFoundSchemaAsync"/>, no-op once present).</summary>
    private async Task EnsureFlipReportColumnsAsync()
    {
        await flipReports.CreateIfNotExistsAsync();
        foreach (var (name, type) in FlipReportAddedColumns)
        {
            var columns = await session.ExecuteAsync(new SimpleStatement(
                "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ? AND column_name = ?",
                session.Keyspace, "flip_reports", name));
            if (!columns.Any())
                await session.ExecuteAsync(new SimpleStatement($"ALTER TABLE flip_reports ADD {name} {type}"));
        }
    }

    /// <summary>
    /// Access to the products table
    /// </summary>
    public Table<Product> Products => products;

    /// <summary>
    /// Access to the product listings table
    /// </summary>
    public Table<ProductListing> ProductListings => productListings;

    /// <summary>
    /// Access to the price history table
    /// </summary>
    public Table<PricePoint> PriceHistory => priceHistory;

    /// <summary>
    /// Access to the listings table
    /// </summary>
    public Table<Listing> Listings => listings;

    public Table<SitemapEntry> SitemapEntries => sitemapEntries;

    public Table<FlipReport> FlipReports => flipReports;

    /// <summary>Tracks one-time background migration/regroup runs (see AneNotifier's RegroupRunService).</summary>
    public Table<MigrationRun> MigrationRuns => migrationRuns;

    /// <summary>
    /// Writes decisions of one regroup run to <c>regroup_moves</c> as a single-partition unlogged batch (all
    /// rows share the run id) with a 30 day TTL. Callers keep batches small (see RegroupMoveBuffer).
    /// </summary>
    public async Task InsertRegroupMovesAsync(IReadOnlyList<RegroupMove> moves)
    {
        if (moves.Count == 0)
            return;
        var batch = session.CreateBatch(BatchType.Unlogged);
        foreach (var move in moves)
            batch.Append(regroupMoves.Insert(move).SetTTL(RegroupMove.TtlSeconds));
        await batch.ExecuteAsync();
    }

    /// <summary>One page of a run's persisted decisions; pass the returned paging state back to continue.</summary>
    public async Task<(IReadOnlyList<RegroupMove> Moves, byte[]? PagingState)> GetRegroupMovesPageAsync(
        string runId, int limit, byte[]? pagingState)
    {
        var cql = Cql.New("WHERE run_id = ?", runId)
            .WithOptions(o =>
            {
                o.SetPageSize(limit);
                if (pagingState != null)
                    o.SetPagingState(pagingState);
            });
        var page = await mapper.FetchPageAsync<RegroupMove>(cql);
        return (page.ToList(), page.PagingState);
    }

    /// <summary>Loads a migration run row, or null when this run_id+mode has never been started.</summary>
    public async Task<MigrationRun?> GetMigrationRunAsync(string runId, string mode)
    {
        return await migrationRuns
            .Where(r => r.RunId == runId && r.Mode == mode)
            .FirstOrDefault()
            .ExecuteAsync();
    }

    /// <summary>Upserts a migration run row (no TTL - these are small, operator-relevant audit rows).</summary>
    public async Task UpsertMigrationRunAsync(MigrationRun run)
    {
        await migrationRuns.Insert(run).ExecuteAsync();
    }

    /// <summary>
    /// Get a product by SEO ID
    /// </summary>
    public async Task<Product?> GetProductAsync(string seoId)
    {
        return await products.FirstOrDefault(p => p.SeoId == seoId).ExecuteAsync();
    }

    /// <summary>
    /// Insert or update a product
    /// </summary>
    public async Task UpsertProductAsync(Product product)
    {
        product.LastUpdated = DateTime.UtcNow;
        await products.Insert(product)
            .SetTTL(ProductDataRetention.ProductTtlSeconds)
            .ExecuteAsync();
    }

    /// <summary>
    /// Get product listings with cursor-based pagination
    /// </summary>
    /// <param name="productSeoId">Product SEO ID</param>
    /// <param name="before">Cursor timestamp - return listings before this time</param>
    /// <param name="limit">Maximum number of results</param>
    public async Task<List<ProductListing>> GetProductListingsAsync(string productSeoId, DateTime before, int limit = 50)
    {
        var query = productListings
            .Where(pl => pl.ProductSeoId == productSeoId && pl.FoundAt < before)
            .OrderByDescending(pl => pl.FoundAt)
            .Take(limit);

        var result = await query.ExecuteAsync();
        return result.ToList();
    }

    /// <summary>
    /// Get all product listings for a product (used for price aggregation)
    /// </summary>
    public async Task<List<ProductListing>> GetAllProductListingsAsync(string productSeoId, int limit = 1000)
    {
        var query = productListings
            .Where(pl => pl.ProductSeoId == productSeoId)
            .OrderByDescending(pl => pl.FoundAt)
            .Take(limit);

        var result = await query.ExecuteAsync();
        return result.ToList();
    }

    /// <summary>
    /// Insert a product listing
    /// </summary>
    public async Task InsertProductListingAsync(ProductListing listing)
    {
        await productListings.Insert(listing)
            .SetTTL(ProductDataRetention.GetProductListingTtlSeconds(listing))
            .ExecuteAsync();
    }

    /// <summary>
    /// Get price history for a product
    /// </summary>
    public async Task<List<PricePoint>> GetPriceHistoryAsync(string productSeoId, int days = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var query = priceHistory
            .Where(ph => ph.ProductSeoId == productSeoId && ph.Date >= cutoff)
            .OrderByDescending(ph => ph.Date);

        var result = await query.ExecuteAsync();
        return result.ToList();
    }

    /// <summary>
    /// Insert a price point
    /// </summary>
    public async Task InsertPricePointAsync(PricePoint pricePoint)
    {
        await priceHistory.Insert(pricePoint)
            .SetTTL(ProductDataRetention.GetPriceHistoryTtlSeconds(pricePoint))
            .ExecuteAsync();
    }

    /// <summary>
    /// Get a listing by ID and platform
    /// </summary>
    public async Task<Listing?> GetListingAsync(string id, Platform platform)
    {
        // The driver cannot bind the Platform enum as a query parameter ("Unknown Cassandra target type for
        // CLR type Coflnet.Ane.Platform"), so read the partition by Id alone and pick the platform in memory.
        var rows = await mapper.FetchAsync<Listing>(BuildListingLookupCql(id));
        return SelectListingForPlatform(rows.ToList(), platform);
    }

    /// <summary>
    /// Statement reading every platform's row of one listing id (Id is the partition key, Platform the
    /// clustering key stored as int). Binds only the string id - never an enum.
    /// </summary>
    public static Cql BuildListingLookupCql(string id) => Cql.New("SELECT * FROM listings WHERE id = ?", id);

    /// <summary>Picks the row of <paramref name="platform"/> out of the rows of one listing id, or null.</summary>
    public static Listing? SelectListingForPlatform(IReadOnlyList<Listing> candidates, Platform platform) =>
        candidates.FirstOrDefault(l => l.Platform == platform);

    public async Task InsertFlipReportAsync(FlipReport report)
    {
        await EnsureFlipReportSchemaAsync();
        await flipReports.Insert(report).ExecuteAsync();
    }

    public async Task<List<FlipReport>> GetFlipReportsAsync(string status, int limit = 100)
    {
        await EnsureFlipReportSchemaAsync();
        var result = await flipReports
            .Where(r => r.Status == status)
            .Take(limit)
            .ExecuteAsync();
        return result.ToList();
    }

    /// <summary>Statuses a report can have; each is a partition of flip_reports.</summary>
    public static readonly string[] FlipReportStatuses = { "pending", "approved", "rejected", "fixed" };

    /// <summary>
    /// Reports created at or after <paramref name="since"/>, newest first, optionally of one status. The partition key is the status and
    /// created_at the first clustering column, so each status is one efficient clustering range read; without a status the (few) status
    /// partitions are read separately and merged in memory. Rows with a status outside <see cref="FlipReportStatuses"/> are not returned.
    /// </summary>
    public async Task<List<FlipReport>> GetFlipReportsSinceAsync(DateTime? since, string? status, int limit)
    {
        await EnsureFlipReportSchemaAsync();
        var statuses = string.IsNullOrEmpty(status) ? FlipReportStatuses : new[] { status };
        var all = new List<FlipReport>();
        foreach (var s in statuses)
        {
            var query = since.HasValue
                ? flipReports.Where(r => r.Status == s && r.CreatedAt >= since.Value)
                : flipReports.Where(r => r.Status == s);
            all.AddRange(await query.Take(limit).ExecuteAsync());
        }
        return all.OrderByDescending(r => r.CreatedAt).Take(limit).ToList();
    }

    /// <summary>
    /// One report by id. report_id is only the second clustering column (after created_at) of the status partition, so an id lookup
    /// cannot be a point read: this scans each status partition (bounded by <paramref name="scanLimit"/> newest rows per status).
    /// flip_reports is small (user submitted reports), so this is acceptable; add a report_id lookup table if it ever grows large.
    /// </summary>
    public async Task<FlipReport?> GetFlipReportByIdAsync(string reportId, int scanLimit = 5000)
    {
        await EnsureFlipReportSchemaAsync();
        foreach (var s in FlipReportStatuses)
        {
            var rows = await flipReports.Where(r => r.Status == s).Take(scanLimit).ExecuteAsync();
            var hit = rows.FirstOrDefault(r => string.Equals(r.ReportId, reportId, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
                return hit;
        }
        return null;
    }

    /// <summary>
    /// Stores the resolution. Status is the partition key so a status change is insert-new then delete-old (a crash in between leaves a
    /// duplicate, never a lost report). <paramref name="report"/> carries the NEW status; <paramref name="oldStatus"/> the stored one.
    /// </summary>
    public async Task SaveFlipReportResolutionAsync(FlipReport report, string oldStatus)
    {
        await EnsureFlipReportSchemaAsync();
        await flipReports.Insert(report).ExecuteAsync();
        if (!string.Equals(oldStatus, report.Status, StringComparison.Ordinal))
            await flipReports.Where(r => r.Status == oldStatus && r.CreatedAt == report.CreatedAt && r.ReportId == report.ReportId)
                .Delete().ExecuteAsync();
    }

    /// <summary>
    /// Statement for one page of <see cref="GetProductListingPartitionKeysPageAsync"/>. Automatic paging is
    /// off: with it on, enumerating the row set fetches every following page too, so a "page" would be the
    /// whole table and the returned paging state always null.
    /// </summary>
    public static IStatement BuildProductListingPartitionKeysStatement(int pageSize, byte[]? pagingState)
    {
        var statement = new SimpleStatement("SELECT DISTINCT product_seo_id FROM product_listings")
            .SetPageSize(pageSize)
            .SetAutoPage(false);
        if (pagingState != null)
            statement = statement.SetPagingState(pagingState);
        return statement;
    }

    /// <summary>
    /// Pages through the distinct <c>product_seo_id</c> partition keys of <c>product_listings</c>, via a
    /// raw CQL <c>SELECT DISTINCT</c> (the LINQ mapper has no way to express "distinct partition keys" -
    /// see the Products table LINQ mappings above). The <c>products</c> table is ~44x larger on disk than
    /// <c>product_listings</c> and ~99.7% of products in <c>products</c>-token order have no offers left
    /// at all (offers expire in 30-90 days, products live 365), so both the periodic maintenance pass
    /// (<c>ProductGrouper.ProcessProductsBackgroundTasksAsync</c>) and the one-time regroup scan
    /// (<c>RegroupRunService.RunScanAsync</c>) drive their scan off this instead of paging <c>products</c>
    /// directly - shared here so both use the exact same query. A caller pairs this with
    /// <see cref="GetProductAsync"/> per key and must treat a missing product row as an orphan (expected -
    /// old products get pruned from <c>products</c> before their trailing offer rows expire).
    /// </summary>
    public async Task<(IReadOnlyList<string> SeoIds, byte[]? PagingState)> GetProductListingPartitionKeysPageAsync(
        int pageSize, byte[]? pagingState)
    {
        var rowSet = await session.ExecuteAsync(BuildProductListingPartitionKeysStatement(pageSize, pagingState));
        var seoIds = rowSet
            .Select(row => row.GetValue<string>("product_seo_id"))
            .Where(seoId => !string.IsNullOrEmpty(seoId))
            .ToList();
        return (seoIds, rowSet.PagingState);
    }
}
