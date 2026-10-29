namespace Coflnet.Ane.ShopReferences;

/// <summary>Public factual identity of one verified single-tool merchant article, without a cached price.</summary>
public sealed record GoToolsArticle(string KnownProductId, string Mpn, string Gtin, string MerchantSku, string Url);

public static class GoToolsArticles
{
    public static IReadOnlyList<GoToolsArticle> All { get; } = Array.AsReadOnly<GoToolsArticle>([
        new("knipex-0301160", "03 01 160", "4003773013235", "35930", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-160-mm-poliert-kunststoffueberzug_144938_35930"),
        new("knipex-8701250", "87 01 250", "4003773022022", "18450", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-250-mm-spannweite-46-mm-poliert_127452_18450"),
        new("knipex-8603250", "86 03 250", "4003773033837", "18589", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-250-mm-spannweite-52-mm-verchromt-kunststoffueberzug_127591_18589")
    ]);

    public static GoToolsArticle? ForProduct(string? knownProductId) => All.FirstOrDefault(a => a.KnownProductId == knownProductId);
    public static GoToolsArticle? ForUrl(string url) => All.FirstOrDefault(a => a.Url == url);
}
