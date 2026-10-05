using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

/// <summary>A part number found in text, as written and with the confidence of how it was recognised. <see cref="Labelled"/> when read behind an explicit label.</summary>
public sealed record PartNumberMention(string Value, double Confidence, string Evidence)
{
    public string Key => PartNumbers.Key(Value);
    public bool Labelled => Confidence >= PartNumbers.LabelledConfidence;
}

/// <summary>
/// Manufacturer part numbers in listing text. Three ways to find one, all bounded by word boundaries and a terminating separator so prose is never captured: an explicit label
/// ("Teile-Nr. 1 619 P08 924", "Part No: 2609110334", "Art.-Nr. 1600A00F7V") gives <see cref="LabelledConfidence"/>; the Bosch typographic format with its spaces ("1 619 P08 924",
/// "2 609 110 334") 0.85; a bare code 0.6 only when it is clearly a code: letters and digits mixed, at least five digits, an upper case letter or a separator in the original
/// (a lower case "hans12345" is a user name). A token is never a part number when it is a pure digit run that is not labelled in a known manufacturer format (phone numbers), when a
/// contact word (Kontakt, Tel, WhatsApp, ...) stands shortly before it, or when it looks like a price, a year, a capacity or a measure (mAh, GB, W, V, mm). <see cref="Key"/> is the
/// comparison form: upper case letters and digits only.
/// </summary>
public static class PartNumbers
{
    public const double LabelledConfidence = 0.95;
    public const int MinKeyLength = 5, MaxKeyLength = 20;

    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    // the value: code characters, and a space only when the next segment holds a digit ("1 619 P08 924"), never into a prose word
    private const string Value = @"(?<pn>[A-Z0-9](?:[A-Z0-9\-./]|\s(?=[A-Z0-9]{0,3}\d[A-Z0-9]{0,3}(?![A-Z0-9])))*[A-Z0-9])(?![\p{L}\p{N}])";

    // "Teile-Nr.", "Teilenummer", "Ersatzteilnummer", "Art.-Nr.", "Artikelnummer", "Bestellnummer", "Sachnummer", "Part No", "Part Number", "Part #", "P/N", "PN", "Ref.", "Référence", "Codice", "Referencia", "Order No", "OEM", "MPN"
    private static readonly Regex Labelled = new(
        @"(?<![\p{L}\p{N}])(?:(?:ersatz)?teile?[\s.\-]*(?:nr|nummer|no)|art(?:ikel)?[\s.\-]*(?:nr|nummer|no)|bestell[\s.\-]*(?:nr|nummer)|sach[\s.\-]*(?:nr|nummer)|material[\s.\-]*(?:nr|nummer)|" +
        @"part[\s.\-]*(?:no|number|nr|#)|p/n|pn|ref(?:erence|érence|erenz)?|codice|referencia|order[\s.\-]*(?:no|number)|oem[\s.\-]*(?:no|nr|number)?|mpn)" +
        @"(?![\p{L}])\.?\s*[:#]?\s*" + Value,
        Opts);

    // Bosch style with its typographic spaces: 1 619 P08 924, 2 609 110 334
    private static readonly Regex Bosch = new(@"(?<![\p{L}\p{N}])(?<pn>[123]\s6\d{2}\s[A-Z0-9]{3}\s[A-Z0-9]{3,4})(?![\p{L}\p{N}])", Opts);
    // a 10 digit Bosch number without spaces is only taken behind a label
    private static readonly Regex BoschPlain = new(@"^[123]6\d{8}$", Opts);

    // a bare code: letters and digits, 6 to 20 characters, hyphen/slash/dot inside, must contain both a letter and a digit
    private static readonly Regex Bare = new(@"(?<![\p{L}\p{N}#@])(?=[A-Z0-9\-./]{6,20}(?![\p{L}\p{N}]))(?=[A-Z0-9\-./]*\d)(?=[A-Z0-9\-./]*[A-Z])(?<pn>[A-Z0-9][A-Z0-9\-./]{4,18}[A-Z0-9])", Opts);

    // what a value must not be: a measure or a unit ("35MM", "18V", "42T", "M1.5", "1.5KW", "230V", "2000W", "10X30", "6.35MM", "5000MAH", "64GB"), a size ("Gr.42"), a price, a year, a date or an URL piece
    private static readonly Regex NotACode = new(
        @"^(?:\d+(?:[.,]\d+)?\s*(?:x\s*\d+(?:[.,]\d+)?\s*)*(?:mm|cm|m|v|w|kw|a|ah|mah|wh|kwh|gb|tb|mb|nm|rpm|u/min|hz|ghz|mhz|kg|g|t|z|st|stk|x|er|pcs|pc|tlg|teilig|mal|ps|hp|l|ml|%|eur|euro|€|zoll|inch|"")" +
        @"|m\d{1,2}(?:[.,]\d+)?|\d{1,2}[./]\d{1,2}[./]\d{2,4}|(?:19|20)\d{2}|\d+[.,]\d{2}|www\..*|.*\.(?:de|com|at|ch|nl|html?))$", Opts);

    // the words after which a code is contact data, within the 30 characters before it
    private static readonly Regex ContactBefore = new(@"(?:kontakt|contact|tel|telefon|telephone|phone|handy|mobil|mobile|whatsapp|signal|telegram|sms|anruf|anrufen|call|ruf|nummer|number)\W*\S*\W*$", Opts);
    private static readonly Regex MoneyAfter = new(@"^\s*(?:€|eur|euro|chf|\$|vb|vhb|fp|festpreis)", Opts);

    private static readonly Regex NonKey = new(@"[^A-Z0-9]", Opts);

    /// <summary>The comparison key: upper case letters and digits only ("1 619 P08 924" and "1619p08924" are one key).</summary>
    public static string Key(string partNumber) => NonKey.Replace(partNumber.ToUpperInvariant(), "");

    /// <summary>The part numbers in <paramref name="text"/> (original casing), best first, without duplicates by key. At most <paramref name="max"/>.</summary>
    public static IReadOnlyList<PartNumberMention> Find(string? text, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var found = new List<PartNumberMention>();

        void Add(Match m, string raw, double confidence, string how)
        {
            var value = raw.Trim().Trim('.', '-', '/', ':', ',');
            var key = Key(value);
            if (key.Length < MinKeyLength || key.Length > MaxKeyLength || !key.Any(char.IsDigit))
                return;
            if (key.Count(char.IsDigit) < 2)
                return;
            var compact = value.Replace(" ", "");
            if (NotACode.IsMatch(compact))
                return;
            if (key.All(char.IsDigit))
            {
                // a pure digit run is only a part number behind a label (Miele "Teile-Nr. 5091740") or in the spaced Bosch format; never when it looks like a phone number
                if (confidence < LabelledConfidence && !raw.Contains(' '))
                    return;
                if (key.Length > 10 || key[0] == '0' || raw.TrimStart().StartsWith('+'))
                    return;
                if (!BoschPlain.IsMatch(key) && key.Length >= 9)
                    return;
            }
            var start = Math.Max(0, m.Index - 30);
            if (ContactBefore.IsMatch(text.Substring(start, m.Index - start)))
                return;
            var end = m.Index + m.Length;
            if (end < text.Length && MoneyAfter.IsMatch(text.Substring(end, Math.Min(12, text.Length - end))))
                return;
            if (found.Any(f => f.Key == key))
                return;
            found.Add(new PartNumberMention(value, confidence, PartText.Evidence($"part number {how}: \"{value}\"") ?? ""));
        }

        foreach (Match m in Labelled.Matches(text))
            Add(m, m.Groups["pn"].Value, LabelledConfidence, "labelled");
        foreach (Match m in Bosch.Matches(text))
            Add(m, m.Groups["pn"].Value, 0.85, "manufacturer format");
        foreach (Match m in Bare.Matches(text))
        {
            var value = m.Groups["pn"].Value;
            if (value.Count(char.IsDigit) < 5)
                continue;
            // a code is written in upper case or with separators; "hans12345" is a user name
            if (!value.Any(char.IsUpper) && !value.Any(c => c is '-' or '/' or '.') && !char.IsDigit(value[0]))
                continue;
            Add(m, value, 0.6, "bare code");
        }
        return found.OrderByDescending(f => f.Confidence).Take(max).ToList();
    }
}
