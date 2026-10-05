using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>Text helpers of the part index: folding for matching, the first sentence of a description, a sanitizer for the evidence lines that are stored and shown to reviewers, and a content hash.</summary>
public static class PartText
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly Regex Email = new(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}.\-]+\.[\p{L}]{2,}", Opts);
    // seven or more digits with at most one separator between them: phone numbers ("0151 234 5678", "+49 (0) 151-2345678")
    private static readonly Regex Phone = new(@"(?<!\d)\+?\d(?:[\s./()\-]?\d){6,}", Opts);
    private static readonly Regex Control = new(@"[\p{C}\p{Zl}\p{Zp}]+", Opts);
    private static readonly Regex Spaces = new(@"\s{2,}", Opts);
    private static readonly Regex SentenceEnd = new(@"(?<=[.!?;])\s|\r?\n", Opts);

    public const int MaxEvidence = 300;

    /// <summary>Lower case without diacritics ("Zähne" to "zahne", "ß" to "ss"), whitespace collapsed. Used for every pattern match; quotes in evidence are taken from the original text.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var lower = value.ToLowerInvariant().Replace("ß", "ss");
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
        }
        return Spaces.Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    /// <summary>The first sentence (up to ". ", "! ", "? ", "; " or a line break) of a description, at most <paramref name="maxChars"/> characters; the part of a description that describes the part rather than the seller's terms.</summary>
    public static string FirstSentence(string? description, int maxChars = 200)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "";
        var trimmed = description.TrimStart();
        var parts = SentenceEnd.Split(trimmed, 2);
        var first = parts[0].Trim();
        return first.Length > maxChars ? first[..maxChars] : first;
    }

    /// <summary>One line of at most <see cref="MaxEvidence"/> characters without control characters, e-mail addresses or phone-number-like digit runs.</summary>
    public static string? Evidence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var clean = Email.Replace(text, " ");
        clean = Phone.Replace(clean, " ");
        clean = Control.Replace(clean, " ");
        clean = Spaces.Replace(clean, " ").Trim();
        if (clean.Length > MaxEvidence)
            clean = clean[..(MaxEvidence - 1)].TrimEnd() + "…";
        return clean.Length == 0 ? null : clean;
    }

    /// <summary>Invariant number without trailing zeros ("35", "1.5", "0.75").</summary>
    public static string Num(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Parses "1,5" and "1.5" alike.</summary>
    public static bool TryNum(string? text, out double value) =>
        double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>A short stable hash (16 hex characters of SHA-256) of the joined parts, for change detection.</summary>
    public static string Hash(IEnumerable<string> parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f", parts)));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
