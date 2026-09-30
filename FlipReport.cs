namespace Coflnet.Ane;

/// <summary>
/// A user-submitted flip report with optional grouping correction.
/// Stored in Cassandra, partitioned by date bucket for efficient retrieval.
/// </summary>
public class FlipReport
{
    public string ReportId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReportedBy { get; set; }

    // Listing snapshot
    public string? ListingId { get; set; }
    public string? ListingTitle { get; set; }
    public string? Platform { get; set; }
    public string? Category { get; set; }
    public double? Price { get; set; }
    public string? ListingJson { get; set; }

    // Flip context
    public double? Profit { get; set; }
    public double? MedianPrice { get; set; }
    public string? RecentSellsJson { get; set; }

    // User feedback
    public string? Reason { get; set; }
    public string? CurrentSlug { get; set; }
    public string? SuggestedSlug { get; set; }

    // Product context captured at report time (all null on reports written before these columns existed).
    // The description and photo urls of the listing are inside ListingJson (Description, ImageUrls).

    /// <summary>The product the listing actually sits on (verified against product_listings when possible), not the client supplied slug.</summary>
    public string? ProductSeoId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductBrand { get; set; }
    public string? ProductModel { get; set; }
    /// <summary>Categories of the product, joined with ", ".</summary>
    public string? ProductCategory { get; set; }
    public int? ProductListingCount { get; set; }
    public double? ProductMedianPrice { get; set; }
    /// <summary>Free json: product attributes, specificity, category path, lookup diagnostics, enrichment errors.</summary>
    public string? ContextJson { get; set; }

    // Resolution (set by the operator through the training report endpoints).
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    /// <summary>Commit hash or regroup run id of the fix.</summary>
    public string? ResolutionRef { get; set; }

    /// <summary>
    /// pending / approved / rejected / fixed. Partition key of flip_reports: changing it moves the row.
    /// </summary>
    public string Status { get; set; } = "pending";
}
