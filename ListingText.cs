using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Coflnet.Ane;

/// <summary>
/// Text helpers for search indices: indexed text is kept to short plain-text excerpts.
/// </summary>
public static partial class ListingText
{
    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();

    /// <summary>
    /// Plain-text excerpt of at most <paramref name="maxChars"/> characters: HTML tags are removed, entities decoded,
    /// whitespace collapsed and the text is cut at the last word boundary that fits.
    /// Returns null for empty input or a non-positive limit.
    /// </summary>
    public static string? Excerpt(string? text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrWhiteSpace(text))
            return null;
        var plain = WebUtility.HtmlDecode(HtmlTag().Replace(text, " "));
        var builder = new StringBuilder(Math.Min(plain.Length, maxChars + 1));
        var lastWasSpace = true;
        foreach (var c in plain)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (!lastWasSpace)
                    builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            if (builder.Length > maxChars)
                break;
        }
        var collapsed = builder.ToString().Trim();
        if (collapsed.Length == 0)
            return null;
        if (collapsed.Length <= maxChars)
            return collapsed;
        var cut = collapsed[..maxChars];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace >= maxChars / 2)
            cut = cut[..lastSpace];
        if (char.IsHighSurrogate(cut[^1]))
            cut = cut[..^1];
        return cut.TrimEnd();
    }
}
