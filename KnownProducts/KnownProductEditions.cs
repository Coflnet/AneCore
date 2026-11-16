namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Collector, special and limited editions of a catalogue device (a Switch OLED Zelda Edition, a Steam Deck OLED Limited Edition, a PS5 30th Anniversary): a console whose
/// physical design differs from the base model and that collectors pay more for, so it has a page of its own instead of joining the base page.
/// </summary>
/// <remarks>
/// <para>
/// An edition is an ordinary catalogue entry that names its base entries in <see cref="EditionOfKey"/> and the words that identify it in <see cref="EditionTermsKey"/>
/// (stored in <see cref="KnownProduct.PossibleAttributes"/>, so the existing seed sync and Cassandra map column carry them without a schema change; neither key is a
/// slug-differentiating attribute, see <see cref="KnownProductAttributeConstraint"/>). Each terms entry is one requirement written as alternatives separated by
/// <c>|</c> ("zelda|tears of the kingdom|totk"); every requirement has to be met somewhere in the title. <see cref="MarkerRequirement"/>, as a whole entry or as one
/// alternative, stands for the edition words ("Edition", "Limited", "Special", "Sonderedition") without the game editions ("Collector's Edition", "Day One Edition")
/// and the PS5's "Digital Edition"; <see cref="LimitedRequirement"/> for the same words without a bare "Edition". An entry starting with <c>!</c> excludes the edition when one of its alternatives is in the title ("!wildcat").
/// </para>
/// <para>
/// Aliases alone cannot carry this: they match consecutive tokens only, while marketplace titles put storage, colour, "Modell" or "OVP" between the console and its
/// edition words in every order. The alias match still decides the base device; this class then looks for the edition words anywhere in the title, see
/// <see cref="EditionTitle"/> for the words that do not count (negated, or the game sold with the console).
/// </para>
/// <para>
/// The edition that names the most theme requirements (an edition word alone is no theme) wins, then the one with the most theme words. An edition matched by its own
/// alias stands unless a sibling edition beats it; two editions named equally well leave the candidate as it is.
/// </para>
/// </remarks>
public sealed class KnownProductEditions
{
    /// <summary>Ids of the base catalogue entries whose titles may name this edition.</summary>
    public const string EditionOfKey = "edition_of";

    /// <summary>The requirements that identify the edition (each one "alternative|alternative|...").</summary>
    public const string EditionTermsKey = "edition_terms";

    /// <summary>A requirement (or alternative) met by any console edition word, see <see cref="EditionTitle.MarkerWords"/>.</summary>
    public const string MarkerRequirement = "{edition}";

    /// <summary>A requirement (or alternative) met by a limited-design word only ("Limited", "Special", "Sonderedition"), not by a bare "Edition", see <see cref="EditionTitle.LimitedWords"/>.</summary>
    public const string LimitedRequirement = "{limited}";

    private const string ExclusionPrefix = "!";

    private sealed record Alternative(string[] Tokens, bool IsMarker);

    private sealed record Requirement(Alternative[] Alternatives, bool IsExclusion);

    private sealed record Rule(KnownProduct Edition, Requirement[] Requirements, bool IsTheme);

    /// <summary>How well a title names an edition: the theme requirements it meets and their words.</summary>
    private readonly record struct Score(int Requirements, int Tokens)
    {
        public bool Beats(Score other) => Requirements > other.Requirements || Requirements == other.Requirements && Tokens > other.Tokens;
    }

    private readonly record struct Hit(Rule Rule, Score Score);

    private static readonly Alternative[] MarkerAlternatives = EditionTitle.MarkerWords.Select(w => new Alternative(new[] { w }, true)).ToArray();
    private static readonly Alternative[] LimitedAlternatives = EditionTitle.LimitedWords.Select(w => new Alternative(new[] { w }, true)).ToArray();

    /// <summary>The theme words of the seeded editions that are named after a game or theme (Zelda, Minecraft, Spider-Man), for title-only rules that have no catalogue match yet.</summary>
    private static readonly Lazy<string[][]> SeedThemeWords = new(() => KnownProductSeed.LoadNamed(ConsoleSeedFile)
        .Select(BuildRule).OfType<Rule>().Where(rule => rule.IsTheme)
        .SelectMany(rule => rule.Requirements.Where(r => !r.IsExclusion))
        .SelectMany(r => r.Alternatives.Where(a => !a.IsMarker).Select(a => a.Tokens))
        .ToArray());

    private const string ConsoleSeedFile = "game-consoles.json";
    private static readonly List<Rule> NoRules = new();

    private readonly Dictionary<string, List<Rule>> rulesByBase;

    internal KnownProductEditions(IEnumerable<KnownProduct> products) => rulesByBase = BuildRules(products);

    /// <summary>True when <paramref name="product"/> is a collector edition of another catalogue entry.</summary>
    public static bool IsEdition(KnownProduct? product) => product?.PossibleAttributes.ContainsKey(EditionOfKey) == true;

    /// <summary>
    /// The title names a theme word of a seeded game-themed edition ("minecraft", "spider man", "fortnite"): for title-only rules (the scope gate) that decide before the catalogue
    /// match whether "Xbox One S Minecraft Limited" is a console in its edition design or "Nintendo Switch Xenoblade Chronicles 2 Limited" a game. Reads the embedded seed only.
    /// </summary>
    public static bool NamesSeedEditionTheme(string? title)
    {
        var tokens = KnownProductMatcher.Normalize(title ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return SeedThemeWords.Value.Any(words => ContainsSequence(tokens, words));
    }

    private static bool ContainsSequence(string[] tokens, string[] words)
    {
        for (var start = 0; start + words.Length <= tokens.Length; start++)
        {
            if (tokens.AsSpan(start, words.Length).SequenceEqual(words))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The edition of <paramref name="candidate"/> the title names, else <paramref name="candidate"/> itself; null when the candidate is an edition (matched by its own alias)
    /// whose words do not count in this title and no sibling edition is named either, so the caller falls back to the device.
    /// </summary>
    internal KnownProduct? Refine(KnownProduct candidate, string rawTitle, string[] titleTokens, int aliasEnd)
    {
        var rules = RulesFor(candidate);
        if (rules.Count == 0)
            return candidate;
        var title = new EditionTitle(rawTitle, titleTokens, aliasEnd);
        var hits = rules.Select(rule => ScoreOf(rule, title)).OfType<Hit>()
            .OrderByDescending(hit => hit.Score.Requirements).ThenByDescending(hit => hit.Score.Tokens).ToList();
        if (hits.Count == 0)
            return IsEdition(candidate) ? null : candidate;
        if (hits.FirstOrDefault(hit => hit.Rule.Edition.Id == candidate.Id) is { Rule: not null } own)
            return hits[0].Score.Beats(own.Score) ? hits[0].Rule.Edition : candidate;
        if (hits.Count > 1 && !hits[0].Score.Beats(hits[1].Score))
            return candidate;
        return hits[0].Rule.Edition;
    }

    /// <summary>The editions of the candidate's base device; empty (and allocation free) for the many devices without editions.</summary>
    private IReadOnlyList<Rule> RulesFor(KnownProduct candidate)
    {
        if (!candidate.PossibleAttributes.TryGetValue(EditionOfKey, out var bases))
            return rulesByBase.GetValueOrDefault(candidate.Id) ?? NoRules;
        return bases.SelectMany(id => rulesByBase.GetValueOrDefault(id) ?? NoRules).Distinct().ToList();
    }

    private static Hit? ScoreOf(Rule rule, EditionTitle title)
    {
        var requirements = 0;
        var tokens = 0;
        foreach (var requirement in rule.Requirements)
        {
            var found = BestNamed(requirement, title);
            if (requirement.IsExclusion)
            {
                if (found != null)
                    return null;
                continue;
            }
            if (found == null)
                return null;
            if (found.IsMarker)
                continue;
            requirements++;
            tokens += found.Tokens.Length;
        }
        return new Hit(rule, new Score(requirements, tokens));
    }

    /// <summary>The alternative of the requirement the title names, a theme word before an edition word and a longer one first; null for none.</summary>
    private static Alternative? BestNamed(Requirement requirement, EditionTitle title) =>
        requirement.Alternatives
            .Where(alternative => title.Names(alternative.Tokens, alternative.IsMarker, !alternative.IsMarker && !requirement.IsExclusion))
            .OrderBy(alternative => alternative.IsMarker).ThenByDescending(alternative => alternative.Tokens.Length)
            .FirstOrDefault();

    private static Dictionary<string, List<Rule>> BuildRules(IEnumerable<KnownProduct> products)
    {
        var rules = new Dictionary<string, List<Rule>>(StringComparer.Ordinal);
        foreach (var product in products)
        {
            if (BuildRule(product) is not { } rule)
                continue;
            foreach (var baseId in product.PossibleAttributes[EditionOfKey])
            {
                if (!rules.TryGetValue(baseId, out var list))
                    rules[baseId] = list = new List<Rule>();
                list.Add(rule);
            }
        }
        return rules;
    }

    private static Rule? BuildRule(KnownProduct product)
    {
        if (!product.PossibleAttributes.ContainsKey(EditionOfKey) || !product.PossibleAttributes.TryGetValue(EditionTermsKey, out var terms))
            return null;
        var requirements = terms.Select(ParseRequirement).Where(r => r.Alternatives.Length > 0).ToArray();
        if (requirements.All(r => r.IsExclusion))
            return null;
        // an edition that needs an edition word is named after a game or a theme ("Minecraft Limited"), see NamesSeedEditionTheme
        var isTheme = requirements.Any(r => r.Alternatives.Any(a => a.IsMarker));
        return new Rule(product, requirements, isTheme);
    }

    private static Requirement ParseRequirement(string term)
    {
        var text = term?.Trim() ?? "";
        var isExclusion = text.StartsWith(ExclusionPrefix, StringComparison.Ordinal);
        if (isExclusion)
            text = text[ExclusionPrefix.Length..];
        var alternatives = text.Split('|').SelectMany(ParseAlternative).ToArray();
        return new Requirement(alternatives, isExclusion);
    }

    private static IEnumerable<Alternative> ParseAlternative(string text)
    {
        if (string.Equals(text.Trim(), MarkerRequirement, StringComparison.OrdinalIgnoreCase))
            return MarkerAlternatives;
        if (string.Equals(text.Trim(), LimitedRequirement, StringComparison.OrdinalIgnoreCase))
            return LimitedAlternatives;
        var normalized = KnownProductMatcher.Normalize(text);
        return normalized.Length == 0
            ? Enumerable.Empty<Alternative>()
            : new[] { new Alternative(normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries), false) };
    }
}
