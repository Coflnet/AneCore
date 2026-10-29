namespace Coflnet.Ane.ShopReferences;

/// <summary>Public factual identity of one verified single-tool merchant article, without a cached price.</summary>
public sealed record GoToolsArticle(string KnownProductId, string Mpn, string Gtin, string MerchantSku, string Url);

public static class GoToolsArticles
{
    public static IReadOnlyList<GoToolsArticle> All { get; } = Array.AsReadOnly<GoToolsArticle>([
        new("knipex-0301160", "03 01 160", "4003773013235", "35930", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-160-mm-poliert-kunststoffueberzug_144938_35930"),
        new("knipex-8701250", "87 01 250", "4003773022022", "18450", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-250-mm-spannweite-46-mm-poliert_127452_18450"),
        new("knipex-8603250", "86 03 250", "4003773033837", "18589", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-250-mm-spannweite-52-mm-verchromt-kunststoffueberzug_127591_18589"),
        new("knipex-2612200", "26 12 200", "4003773023142", "18690", "https://www.gotools.de/marken/knipex/knipex-flachrundzange-laenge-200-mm-poliert-gerade-mehrkomponenten-huellen_127692_18690"),
        new("knipex-2615200", "26 15 200", "4003773035022", "51163", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/flachrundzangen-storchschnabelzangen/flachrundzange-storchschnabelzange/flachrundzange-knipex/knipex-flachrundzange-laenge-200-mm-verchromt-gerade-mehrkomponenten-huellen_160171_51163"),
        new("knipex-1106160", "11 06 160", "4003773021933", "18576", "https://www.gotools.de/marken/knipex/knipex-abisolierzange-laenge-160-mm-vde-verchromt-mehrkomponenten-huellen_127578_18576"),
        new("knipex-1242195", "12 42 195", "4003773054580", "18691", "https://www.gotools.de/marken/knipex/knipex-automatikabisolierzange-multistrip-10-laenge-195-mm-0-03-10-awg-32-7-mm_127693_18691"),
        new("knipex-975304", "97 53 04", "4003773028017", "18581", "https://www.gotools.de/marken/knipex/knipex-aderendhuelsenzange-gesamtlaenge-180-mm-0-08-10-0-16-0-awg-28-5-mm_127583_18581"),
        new("knipex-121202", "12 12 02", "4003773048077", "52547", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/abisolierzangen/abisolierzange/praezisionsabisolierzange-knipex/knipex-praezisionsabisolierzange-laenge-195-mm-mehrkomponeten-huellen-0-03-2-08-awg-32-14-mm_161555_52547"),
        new("knipex-0301180", "03 01 180", "4003773013242", "35540", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-180-mm-poliert-kunststoffueberzug_144548_35540"),
        new("knipex-0301200", "03 01 200", "4003773013259", "57238", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/knipex-kombizange-laenge-200-mm-poliert-kunststoffueberzug_166246_57238"),
        new("knipex-975314", "97 53 14", "4003773041474", "18659", "https://www.gotools.de/marken/knipex/knipex-aderendhuelsenzange-gesamtlaenge-180-mm-0-08-10-0-awg-28-7-mm-brueniert-mehrkomponenten-huellen_127661_18659")
    ]);

    public static GoToolsArticle? ForProduct(string? knownProductId) => All.FirstOrDefault(a => a.KnownProductId == knownProductId);
    public static GoToolsArticle? ForUrl(string url) => All.FirstOrDefault(a => a.Url == url);
}
