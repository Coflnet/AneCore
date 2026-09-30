using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.TrainingListings;

/// <summary>
/// Masks personal data inside free text: phone numbers (German, Dutch, French, international), e-mail addresses, IBANs and urls to
/// messenger and social profiles become <c>[phone]</c>, <c>[email]</c>, <c>[iban]</c> and <c>[link]</c>. Model numbers, prices, article
/// numbers, sizes, dates and opening hours must stay untouched: a number is only taken for a phone number when it starts with a
/// trunk or country prefix (<c>0</c>, <c>00</c>, <c>+</c>), has 9 to 15 digits, is no date and does not follow an article/EAN label.
/// </summary>
public static class TrainingTextSanitizer
{
    public const string Phone = "[phone]";
    public const string Email = "[email]";
    public const string Iban = "[iban]";
    public const string Link = "[link]";

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex EmailPattern = new(
        @"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}\-]+(?:\.[\p{L}\p{N}\-]+)*\.\p{L}{2,}", Options, Timeout);

    // "name (at) domain.de", "name [at] domain.de"
    private static readonly Regex ObfuscatedEmailPattern = new(
        @"[\p{L}\p{N}._%+\-]+\s*[\(\[\{]\s*(?:at|ät)\s*[\)\]\}]\s*[\p{L}\p{N}\-]+(?:\.[\p{L}\p{N}\-]+)+", Options, Timeout);

    private static readonly Regex LinkPattern = new(
        @"(?<![\p{L}\p{N}@.\-])(?:https?://)?(?:www\.|m\.|web\.)?(?:wa\.me|api\.whatsapp\.com|chat\.whatsapp\.com|whatsapp\.com|t\.me|telegram\.me|telegram\.dog|"
        + @"m\.me|messenger\.com|signal\.me|signal\.group|threema\.id|line\.me|discord\.gg|discord\.com/invite|viber\.com|instagram\.com|"
        + @"facebook\.com|fb\.me|fb\.com|snapchat\.com|tiktok\.com|skype\.com)(?![\p{L}\p{N}.\-]*[\p{L}\p{N}])(?:[/?#][^\s<>""')\]]*)?",
        Options, Timeout);

    private static readonly Regex IbanCandidate = new(
        @"(?<![\p{L}\p{N}])[A-Z]{2}\d{2}(?:[ \-]?[A-Z0-9]{2,4}){3,8}(?![\p{L}\p{N}])", Options, Timeout);

    private static readonly Dictionary<string, int> IbanLengths = new()
    {
        ["DE"] = 22, ["NL"] = 18, ["FR"] = 27, ["AT"] = 20, ["BE"] = 16, ["CH"] = 21, ["LU"] = 20, ["ES"] = 24, ["IT"] = 27,
        ["PT"] = 25, ["GB"] = 22, ["IE"] = 22, ["PL"] = 28, ["DK"] = 18, ["SE"] = 24, ["NO"] = 15, ["FI"] = 18, ["CZ"] = 24, ["LI"] = 21
    };

    // trunk/country prefix, then 7 to 14 more digits with at most one separator between them (space, . - / or a bracket)
    private static readonly Regex PhonePattern = new(
        @"(?<![\p{L}\p{N}_/+,])(?<!\d[.\-])\(?(?:\+\s?\d{1,3}|00\d{1,3}|0)(?:(?:\s?[./\-()]\s?|\s|[()])?\d){7,14}(?![\p{N}]|[.\-/]\d)",
        Options, Timeout);

    private static readonly Regex DateLike = new(@"^\d{1,2}[./]\d{1,2}[./]\d{2,4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // "Art.-Nr. 0815 4711 12", "EAN 0012345678905": identifiers, not phone numbers
    private static readonly Regex IdentifierLabel = new(
        @"\b(?:art(?:ikel)?[\s.\-]*(?:nr|nummer|no|num)?|ean|gtin|upc|sku|mpn|modell?[\s.\-]*(?:nr|nummer)?|serien[\s.\-]*(?:nr|nummer)?|seriennummer|bestell[\s.\-]*(?:nr|nummer)?|order|id|ref\w*|p/n|s/n)[\s.:#\-]*$",
        Options, Timeout);

    /// <summary>The text with personal data masked; null and empty stay as they are.</summary>
    public static string? Mask(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var result = EmailPattern.Replace(text, Email);
        result = ObfuscatedEmailPattern.Replace(result, Email);
        result = LinkPattern.Replace(result, Link);
        result = IbanCandidate.Replace(result, MaskIban);
        var input = result;
        return PhonePattern.Replace(input, m => IsPhone(input, m) ? Phone : m.Value);
    }

    private static bool IsPhone(string text, Match match)
    {
        var digits = match.Value.Count(char.IsDigit);
        if (digits is < 9 or > 15)
            return false;
        if (DateLike.IsMatch(match.Value.Trim('(', ')', ' ')))
            return false;
        var before = text.Substring(Math.Max(0, match.Index - 24), Math.Min(24, match.Index));
        return !IdentifierLabel.IsMatch(before);
    }

    private static string MaskIban(Match match)
    {
        var value = match.Value;
        var alnum = new List<int>(); // index of the last char of the n-th alphanumeric char
        for (var i = 0; i < value.Length; i++)
            if (char.IsLetterOrDigit(value[i]))
                alnum.Add(i);
        // the candidate may have swallowed following words ("... 0130 00 Bank"), so the longest valid prefix wins
        for (var count = Math.Min(34, alnum.Count); count >= 15; count--)
        {
            var text = new StringBuilder(count);
            foreach (var index in alnum.Take(count))
                text.Append(char.ToUpperInvariant(value[index]));
            if (IsValidIban(text.ToString()))
                return Iban + value[(alnum[count - 1] + 1)..];
        }
        return value;
    }

    /// <summary>Length matches the country (known ones) or the ISO 7064 mod 97 check digit is right.</summary>
    internal static bool IsValidIban(string compact)
    {
        if (compact.Length is < 15 or > 34)
            return false;
        if (IbanLengths.TryGetValue(compact[..2], out var length) && length == compact.Length)
            return true;
        var rearranged = compact[4..] + compact[..4];
        var number = new StringBuilder();
        foreach (var c in rearranged)
            number.Append(char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString());
        return BigInteger.Parse(number.ToString()) % 97 == 1;
    }
}
