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

    /// <summary>
    /// pending / approved / rejected
    /// </summary>
    public string Status { get; set; } = "pending";
}
