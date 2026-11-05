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
        new("knipex-975314", "97 53 14", "4003773041474", "18659", "https://www.gotools.de/marken/knipex/knipex-aderendhuelsenzange-gesamtlaenge-180-mm-0-08-10-0-awg-28-7-mm-brueniert-mehrkomponenten-huellen_127661_18659"),
        new("knipex-7006180", "70 06 180", "4003773033813", "18571", "https://www.gotools.de/marken/knipex/knipex-seitenschneider-laenge-180-mm-mehrkomponenten-huellen_127573_18571"),
        new("knipex-2616200", "26 16 200", "4003773022831", "18574", "https://www.gotools.de/marken/knipex/knipex-flachrundzange-laenge-200-mm-gerade-vde-mehrkomponenten-huellen_127576_18574"),
        new("knipex-2626200", "26 26 200", "4003773022855", "18575", "https://www.gotools.de/marken/knipex/knipex-flachrundzange-laenge-200-mm-40-gewinkelt-vde-mehrkomponenten-huellen_127577_18575"),
        new("knipex-1240200", "12 40 200", "4003773026662", "18578", "https://www.gotools.de/marken/knipex/knipex-automatikabisolierzange-laenge-200-mm-0-03-10-awg-32-7-mm_127580_18578"),
        new("knipex-975236", "97 52 36", "4003773051893", "18579", "https://www.gotools.de/marken/knipex/knipex-crimpzange-preciforce-laenge-220-mm-0-5-6-awg-20-10-mm-487-g_127581_18579"),
        new("knipex-975309", "97 53 09", "4003773044550", "18580", "https://www.gotools.de/marken/knipex/knipex-crimpzange-gesamtlaenge-190-mm-0-08-10-0-16-awg-28-5-mm-brueniert-2-komponenten-huellen_127582_18580"),
        new("knipex-8701125", "87 01 125", "4003773069935", "18583", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-125-mm-spannweite-27-mm-poliert-kunststoffueberzug_127585_18583"),
        new("knipex-8701560", "87 01 560", "4003773044321", "18585", "https://www.gotools.de/marken/knipex/knipex-rohr-und-wasserpumpenzange-cobra-laenge-560-mm-spannweite-120-mm-poliert-kunststoffueberzug_127587_18585"),
        new("knipex-8701150", "87 01 150", "4003773060116", "18590", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-150-mm-spannweite-30-mm_127592_18590"),
        new("knipex-8603150", "86 03 150", "4003773069676", "18660", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-150-mm-spannweite-27-mm-verchromt-kunststoffueberzug_127662_18660"),
        new("knipex-8701400", "87 01 400", "4003773005636", "18661", "https://www.gotools.de/marken/knipex/knipex-rohr-und-wasserpumpenzange-cobra-laenge-400-mm-spannweite-95-mm-poliert-kunststoffueberzug_127663_18661"),
        new("knipex-8603180", "86 03 180", "4003773035466", "18663", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-180-mm-spannweite-40-mm-verchromt-kunststoffueberzug_127665_18663"),
        new("knipex-8603300", "86 03 300", "4003773041429", "18664", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/sanitaerwerkzeuge/zangenschluessel/knipex-zangenschluessel-laenge-300-mm-spannweite-60-mm-verchromt-kunststoffueberzug_127666_18664"),
        new("knipex-7002160", "70 02 160", "4003773034032", "18689", "https://www.gotools.de/marken/knipex/knipex-seitenschneider-laenge-160-mm-kopf-poliert-mehrkomponenten-huellen-max-2mm_127691_18689"),
        new("knipex-8701180", "87 01 180", "4003773022015", "18693", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-180-mm-spannweite-36-mm_127695_18693"),
        new("knipex-9536280", "95 36 280", "4003773026891", "18732", "https://www.gotools.de/marken/knipex/knipex-kabelschneider-gesamtlaenge-280-mm-max-52-380-mm-mm-mehrkomponenten-huellen-vde_127734_18732"),
        new("knipex-8702250", "87 02 250", "4003773040316", "18791", "https://www.gotools.de/marken/knipex/knipex-wasserpumpenzange-cobra-laenge-250-mm-spannweite-46-mm-poliert-mehrkomponenten-huellen_127793_18791"),
        new("knipex-8703250", "87 03 250", "4003773043805", "29360", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/wasserpumpenzange/wasserpumpenzange-cobra-knipex/knipex-wasserpumpenzange-cobra-laenge-250-mm-spannweite-46-mm-verchromt-kunststoffueberzug_138364_29360"),
        new("knipex-9721215c", "97 21 215 C", "4003773019701", "30072", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/crimpzangen-und-zubehoer/crimpzangen/crimpzange-knipex/knipex-crimpzange-laenge-230-mm-0-5-6-awg-20-10-mm_139076_30072"),
        new("knipex-975230", "97 52 30", "4003773051855", "30176", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/crimpzangen-und-zubehoer/crimpzangen/crimpzange-preciforce-knipex/knipex-crimpzange-preciforce-laenge-220-mm-1-5-10-awg-15-11-mm-477-g_139180_30176"),
        new("knipex-975233", "97 52 33", "4003773051862", "30187", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/crimpzangen-und-zubehoer/crimpzangen/crimpzange-preciforce-knipex/knipex-crimpzange-preciforce-laenge-220-mm-0-5-10-awg-20-7-mm-478-g_139191_30187"),
        new("knipex-7006125", "70 06 125", "4003773018124", "31064", "https://www.gotools.de/werkstattbedarf/handwerkzeuge/zangen/seitenschneider/seitenschneider/seitenschneider-knipex/knipex-seitenschneider-laenge-125-mm-vde-mehrkomponenten-huellen_140068_31064")
    ]);

    public static GoToolsArticle? ForProduct(string? knownProductId) => All.FirstOrDefault(a => a.KnownProductId == knownProductId);
    public static GoToolsArticle? ForUrl(string url) => All.FirstOrDefault(a => a.Url == url);
}
