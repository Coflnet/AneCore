using MessagePack;

namespace Coflnet.Ane;

[MessagePackObject]
public class MissingListing
{
    [Key(0)]
    public string ListingId { get; set; } = string.Empty;
    [Key(1)]
    public string ProductSeoId { get; set; } = string.Empty;
    [Key(2)]
    public DateTime FoundAt { get; set; }
    [Key(3)]
    public double Price { get; set; }
}
