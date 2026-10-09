namespace Coflnet.Ane;

/// <summary>
/// Conservative language guess for a short marketplace title, built from function words and marketplace vocabulary that
/// belong to one language only. Used where a marketplace gives no listing country (Vinted catalogue items) so a clearly
/// foreign title is not stamped with the domain's country. Returns null whenever the evidence is weak, tied, or any German
/// word is present: a wrong foreign guess would hide a German listing from German visitors.
/// </summary>
public static class TitleLanguage
{
    // strong = one hit decides (worth 2), weak = needs a second hit (worth 1). Words shared by several languages
    // (con it/es, per, de, brand and model names) are weak or absent.
    private static readonly Dictionary<string, (HashSet<string> Strong, HashSet<string> Weak)> Languages = new()
    {
        ["it"] = (Set("custodia", "scatola", "auricolari", "originali", "nuovo", "nuova", "usato", "usata", "della", "delle",
                      "taglia", "scarpe", "cuffie", "ricarica"),
                  Set("con", "per", "di", "e", "originale")),
        ["fr"] = (Set("avec", "neuf", "neuve", "très", "état", "taille", "chaussures", "d'origine", "première", "génération",
                      "écouteurs", "ecouteurs", "boîte", "d'occasion"),
                  Set("pour", "de", "originale", "boite")),
        ["es"] = (Set("nuevo", "nueva", "talla", "zapatillas", "auriculares", "caja", "cargador"),
                  Set("con", "para", "estado", "usado", "de")),
        ["nl"] = (Set("maat", "nieuw", "schoenen", "oplaadcase", "generatie", "zgan", "hoesje", "oordopjes"),
                  Set("met", "voor", "een")),
        ["pl"] = (Set("rozmiar", "nowe", "nowy", "nowa", "buty", "słuchawki", "sluchawki", "oryginalne"),
                  Set("stan")),
    };

    private static readonly HashSet<string> German = Set("mit", "und", "neu", "größe", "grösse", "groesse", "schuhe", "kopfhörer",
        "kopfhoerer", "ovp", "gebraucht", "für", "fuer", "zum", "zur", "ohne", "verkauf", "inkl", "ladecase", "neuwertig");

    private static HashSet<string> Set(params string[] words) => new(words, StringComparer.Ordinal);

    /// <summary>Two-letter language (it, fr, es, nl, pl) or null when unsure or German.</summary>
    public static string? Guess(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;
        var words = Tokenize(title);
        if (words.Any(German.Contains))
            return null;

        string? best = null;
        var bestScore = 0;
        var tie = false;
        foreach (var (language, (strong, weak)) in Languages)
        {
            var score = words.Count(strong.Contains) * 2 + words.Count(weak.Contains);
            if (score > bestScore) { best = language; bestScore = score; tie = false; }
            else if (score == bestScore && score > 0) tie = true;
        }
        return bestScore >= 2 && !tie ? best : null;
    }

    /// <summary>Country for a guessed language, null for languages that do not point at one country.</summary>
    public static string? CountryFor(string? language) => language switch
    {
        "it" => "IT",
        "fr" => "FR",
        "es" => "ES",
        "nl" => "NL",
        "pl" => "PL",
        _ => null,
    };

    private static List<string> Tokenize(string title)
    {
        var normalized = title.ToLowerInvariant().Replace('’', '\'').Replace('`', '\'').Replace('´', '\'');
        var words = new HashSet<string>(StringComparer.Ordinal);
        var current = new System.Text.StringBuilder();
        void Flush()
        {
            if (current.Length > 0) words.Add(current.ToString().Trim('\''));
            current.Clear();
        }
        foreach (var c in normalized)
        {
            if (char.IsLetter(c) || c == '\'') current.Append(c);
            else Flush();
        }
        Flush();
        return words.Where(w => w.Length > 0).ToList();
    }
}
