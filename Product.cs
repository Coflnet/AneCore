using System.Text.Json.Serialization;

namespace Coflnet.Ane;

/// <summary>
/// Represents a product aggregated from multiple listings
/// Primary key is the SEO-friendly identifier (SeoId)
/// </summary>
public class Product
{
    [JsonPropertyName("id")]
    public string SeoId { get; set; } = ""; // Primary key - SEO-friendly product identifier
    public List<string> Category { get; set; } = new(); // Categories extracted by LLM (may contain multiple categories)
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? IdentifierType { get; set; }
    public string? IdentifierValue { get; set; }
    public string Condition { get; set; } = "unknown";
    public double AveragePrice { get; set; }
    public double MedianPrice { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public int ListingCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<string> SampleTitles { get; set; } = new();
    public string? ImageUrl { get; set; }
    public Dictionary<string, string>? Attributes { get; set; } // Key attributes extracted from listings

    // Canonical SEO ID for grouped products - if set, this product redirects to another
    public string? CanonicalSeoId { get; set; }
    // All SEO IDs grouped together (includes self) - stored on canonical product only
    public List<string> RelatedSeoIds { get; set; } = new();
}
