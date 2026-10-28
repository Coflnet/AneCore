using System.Text.RegularExpressions;

namespace Coflnet.Ane;

/// <summary>Independent Pokemon singles and the shared minimum identity check for legacy comparisons.</summary>
public static class PokemonOfferRules
{
    public const string OfferKind = "pokemon_card_offer";
    public const string SeoPrefix = "pokemon-offer-";

    // Normalized output shapes of the existing card-number extractor, not a title parser.
    private static readonly Regex Number = new(@"^(?:(?:tg|gg|rc|sv|swsh|sm|xy|bw)?\d{1,3}/(?:tg|gg|rc|sv)?\d{2,3}|(?:svp|swsh|sm|xy|bw|sv|tg|gg)\d{2,3}|\d{1,3})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsUnidentifiedSingle(string? brand, IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes?.GetValueOrDefault("product_type") is not ("single card" or "graded card")) return false;
        brand = (string.IsNullOrWhiteSpace(brand) ? attributes.GetValueOrDefault("brand") : brand)?.Trim();
        if (!string.Equals(brand, "Pokemon", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(brand, "Pokémon", StringComparison.OrdinalIgnoreCase)) return false;
        var number = attributes.GetValueOrDefault("card_number");
        return string.IsNullOrWhiteSpace(number) || !Number.IsMatch(number);
    }
}
