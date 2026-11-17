using System.Text.RegularExpressions;

namespace Coflnet.Ane.ReferenceListings;

/// <summary>Price-token parsing helpers and removal of non-item amounts (shipping costs, unit prices).</summary>
internal static class ReferencePriceText
{
    internal const string CurrencyPattern = @"€|EUR|CHF|US\$|\$|£|GBP|USD|PLN|zł|CZK|Kč|DKK|SEK|HUF|RON|BGN|NOK";
    private const string Money = @"(?:(?:" + CurrencyPattern + @")\s*\d[\d.,]*|\d[\d.,]*\s*(?:" + CurrencyPattern + @"))";
    private const string ShippingWord = @"(?:Versand(?:kosten)?|Lieferkosten|shipping(?:\s+costs?)?|delivery|postage|porto)";
    private const string IncludedWord = @"(?:ab|from|:|kostenfrei|kostenlos|gratis|frei|free|inkl|inklusive|incl|including|included|möglich|moeglich|available|possible)";
    private const string UnitMeasure = @"(?:kg|mg|g|ml|cl|l|cm|qm|m2|m²|lfm|m)(?![a-zäöüß0-9])";
    private const string UnitPiece = @"(?:stück|stk\.?|st\.|pieces?|pcs?|unit|einheit)(?![a-zäöüß])";

    internal static readonly Regex PriceRegex = new(
        @"(?:(?<currency>" + CurrencyPattern + @")\s*)?(?<amount>\d{1,3}(?:[.,\s ]\d{3})+(?:[,\.]\d{1,2})?|\d+(?:[,\.]\d{1,2})?)(?:\s*(?<currency2>" + CurrencyPattern + @"))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Only shipping COST phrasing: "+ Versand", "zzgl. Versand", "Versand ab 0,49 €", "shipping: €3", "30 € Versand".
    // "inkl. Versand", "versandkostenfrei" and "free delivery" describe the item price and stay.
    private static readonly Regex ShippingCostRegex = new(
        @"(?:(?:\+|zzgl\.?|zuzüglich|plus|excl(?:uding|\.)?|exkl(?:usive|\.)?)\s*" + ShippingWord + @"\b(?:\s*(?:ab|from|:))?\s*" + Money + @"?)"
        + @"|(?<!(?:incl\.?|inkl\.?|including|inklusive)\s)\b" + ShippingWord + @"\s*(?:ab|from|:)\s*" + Money
        + @"|" + Money + @"\s*\b" + ShippingWord + @"\b(?!\s*" + IncludedWord + @")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string UnitAny = @"(?:" + UnitMeasure + "|" + UnitPiece + ")";

    // "25,98 € / 1 kg" and "12,99 € / Stück": the item price itself may be per measure, so these are
    // unit prices only inside parentheses or after another price.
    private static readonly Regex UnitPerRegex = new(
        @"(?<open>\()?\s*" + Money + @"\s*(?:/|pro|per)\s*\d*\s*" + UnitAny + @"\.?\s*\)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "(1 kg = 25,98 €)" is always a unit price.
    private static readonly Regex UnitEqualsRegex = new(
        @"\(?\s*\d+\s*" + UnitAny + @"\s*=\s*" + Money + @"\s*\)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnitLabelRegex = new(
        @"(?:Grundpreis|Basispreis|base\s+price|unit\s+price)\s*:?\s*(?:" + Money + @"(?:\s*(?:/|pro|per)\s*\d*\s*[a-zäöü²0-9.]+)?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Text with shipping costs and unit prices removed; what remains describes the item price.</summary>
    internal static string Clean(string text)
    {
        var cleaned = UnitEqualsRegex.Replace(text, " ");
        cleaned = UnitPerRegex.Replace(cleaned, m => IsUnitPrice(cleaned, m) ? " " : m.Value);
        cleaned = UnitLabelRegex.Replace(cleaned, " ");
        cleaned = ShippingCostRegex.Replace(cleaned, " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim(' ', ',', ';', '+', '(', ')');
    }

    private static bool IsUnitPrice(string text, Match match)
        => match.Groups["open"].Success || CountPriceTokens(text[..match.Index]) > 0;

    internal static int CountPriceTokens(string text)
        => PriceRegex.Matches(text).Count(m => m.Groups["currency"].Length > 0 || m.Groups["currency2"].Length > 0);
}
