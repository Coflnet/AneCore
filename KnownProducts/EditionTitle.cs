using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// The tokens of a listing title as <see cref="KnownProductEditions"/> reads them: where a run of edition words stands, and whether it counts. It does not count when
/// it is negated ("keine Limited Edition", "nicht die Zelda Edition"), when an edition word belongs to a game ("Collector's Edition", "Day One Edition") and, for the
/// theme words of an edition, when they are the object of an inclusion word or name a peripheral ("Galaxy Black Controller"): "PS5 + Spider-Man 2" and "Switch OLED mit Zelda" sell a game with the console. The object
/// ends at an extras word ("mit OVP Zelda Edition", "mit 2 Controllern 30th Anniversary") and at a separator ("mit Rechnung - Zelda Edition").
/// </summary>
internal sealed class EditionTitle
{
    /// <summary>The console edition words; together they are <see cref="KnownProductEditions.MarkerRequirement"/>.</summary>
    internal static readonly string[] MarkerWords =
    {
        "edition", "editie", "edicion", "edizione", "sonderedition", "sonderausgabe", "special", "limited", "limitiert", "limitierte", "limitierter", "ltd",
    };

    /// <summary>The words of a limited design without the bare "Edition"; together they are <see cref="KnownProductEditions.LimitedRequirement"/>.</summary>
    internal static readonly string[] LimitedWords =
    {
        "special", "limited", "limitiert", "limitierte", "limitierter", "ltd", "sonderedition", "sonderausgabe",
    };

    private static readonly HashSet<string> MarkerWordSet = new(MarkerWords, StringComparer.Ordinal);

    /// <summary>Peripherals sold in an edition's design on their own ("Galaxy Black Controller", "30th Anniversary DualSense"): the edition words in front of them name the peripheral.</summary>
    private static readonly HashSet<string> PeripheralWords = new(StringComparer.Ordinal)
    {
        "controller", "controllers", "controllern", "dualsense", "dualshock", "joy", "joycon", "joycons", "gamepad",
    };

    /// <summary>Words in front of "Edition" that make it the edition of a game or the disc-less PS5, not a console design.</summary>
    private static readonly HashSet<string> GameEditionWords = new(StringComparer.Ordinal)
    {
        "collector", "collectors", "sammler", "deluxe", "ultimate", "gold", "premium", "goty", "year", "definitive", "complete", "standard", "launch", "legacy",
        "remastered", "steelbook", "digital", "disc", "disk", "blu", "ray",
    };

    private static readonly HashSet<string> NegationWords = new(StringComparer.Ordinal)
    {
        "kein", "keine", "keinen", "keiner", "nicht", "no", "not", "non", "ohne", "without", "sans", "senza", "sin", "geen", "niet",
    };

    /// <summary>Words a negation may skip on its way to the edition words: "nicht DIE Zelda Edition", "keine LIMITED Edition".</summary>
    private static readonly HashSet<string> NegationFillerWords = new(StringComparer.Ordinal)
    {
        "die", "der", "das", "den", "the", "a", "an", "ein", "eine", "einer", "la", "le", "les", "de", "het",
    };

    /// <summary>Words that add something else to the offer ("+" is normalised to "plus").</summary>
    private static readonly HashSet<string> InclusionWords = new(StringComparer.Ordinal)
    {
        "plus", "mit", "with", "inkl", "inklusive", "incl", "inclusive", "including", "includes", "avec", "met", "con", "samt", "sowie", "inclusief",
    };

    /// <summary>The extras an inclusion word names before the console's own words go on: packaging, receipt, accessories, a number of games.</summary>
    private static readonly HashSet<string> ExtrasWords = new(StringComparer.Ordinal)
    {
        "ovp", "karton", "verpackung", "originalverpackung", "box", "anleitung", "rechnung", "quittung", "kassenbon", "beleg", "garantie", "zubehor",
        "controller", "controllern", "controllers", "joy", "con", "cons", "joycon", "joycons", "kabel", "kabeln", "ladekabel", "netzteil", "dock",
        "tasche", "hulle", "case", "schutzhulle", "panzerglas", "schutzfolie", "speicherkarte", "karte", "headset", "spiele", "spielen", "games",
    };

    /// <summary>Separators of the raw title that end the object of an inclusion word: comma, semicolon, bar, brackets and a free-standing dash.</summary>
    private static readonly Regex Separator = new(@"[,;|()\[\]]|\s[-–—]+\s", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string[] tokens;
    private readonly int aliasEnd;
    private readonly int[]? segments;

    internal EditionTitle(string rawTitle, string[] tokens, int aliasEnd)
    {
        this.tokens = tokens;
        this.aliasEnd = Math.Max(aliasEnd, 0);
        segments = SegmentsOf(rawTitle, tokens.Length);
    }

    /// <summary>The title names <paramref name="words"/> in a place where they count (see the class summary).</summary>
    internal bool Names(string[] words, bool isMarker, bool isThemeWord)
    {
        for (var start = 0; start + words.Length <= tokens.Length; start++)
        {
            if (!tokens.AsSpan(start, words.Length).SequenceEqual(words))
                continue;
            if (IsNegated(start) || isMarker && NamesAGameEdition(start))
                continue;
            if (isThemeWord && (IsInclusionObject(start) || NamesAPeripheral(start + words.Length)))
                continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// A peripheral word right behind the words, or behind one more word that is no inclusion or edition word ("Galaxy Black Controller", "30th Anniversary DualSense"):
    /// the words name the design of a peripheral. "30th Anniversary mit Controller" sells the console with a controller.
    /// </summary>
    private bool NamesAPeripheral(int end)
    {
        for (var i = end; i < tokens.Length && i <= end + 1; i++)
        {
            if (PeripheralWords.Contains(tokens[i]))
                return true;
            if (InclusionWords.Contains(tokens[i]) || MarkerWordSet.Contains(tokens[i]))
                return false;
        }
        return false;
    }

    /// <summary>The segment of each token between separators of the raw title; null when the raw title does not split into the same tokens.</summary>
    private static int[]? SegmentsOf(string rawTitle, int tokenCount)
    {
        var segments = new List<int>(tokenCount);
        var parts = Separator.Split(rawTitle ?? "");
        for (var part = 0; part < parts.Length; part++)
        {
            var count = KnownProductMatcher.Normalize(parts[part]).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            segments.AddRange(Enumerable.Repeat(part, count));
        }
        return segments.Count == tokenCount ? segments.ToArray() : null;
    }

    /// <summary>A negation word in front of the words, past articles and other edition words ("keine Limited Edition", "nicht die Zelda Edition").</summary>
    private bool IsNegated(int start)
    {
        for (var i = start - 1; i >= 0; i--)
        {
            if (NegationWords.Contains(tokens[i]))
                return true;
            if (!NegationFillerWords.Contains(tokens[i]) && !MarkerWordSet.Contains(tokens[i]))
                return false;
        }
        return false;
    }

    /// <summary>The edition word belongs to a game or the disc-less PS5: "Deluxe Edition", "Collector's Edition" ("collector s edition"), "Day One Edition", "Digital Edition".</summary>
    private bool NamesAGameEdition(int start)
    {
        if (start == 0)
            return false;
        var previous = tokens[start - 1];
        var beforePrevious = start >= 2 ? tokens[start - 2] : "";
        return previous switch
        {
            "s" => beforePrevious.StartsWith("collector", StringComparison.Ordinal),
            "one" => beforePrevious == "day",
            _ => GameEditionWords.Contains(previous),
        };
    }

    /// <summary>
    /// The words are the object of an inclusion word after the device ("+ Spider-Man 2", "mit Zelda"): an inclusion word comes before them in the same segment, with no
    /// extras word between ("mit OVP Zelda Edition" names the OVP, the Zelda Edition is the console).
    /// </summary>
    private bool IsInclusionObject(int start)
    {
        for (var i = start - 1; i >= aliasEnd; i--)
        {
            if (segments != null && segments[i] != segments[start])
                return false;
            if (ExtrasWords.Contains(tokens[i]))
                return false;
            if (InclusionWords.Contains(tokens[i]))
                return true;
        }
        return false;
    }
}
