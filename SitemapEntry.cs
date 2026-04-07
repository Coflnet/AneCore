namespace Coflnet.Ane;

/// <summary>
/// A single sitemap URL entry stored in ScyllaDB.
/// Partition key: Partition (int) — groups entries into chunks of up to 5000 products (10k URLs with locales).
/// Clustering key: EntryIndex (int) — deduplicates entries within a partition on upsert.
/// </summary>
public class SitemapEntry
{
    public int Partition { get; set; }
    public int EntryIndex { get; set; }
    public string SeoId { get; set; } = null!;
    public DateTime LastUpdated { get; set; }
}
