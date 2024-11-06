namespace Coflnet.Ane;

[Flags]
public enum PriceKind
{
    Unknown,
    BuyItemNow,
    Auction,
    Negotiatable = 4,
    IncludesTax = 8,
}
