using System.Text.RegularExpressions;

namespace Coflnet.Ane.Text;

/// <summary>
/// Normalisation of product model names that are written with and without a space ("14 T" / "14T").
/// Only for the query side: listing titles are never rewritten, they are matched against both spellings.
/// </summary>
public static class ModelTokens
{
    // A token ending in a digit directly followed by exactly one letter token ("14 T pro", "5 g", "S23 u").
    // The number token must not be part of a decimal/list ("1.5 m", "1, t"), the letter must stand alone
    // A standalone "x" is a dimension or count ("140 x 200", "2 x Controller") and never joined.
    // ("14 t-shirt", "14 ti" stay as they are). A letter in front of the number ("a 14") is never touched.
    private static readonly Regex NumberThenLetter = new(
        @"(?<![\p{L}\p{N}.,])(?<number>[\p{L}\p{N}]*\p{N})[\p{Zs}\t]+(?<letter>(?![xX])\p{L})(?![\p{L}\p{N}\-_'’])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Joins a number token and a directly following single-letter token: "Xiaomi 14 T pro" becomes
    /// "Xiaomi 14T pro", "5 g" becomes "5g". Everything else is returned unchanged.
    /// </summary>
    public static string JoinModelSuffix(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        return NumberThenLetter.Replace(text, m => m.Groups["number"].Value + m.Groups["letter"].Value);
    }

    /// <summary>True for a token such as "14t" or "s23u": ends with one letter that directly follows a digit, and contains only letters and digits.</summary>
    public static bool IsModelSuffixToken(string token)
        => Regex.IsMatch(token, @"^[\p{L}\p{N}]*\p{N}\p{L}$");

    /// <summary>"14t" becomes "14 t": the spaced spelling of a joined model token.</summary>
    public static string SplitModelSuffix(string token)
        => token[..^1] + " " + token[^1];
}
