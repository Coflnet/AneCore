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
}
