using System.Text.RegularExpressions;
using Coflnet.Ane.Text;

namespace Coflnet.Ane.ReferenceListings;

/// <summary>
/// Turns a listing title ("Xiaomi 14 T pro Smartphone - Neuwertig in OVP") into the product query
/// ("Xiaomi 14T pro") used as the suggested search term. Only condition, packaging and site noise is
/// dropped; every other word of every segment stays, so models and value words (defect, editions,
/// capacity, grading) are never lost. Ambiguous words ("new", "top", "in") are only removed as part
/// of a condition phrase, so "Top Gun", "Made in Abyss" and "New Nintendo 3DS XL" stay intact.
/// </summary>
public static class ReferenceProductQuery
{
    private const int MaxLength = 140;
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string B = @"(?<![\p{L}\p{N}'’])";
    private const string E = @"(?![\p{L}\p{N}'’])";

    private static readonly Regex SegmentSeparator = new(@"\s+[-–—|]\s+", RegexOptions.Compiled);

    // Phrases are condition/packaging only when written together.
    private static readonly Regex ConditionPhrase = new(
        B + @"(?:(?:neu|new)\s*(?:und|&|in|mit|im)\s+(?:ovp|originalverpackung|box|karton)"
        + @"|wie\s+neu|mint\s+condition"
        + @"|(?:in\s+|im\s+)?(?:sehr\s+)?(?:gut(?:er|em)?|top|einwandfrei(?:er|em)?)\s+zustand"
        + @"|sehr\s+gut(?:er|em)?|(?:in|mit)\s+(?:ovp|originalverpackung|original\s+verpackung))" + E, Opts);

    // Words that are condition, packaging, a category noun or the marketplace itself, wherever they stand.
    private static readonly HashSet<string> NoiseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "neu", "neuwertig", "neuwertige", "neuwertiger", "neuwertiges", "ovp", "originalverpackt", "originalverpackung",
        "verpackt", "gebraucht", "used", "unbenutzt", "inkl", "inklusive",
        "kleinanzeigen", "ebay-kleinanzeigen", "kleinanzeigen.de", "vinted", "willhaben", "marktplaats", "autoscout24",
        "shpock", "quoka", "tutti.ch", "ricardo", "leboncoin", "markt.de", "mobile.de", "marketplace",
    };

    // Words that only mean "condition" on their own segment ("Sony PS5 - Neu", "Top").
    private static readonly HashSet<string> WeakWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "new", "top", "mint", "like", "good", "very", "gut", "sehr", "in", "mit", "wie", "und", "and", "with", "zustand", "condition", "boxed",
    };

    // Condition in English only when it ends the segment: "Sting - Brand New Day" and "Like New Vinyl" are names.
    private static readonly Regex TrailingCondition = new(
        B + @"(?:(?:like|as)\s+new|brand\s+new)(?:\s+boxed)?[\s,.!]*$", Opts);

    // Category nouns are noise after the product name, but the leading word of a title can be its name ("Handy Manny").
    private static readonly HashSet<string> CategoryNouns = new(StringComparer.OrdinalIgnoreCase) { "smartphone", "handy" };

    private static readonly Regex BoxedWord = new(B + @"boxed" + E + @"(?!\s+(?:set|edition|collection|version))", Opts);
    private static readonly char[] EdgePunctuation = [',', ';', ':', '!', '(', ')'];

    /// <summary>Returns the product query for an already de-noised title; the title with separators collapsed if only noise remains.</summary>
    public static string FromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var segments = SegmentSeparator.Split(title.Trim())
            .Select((segment, index) => CleanSegment(segment, index == 0))
            .Where(s => s.Length > 0);
        var query = string.Join(' ', segments).Trim('-', '|', ':', ',', '.', ' ');
        if (query.Length == 0)
            query = SegmentSeparator.Replace(title.Trim(), " ");
        return query.Length > MaxLength ? query[..MaxLength].Trim() : query;
    }

    private static string CleanSegment(string segment, bool isFirstSegment)
    {
        var text = ConditionPhrase.Replace(segment, " ");
        text = BoxedWord.Replace(text, " ");
        text = TrailingCondition.Replace(text, " ");
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim(EdgePunctuation)).Where(t => t.Length > 0).ToList();
        var kept = tokens.Where((t, i) => !IsNoise(t, i == 0 && isFirstSegment, i + 1 < tokens.Count ? tokens[i + 1] : null)).ToList();
        if (kept.All(t => WeakWords.Contains(t)))
            return string.Empty;
        return ModelTokens.JoinModelSuffix(string.Join(' ', kept));
    }

    private static bool IsNoise(string token, bool isTitleStart, string? next)
    {
        var word = token.TrimEnd('.');
        if (NoiseWords.Contains(word))
            return true;
        if (!CategoryNouns.Contains(word))
            return false;
        return !isTitleStart && !(next != null && char.IsUpper(next[0]) && !NoiseWords.Contains(next.TrimEnd('.')));
    }
}
