using System.Globalization;
using System.Text;

namespace Coflnet.Ane;

/// <summary>
/// German → English Pokémon (TCG) name table, shared so it exists exactly once. AneApi uses this to
/// translate localized card names in search queries (e.g. "glurak vmax" also matching "charizard
/// vmax"). AneNotifier's Extraction/PokemonData.cs carries its own copy of this same table today and
/// should switch to referencing <see cref="GermanToEnglish"/> here instead - see that file's
/// <c>GermanToEnglish</c> member.
/// </summary>
public static class PokemonNames
{
    /// <summary>
    /// German (folded) → English Pokémon name. Identical names (shared between German and English, or
    /// English names themselves) map to themselves, so a lookup never needs to know which language it
    /// was given. Keyed case-insensitively - see <see cref="TryGetEnglishName"/> for a lookup that also
    /// ignores diacritics.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> GermanToEnglish = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["glurak"] = "Charizard", ["glumanda"] = "Charmander", ["glutexo"] = "Charmeleon",
        ["bisasam"] = "Bulbasaur", ["bisaknosp"] = "Ivysaur", ["bisaflor"] = "Venusaur",
        ["schiggy"] = "Squirtle", ["schillok"] = "Wartortle", ["turtok"] = "Blastoise",
        ["mewtu"] = "Mewtwo", ["evoli"] = "Eevee", ["aquana"] = "Vaporeon", ["blitza"] = "Jolteon",
        ["flamara"] = "Flareon", ["psiana"] = "Espeon", ["nachtara"] = "Umbreon", ["folipurba"] = "Leafeon",
        ["glaziola"] = "Glaceon", ["feelinara"] = "Sylveon", ["nebulak"] = "Gastly", ["alpollo"] = "Haunter",
        ["relaxo"] = "Snorlax", ["garados"] = "Gyarados", ["karpador"] = "Magikarp", ["dragoran"] = "Dragonite",
        ["dragonir"] = "Dragonair", ["arktos"] = "Articuno", ["lavados"] = "Moltres", ["knakrack"] = "Garchomp",
        ["guardevoir"] = "Gardevoir", ["despotar"] = "Tyranitar", ["sichlor"] = "Scyther", ["scherox"] = "Scizor",
        ["simsala"] = "Alakazam", ["machomei"] = "Machamp", ["pummeluff"] = "Jigglypuff", ["piepi"] = "Clefairy",
        ["mauzi"] = "Meowth", ["enton"] = "Psyduck", ["vulnona"] = "Ninetales", ["fukano"] = "Growlithe",
        ["arkani"] = "Arcanine", ["chaneira"] = "Chansey", ["heiteira"] = "Blissey", ["tornupto"] = "Typhlosion",
        ["feurigel"] = "Cyndaquil", ["karnimani"] = "Totodile", ["impergator"] = "Feraligatr", ["endivie"] = "Chikorita",
        ["lohgock"] = "Blaziken", ["flemmli"] = "Torchic", ["gewaldro"] = "Sceptile", ["sumpex"] = "Swampert",
        ["hydropi"] = "Mudkip", ["geckarbor"] = "Treecko", ["brutalanda"] = "Salamence", ["panferno"] = "Infernape",
        ["impoleon"] = "Empoleon", ["plinfa"] = "Piplup", ["quajutsu"] = "Greninja", ["mimigma"] = "Mimikyu",
        ["silvarro"] = "Decidueye", ["bauz"] = "Rowlet", ["fuegro"] = "Incineroar", ["primarene"] = "Primarina",
        ["endynalos"] = "Eternatus", ["wulaosu"] = "Urshifu", ["coronospa"] = "Calyrex", ["katapuldra"] = "Dragapult",
        ["liberlo"] = "Cinderace", ["gortrom"] = "Rillaboom", ["intelleon"] = "Inteleon", ["riffex"] = "Toxtricity",
        ["maskagato"] = "Meowscarada", ["skelokrok"] = "Skeledirge", ["bailonda"] = "Quaquaval", ["felori"] = "Sprigatito",
        ["krokel"] = "Fuecoco", ["kwaks"] = "Quaxly", ["monetigo"] = "Gholdengo", ["granforgita"] = "Tinkaton",
        ["azugladis"] = "Ceruledge", ["crimanzo"] = "Armarouge", ["enigmara"] = "Iono", ["pantimos"] = "Mr. Mime",
        ["rossana"] = "Jynx", ["elektek"] = "Electabuzz", ["kangama"] = "Kangaskhan", ["quaputzi"] = "Poliwhirl",
        ["blutmond-ursaluna"] = "Bloodmoon Ursaluna",
        // Names shared between German and English
        ["pikachu"] = "Pikachu", ["raichu"] = "Raichu", ["pichu"] = "Pichu", ["mew"] = "Mew", ["gengar"] = "Gengar",
        ["lapras"] = "Lapras", ["zapdos"] = "Zapdos", ["lugia"] = "Lugia", ["ho-oh"] = "Ho-Oh", ["celebi"] = "Celebi",
        ["rayquaza"] = "Rayquaza", ["groudon"] = "Groudon", ["kyogre"] = "Kyogre", ["latias"] = "Latias", ["latios"] = "Latios",
        ["jirachi"] = "Jirachi", ["deoxys"] = "Deoxys", ["darkrai"] = "Darkrai", ["arceus"] = "Arceus", ["giratina"] = "Giratina",
        ["dialga"] = "Dialga", ["palkia"] = "Palkia", ["lucario"] = "Lucario", ["absol"] = "Absol", ["onix"] = "Onix",
        ["ditto"] = "Ditto", ["porygon"] = "Porygon", ["aerodactyl"] = "Aerodactyl", ["togepi"] = "Togepi",
        ["togekiss"] = "Togekiss", ["marill"] = "Marill", ["ampharos"] = "Ampharos", ["suicune"] = "Suicune",
        ["entei"] = "Entei", ["raikou"] = "Raikou", ["metagross"] = "Metagross", ["milotic"] = "Milotic",
        ["zoroark"] = "Zoroark", ["zekrom"] = "Zekrom", ["reshiram"] = "Reshiram", ["kyurem"] = "Kyurem",
        ["xerneas"] = "Xerneas", ["yveltal"] = "Yveltal", ["zygarde"] = "Zygarde", ["solgaleo"] = "Solgaleo",
        ["lunala"] = "Lunala", ["necrozma"] = "Necrozma", ["zacian"] = "Zacian", ["zamazenta"] = "Zamazenta",
        ["koraidon"] = "Koraidon", ["miraidon"] = "Miraidon", ["terapagos"] = "Terapagos", ["ogerpon"] = "Ogerpon",
        ["pecharunt"] = "Pecharunt", ["ursaluna"] = "Ursaluna", ["vulpix"] = "Vulpix", ["tauros"] = "Tauros",
        ["magmar"] = "Magmar", ["pinsir"] = "Pinsir", ["crobat"] = "Crobat", ["zubat"] = "Zubat", ["dratini"] = "Dratini",
        ["abra"] = "Abra", ["kadabra"] = "Kadabra", ["meganium"] = "Meganium", ["chien-pao"] = "Chien-Pao", ["chi-yu"] = "Chi-Yu",
        // English names that differ from German, so English titles resolve too
        ["charizard"] = "Charizard", ["charmander"] = "Charmander", ["charmeleon"] = "Charmeleon", ["bulbasaur"] = "Bulbasaur",
        ["venusaur"] = "Venusaur", ["squirtle"] = "Squirtle", ["blastoise"] = "Blastoise", ["mewtwo"] = "Mewtwo",
        ["eevee"] = "Eevee", ["umbreon"] = "Umbreon", ["espeon"] = "Espeon", ["sylveon"] = "Sylveon", ["vaporeon"] = "Vaporeon",
        ["jolteon"] = "Jolteon", ["flareon"] = "Flareon", ["leafeon"] = "Leafeon", ["glaceon"] = "Glaceon",
        ["snorlax"] = "Snorlax", ["gyarados"] = "Gyarados", ["magikarp"] = "Magikarp", ["dragonite"] = "Dragonite",
        ["articuno"] = "Articuno", ["moltres"] = "Moltres", ["garchomp"] = "Garchomp", ["gardevoir"] = "Gardevoir",
        ["tyranitar"] = "Tyranitar", ["alakazam"] = "Alakazam", ["machamp"] = "Machamp", ["greninja"] = "Greninja",
        ["mimikyu"] = "Mimikyu", ["arcanine"] = "Arcanine", ["ninetales"] = "Ninetales", ["gastly"] = "Gastly",
        ["haunter"] = "Haunter", ["blaziken"] = "Blaziken", ["typhlosion"] = "Typhlosion", ["salamence"] = "Salamence",
        ["infernape"] = "Infernape", ["iono"] = "Iono", ["eternatus"] = "Eternatus", ["dragapult"] = "Dragapult",
        ["gholdengo"] = "Gholdengo", ["meowscarada"] = "Meowscarada", ["scizor"] = "Scizor", ["scyther"] = "Scyther",
        ["bloodmoon ursaluna"] = "Bloodmoon Ursaluna",
    };

    /// <summary>
    /// Looks up <paramref name="name"/> in <see cref="GermanToEnglish"/>, case-insensitively (the
    /// dictionary's own comparer) and ignoring diacritics - accents are stripped via Unicode NFD
    /// decomposition before the lookup (same approach as AneNotifier's
    /// <c>Extraction.AttributeNormalizer.Fold</c>), so an accented variant still resolves. Returns null
    /// when nothing matches.
    /// </summary>
    public static string? TryGetEnglishName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return GermanToEnglish.TryGetValue(StripDiacritics(name.Trim()), out var english) ? english : null;
    }

    private static string StripDiacritics(string text)
    {
        string decomposed;
        try
        {
            decomposed = text.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return text; // malformed unicode (e.g. a lone surrogate) - fall back to the raw text
        }

        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }
}
