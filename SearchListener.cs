using MessagePack;

namespace Coflnet.Ane;

[MessagePackObject]
public class SearchListener
{
    [Key(0)]
    public string UserId { get; set; }
    [Key(1)]
    public long Id { get; set; }
    [Key(2)]
    public List<FilterInfo> Filters { get; set; }
}
