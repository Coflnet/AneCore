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
    private readonly Table<Product> products;
    private readonly Table<ProductListing> productListings;
    private readonly Table<PricePoint> priceHistory;
    private readonly Table<Listing> listings;
    private static bool tablesInitialized = false;
    private static readonly object initLock = new object();

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
                .Column(p => p.ListingCount, cm => cm.WithName("listing_count"))
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
            );

        products = new Table<Product>(session, mapping);
        productListings = new Table<ProductListing>(session, mapping);
        priceHistory = new Table<PricePoint>(session, mapping);
        listings = new Table<Listing>(session, mapping);
    }

    /// <summary>
    /// Initialize Cassandra tables if they don't exist
    /// </summary>
    public async Task InitializeTablesAsync()
    {
        if (tablesInitialized) return;

        lock (initLock)
        {
            if (tablesInitialized) return;
            tablesInitialized = true;
        }

        await products.CreateIfNotExistsAsync();
        await productListings.CreateIfNotExistsAsync();
        await priceHistory.CreateIfNotExistsAsync();
        await listings.CreateIfNotExistsAsync();
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
        await products.Insert(product).ExecuteAsync();
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
        await productListings.Insert(listing).ExecuteAsync();
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
        await priceHistory.Insert(pricePoint).ExecuteAsync();
    }

    /// <summary>
    /// Get a listing by ID and platform
    /// </summary>
    public async Task<Listing?> GetListingAsync(string id, Platform platform)
    {
        return await listings.FirstOrDefault(l => l.Id == id && l.Platform == platform).ExecuteAsync();
    }
}
