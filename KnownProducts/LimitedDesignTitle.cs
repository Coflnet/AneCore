using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Whether a console title names a limited or anniversary design of the console itself ("Nintendo Switch OLED Limited Edition", "Xbox Series X 25th Anniversary",
/// "Xbox series X versione Diablo (limited edition)"), for <see cref="KnownProductEditions.NamesLimitedDesign"/>. A wrong yes hides an ordinary offer, so the words count
/// only where they describe the console:
/// <list type="bullet">
/// <item>in the console's own part of the title: before the first comma, semicolon, bar or colon and before the first inclusion, peripheral or game-list word
/// ("PS5 Slim mit 2 Controllern (einer davon Limited Edition)", "PS5 Slim 1TB, DualSense Cobalt Limited Edition", "2 Controller Spiele: Tomb Raider Anniversary");</item>
/// <item>not negated and not a game's edition (<see cref="EditionTitle.CountsAt"/>);</item>
/// <item>not naming a peripheral, a game collection, a steelbook or a shop's stock or offer right behind ("Limited Edition Controller dabei", "30th Anniversary Collection",
/// "limitierte Stückzahl", "limited time deal") and not after a shop word ("Angebot limitiert", "zeitlich limitiert");</item>
/// <item>an anniversary word only right after a number ("30th", "30e", "35.", "25 ans", "25 jähriges"): "cadeau d'anniversaire", "Jubileum actie", "Halo Anniversary" and
/// "Super Mario Bros 35 Anniversary" are no console design.</item>
/// </list>
/// "ltd" alone is shop wording and does not count.
/// </summary>
internal static class LimitedDesignTitle
{
    private static readonly string[][] LimitedWords =
    {
        new[] { "limited" }, new[] { "limitiert" }, new[] { "limitierte" }, new[] { "limitierter" }, new[] { "sonderedition" }, new[] { "sonderausgabe" },
        new[] { "numbered" }, new[] { "nummeriert" }, new[] { "special", "edition" },
    };

    private static readonly HashSet<string> AnniversaryWords = new(StringComparer.Ordinal)
    {
        "anniversary", "anniversaire", "anniversario", "aniversario", "jubilaum", "jubileum", "jubilee", "jahrestag",
    };

    /// <summary>Ordinal suffixes after a number ("30 th" from "30th", "30 e", "30 eme", "30 o") and the year words of "25 ans", "25 Jahre", "25 jähriges".</summary>
    private static readonly HashSet<string> OrdinalWords = new(StringComparer.Ordinal)
    {
        "th", "st", "nd", "rd", "e", "er", "eme", "o", "a", "ans", "jahre", "jahrige", "jahriges", "years", "year", "anni", "jaar", "jarig", "anos", "lat",
    };

    /// <summary>Inclusion words ("+" is normalised to "plus"): what follows is sold with the console.</summary>
    private static readonly HashSet<string> InclusionWords = new(StringComparer.Ordinal)
    {
        "plus", "mit", "with", "inkl", "inklusive", "incl", "inclusive", "including", "includes", "avec", "met", "con", "samt", "sowie", "inclusief", "und", "and", "et",
    };

    /// <summary>Peripherals and the games of a list: like the inclusion words they end the console's own part ("2 Controller Spiele: Tomb Raider Anniversary").</summary>
    private static readonly HashSet<string> ListWords = new(StringComparer.Ordinal)
    {
        "controller", "controllers", "controllern", "dualsense", "dualshock", "joy", "joycon", "joycons", "gamepad", "manette", "manettes",
        "spiel", "spiele", "spielen", "games", "giochi", "jeux", "juegos", "gry",
    };

    /// <summary>Words right behind the design words that make them another thing's: a peripheral, a game collection, a steelbook, a shop's stock.</summary>
    private static readonly HashSet<string> OtherThingWords = new(StringComparer.Ordinal)
    {
        "controller", "controllers", "controllern", "dualsense", "dualshock", "joy", "joycon", "joycons", "gamepad", "manette", "headset",
        "collection", "steelbook", "stuckzahl", "stuck", "auflage", "spiel", "spiele", "game", "games", "cover", "covers", "skin", "faceplate",
        // a shop's stock or offer: "limited stock", "LIMITED OFFER", "limited time deal", "limited availability", "limitierte Menge"
        "stock", "offer", "offers", "deal", "deals", "time", "availability", "available", "supply", "quantity", "quantities", "angebot", "aktion",
        "verfugbar", "verfugbarkeit", "menge", "zeit", "zeitraum",
    };

    /// <summary>A shop word right before the design word: "Angebot limitiert", "zeitlich limitiert", "Weihnachtsangebot limitiert", "offer limited".</summary>
    private static readonly HashSet<string> ShopWordsBefore = new(StringComparer.Ordinal)
    {
        "zeitlich", "angebot", "aktion", "offer", "deal", "stuckzahl", "menge", "verfugbarkeit",
    };

    private static readonly HashSet<string> EditionMarkerWords = new(EditionTitle.MarkerWords, StringComparer.Ordinal);

    /// <summary>The first comma, semicolon, bar or colon of the raw title; brackets and dashes stay inside ("versione Diablo (limited edition)").</summary>
    private static readonly Regex PartSeparator = new(@"[,;|:]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A German ordinal ("35. Jubiläum"): the dot is the ordinal suffix, rewritten so the tokens read "35 th".</summary>
    private static readonly Regex DotOrdinal = new(@"(?<![\d.,])(\d{1,3})\.(?=\s|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The title names a limited or anniversary design of the console itself (see the class summary).</summary>
    internal static bool Names(string? title)
    {
        var withOrdinals = DotOrdinal.Replace(title ?? "", "$1th");
        var tokens = KnownProductMatcher.Normalize(withOrdinals).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var consoleEnd = ConsolePartEnd(withOrdinals, tokens);
        if (consoleEnd == 0)
            return false;
        var edition = new EditionTitle(withOrdinals, tokens, 0);
        return NamesLimitedWord(tokens, consoleEnd, edition) || NamesNumberedAnniversary(tokens, consoleEnd, edition);
    }

    /// <summary>The number of tokens of the console's own part: those before the first separator and before the first inclusion, peripheral or game-list word.</summary>
    private static int ConsolePartEnd(string title, string[] tokens)
    {
        var separator = PartSeparator.Match(title);
        var end = separator.Success ? KnownProductMatcher.Normalize(title[..separator.Index]).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length : tokens.Length;
        var word = Array.FindIndex(tokens, 0, Math.Min(end, tokens.Length), token => InclusionWords.Contains(token) || ListWords.Contains(token));
        return word < 0 ? Math.Min(end, tokens.Length) : word;
    }

    private static bool NamesLimitedWord(string[] tokens, int consoleEnd, EditionTitle edition)
    {
        foreach (var words in LimitedWords)
        {
            for (var start = 0; start < consoleEnd && start + words.Length <= tokens.Length; start++)
            {
                if (tokens.AsSpan(start, words.Length).SequenceEqual(words) && Counts(tokens, edition, start, start + words.Length))
                    return true;
            }
        }
        return false;
    }

    private static bool NamesNumberedAnniversary(string[] tokens, int consoleEnd, EditionTitle edition)
    {
        for (var i = 1; i < consoleEnd; i++)
        {
            if (AnniversaryWords.Contains(tokens[i]) && FollowsNumber(tokens, i) && Counts(tokens, edition, i, i + 1))
                return true;
        }
        return false;
    }

    /// <summary>"30 anniversary" is no ordinal ("Super Mario Bros 35 Anniversary"); "30 th anniversary", "25 ans anniversaire" and "35 th jubilaum" (from "35.") are.</summary>
    private static bool FollowsNumber(string[] tokens, int index) =>
        index >= 2 && OrdinalWords.Contains(tokens[index - 1]) && IsNumber(tokens[index - 2]);

    private static bool IsNumber(string token) => token.Length is > 0 and <= 3 && token.All(char.IsAsciiDigit);

    private static bool Counts(string[] tokens, EditionTitle edition, int start, int end) =>
        edition.CountsAt(start, isMarker: true) && !FollowsShopWord(tokens, start) && !NamesOtherThing(tokens, end);

    private static bool FollowsShopWord(string[] tokens, int start) =>
        start > 0 && (ShopWordsBefore.Contains(tokens[start - 1]) || tokens[start - 1].EndsWith("angebot", StringComparison.Ordinal));

    /// <summary>
    /// A peripheral, collection, steelbook or stock word within the two words behind the design words, past further edition words ("Limited Edition Controller dabei");
    /// an inclusion word ends the search ("Limited Edition mit Controller" sells the console with a controller).
    /// </summary>
    private static bool NamesOtherThing(string[] tokens, int end)
    {
        var otherWords = 0;
        for (var i = end; i < tokens.Length && otherWords < 2 && !InclusionWords.Contains(tokens[i]); i++)
        {
            if (OtherThingWords.Contains(tokens[i]))
                return true;
            if (!EditionMarkerWords.Contains(tokens[i]))
                otherWords++;
        }
        return false;
    }
}
