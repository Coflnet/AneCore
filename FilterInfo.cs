using MessagePack;

namespace Coflnet.Ane;

[MessagePackObject]
public class FilterInfo
{
    [Key(0)]
    public string Name { get; set; }
    [Key(1)]
    public string Value { get; set; }
}