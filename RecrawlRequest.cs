using MessagePack;

namespace Coflnet.Ane;

[MessagePackObject]
public class RecrawlRequest
{
    [Key(0)]
    public string ListingId { get; set; } = string.Empty;
    [Key(1)]
    public string? Url { get; set; }
    [Key(2)]
    public Platform Platform { get; set; }
    [Key(3)]
    public string ProductSeoId { get; set; } = string.Empty;
    /// <summary>Exact operator-requested snapshot recovery; null for ordinary availability checks.</summary>
    [Key(4)]
    public string? SnapshotRecoveryId { get; set; }
    [Key(5)]
    public DateTime? SnapshotRecoveryExpiresAt { get; set; }
    /// <summary>Explicit crowd-origin Kleinanzeigen detail discovery; legacy requests remain availability-only.</summary>
    [Key(6)]
    public bool IsDiscovery { get; set; }

}
