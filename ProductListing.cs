namespace Coflnet.Ane;

/// <summary>
/// Represents a listing associated with a specific product
/// Links listings to their grouped product via ProductSeoId
/// </summary>
public class ProductListing
{
    public string ProductSeoId { get; set; } = ""; // References Product.SeoId
    public string ListingId { get; set; } = "";
    public string Title { get; set; } = "";
    public double Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public Platform Platform { get; set; }
    public DateTime FoundAt { get; set; }
    public bool IsSold { get; set; }
    public string Condition { get; set; } = "unknown";
    public string Country { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? Url { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? InactiveSince { get; set; }
    public List<string> Categories { get; set; } = new(); // All applicable categories (for ambiguous mappings)
}
