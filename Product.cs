using System.Text.Json.Serialization;

namespace Coflnet.Ane;

/// <summary>
/// Represents a product aggregated from multiple listings
/// Primary key is the SEO-friendly identifier (SeoId)
/// </summary>
public class Product
{
    [JsonPropertyName("id")]
    public string SeoId { get; set; } = ""; // Primary key - SEO-friendly product identifier
    public List<string> Category { get; set; } = new(); // Categories extracted by LLM (may contain multiple categories)
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? IdentifierType { get; set; }
    public string? IdentifierValue { get; set; }
    public string Condition { get; set; } = "unknown";
    public double AveragePrice { get; set; }
    public double MedianPrice { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public int ListingCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<string> SampleTitles { get; set; } = new();
    public string? ImageUrl { get; set; }
    public Dictionary<string, string>? Attributes { get; set; } // Key attributes extracted from listings

    // Canonical SEO ID for grouped products - if set, this product redirects to another
    public string? CanonicalSeoId { get; set; }
    // All SEO IDs grouped together (includes self) - stored on canonical product only
    public List<string> RelatedSeoIds { get; set; } = new();

    public static string NormalizeCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return "unknown";
        var cond = condition.Trim().ToLowerInvariant();
        if (ConditionValueNormalization.TryGetValue(cond, out var normalized))
            return normalized;
        // Check if any known condition keyword is contained in the value  
        // (handles cases like "1x vorhanden, gebraucht" containing "gebraucht")
        foreach (var kvp in ConditionValueNormalization)
        {
            if (cond.Contains(kvp.Key) && kvp.Key.Length >= 3) // min 3 chars to avoid false positives
                return kvp.Value;
        }
        // If the value is too long or doesn't match any known condition, treat as unknown
        // This filters out garbage like "25 eur", "nur abholung", "frisch gewaschen"
        if (cond.Length > 30 || !IsLikelyConditionValue(cond))
            return "unknown";
        return cond;
    }

    /// <summary>
    /// Checks whether a string looks like it could be a condition value rather than garbage data.
    /// </summary>
    private static bool IsLikelyConditionValue(string value)
    {
        // Known condition-related patterns (partial matches)
        var conditionPatterns = new[] {
            "new", "neu", "neuf", "nuevo", "nuovo", "nieuw",
            "used", "gebraucht", "occasion", "usado", "usato", "gebruikt",
            "broken", "defekt", "kaputt", "kapot", "trasig",
            "good", "gut", "bon", "bueno", "buono", "goed",
            "fair", "ok", "okay", "akzeptabel",
            "like new", "wie neu", "mint", "excellent", "sehr gut",
            "refurbished", "generalüberholt",
            "funktionsfähig", "funktioniert", "working", "functional"
        };
        return conditionPatterns.Any(p => value.Contains(p));
    }

    /// <summary>
    /// Checks whether a given color string is a recognized color name.
    /// Returns true if the value maps to a known color.
    /// </summary>
    public static bool IsKnownColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return false;
        var col = color.Trim().ToLowerInvariant();
        return ColorValueNormalization.ContainsKey(col);
    }

    public static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return color;
        var col = color.Trim().ToLowerInvariant();
        return ColorValueNormalization.TryGetValue(col, out var normalized) ? normalized : color.Trim();
    }

    public static List<string> GetUnnormalizedColors(string normalizedColor)
    {
        var result = new List<string> { normalizedColor };
        foreach (var kvp in ColorValueNormalization)
        {
            if (kvp.Value.Equals(normalizedColor, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(kvp.Key);
            }
        }
        return result.Distinct().ToList();
    }

    private static readonly Dictionary<string, string> ColorValueNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        // Black
        { "schwarz", "Black" }, { "black", "Black" }, { "noir", "Black" }, { "negro", "Black" }, { "nero", "Black" }, { "zwart", "Black" }, { "svart", "Black" }, { "černá", "Black" },
        // White
        { "weiß", "White" }, { "weiss", "White" }, { "white", "White" }, { "blanc", "White" }, { "blanco", "White" }, { "bianco", "White" }, { "wit", "White" }, { "vit", "White" }, { "bílá", "White" },
        // Red
        { "rot", "Red" }, { "red", "Red" }, { "rouge", "Red" }, { "rojo", "Red" }, { "rosso", "Red" }, { "rood", "Red" }, { "röd", "Red" }, { "červená", "Red" },
        // Blue
        { "blau", "Blue" }, { "blue", "Blue" }, { "bleu", "Blue" }, { "azul", "Blue" }, { "blu", "Blue" }, { "blauw", "Blue" }, { "blå", "Blue" }, { "modrá", "Blue" },
        // Green
        { "grün", "Green" }, { "green", "Green" }, { "vert", "Green" }, { "verde", "Green" }, { "groen", "Green" }, { "grön", "Green" }, { "zelená", "Green" },
        // Yellow
        { "gelb", "Yellow" }, { "yellow", "Yellow" }, { "jaune", "Yellow" }, { "amarillo", "Yellow" }, { "giallo", "Yellow" }, { "geel", "Yellow" }, { "gul", "Yellow" }, { "žlutá", "Yellow" },
        // Orange
        { "orange", "Orange" }, { "naranja", "Orange" }, { "arancione", "Orange" }, { "oranje", "Orange" }, { "oranžová", "Orange" },
        // Purple
        { "lila", "Purple" }, { "purple", "Purple" }, { "violet", "Purple" }, { "morado", "Purple" }, { "viola", "Purple" }, { "paars", "Purple" }, { "lila/purpur", "Purple" }, { "fialová", "Purple" },
        // Pink
        { "rosa", "Pink" }, { "pink", "Pink" }, { "rose", "Pink" }, { "roze", "Pink" }, { "růžová", "Pink" },
        // Brown
        { "braun", "Brown" }, { "brown", "Brown" }, { "marron", "Brown" }, { "marrón", "Brown" }, { "marrone", "Brown" }, { "bruin", "Brown" }, { "brun", "Brown" }, { "hnědá", "Brown" },
        // Grey/Gray
        { "grau", "Grey" }, { "grey", "Grey" }, { "gray", "Grey" }, { "gris", "Grey" }, { "grigio", "Grey" }, { "grijs", "Grey" }, { "grå", "Grey" }, { "šedá", "Grey" },
        // Silver
        { "silber", "Silver" }, { "silver", "Silver" }, { "argent", "Silver" }, { "plata", "Silver" }, { "argento", "Silver" }, { "zilver", "Silver" }, { "stříbrná", "Silver" },
        // Gold
        { "gold", "Gold" }, { "or", "Gold" }, { "oro", "Gold" }, { "goud", "Gold" }, { "guld", "Gold" }, { "zlatá", "Gold" },
        // Bronze
        { "bronze", "Bronze" }, { "bronce", "Bronze" }, { "bronzo", "Bronze" }, { "brons", "Bronze" }, { "bronzová", "Bronze" },
        // Copper
        { "kupfer", "Copper" }, { "copper", "Copper" }, { "cuivre", "Copper" }, { "cobre", "Copper" }, { "rame", "Copper" }, { "koper", "Copper" }, { "koppar", "Copper" }, { "měď", "Copper" },
        // Titanium
        { "titan", "Titanium" }, { "titanium", "Titanium" }, { "titane", "Titanium" }, { "titanio", "Titanium" },
        // Clear/Transparent
        { "transparent", "Clear" }, { "clear", "Clear" }, { "transparente", "Clear" }, { "trasparente", "Clear" }, { "genomskinlig", "Clear" }, { "průhledná", "Clear" },
        // Multi-color
        { "mehrfarbig", "Multicolor" }, { "multicolor", "Multicolor" }, { "multicolore", "Multicolor" }, { "multicolor/mehrfarbig", "Multicolor" }, { "flerfärgad", "Multicolor" }, { "vícebarevná", "Multicolor" }
    };

    private static readonly Dictionary<string, string> ConditionValueNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        // New
        { "neu", "new" }, { "new", "new" }, { "neuf", "new" }, { "nuevo", "new" },
        { "nuovo", "new" }, { "nieuw", "new" }, { "ny", "new" }, { "nový", "new" },
        { "nowy", "new" }, { "novo", "new" }, { "nie benutzt", "new" },
        // Used (includes like new, good, acceptable, used)
        { "wie neu", "used" }, { "like new", "used" }, { "comme neuf", "used" },
        { "como nuevo", "used" }, { "come nuovo", "used" }, { "als nieuw", "used" },
        { "som ny", "used" }, { "jako nový", "used" }, { "like_new", "used" },
        { "gut", "used" }, { "good", "used" }, { "bon", "used" }, { "bueno", "used" },
        { "buono", "used" }, { "goed", "used" }, { "bra", "used" }, { "dobrý", "used" },
        { "akzeptabel", "used" }, { "acceptable", "used" },
        { "aceptable", "used" }, { "accettabile", "used" },
        { "acceptabel", "used" }, { "redelijk", "used" },
        { "gebraucht", "used" }, { "used", "used" }, { "occasion", "used" },
        { "usado", "used" }, { "usato", "used" }, { "gebruikt", "used" },
        { "begagnad", "used" }, { "použitý", "used" },
        // Broken / Defect
        { "defekt", "broken" }, { "broken", "broken" }, { "défectueux", "broken" },
        { "defectuoso", "broken" }, { "difettoso", "broken" }, { "defect", "broken" },
        { "trasig", "broken" }, { "kapot", "broken" }, { "for parts", "broken" }, { "for_parts", "broken" }
    };
}
