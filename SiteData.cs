namespace Coflnet.Ane;

/// <summary>
/// Simple key-value store for site-wide data (e.g., sitemap XML).
/// Shared between services via Cassandra.
/// </summary>
public class SiteData
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
