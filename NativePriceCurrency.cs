using System.Globalization;

namespace Coflnet.Ane;

/// <summary>Normalizes explicitly known native currencies without inferring a marketplace default.</summary>
public static class NativePriceCurrency
{
    private static readonly HashSet<string> Codes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(culture =>
        {
            try { return new RegionInfo(culture.Name).ISOCurrencySymbol; }
            catch (ArgumentException) { return ""; }
        })
        .Where(code => code.Length == 3 && code.All(char.IsAsciiLetterUpper) && code != "XXX")
        .ToHashSet(StringComparer.Ordinal);

    public static string? Normalize(string? currency) => currency?.Trim().ToUpperInvariant() is { } code
        && Codes.Contains(code) ? code : null;
}
