namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// A curated, human-verified catalog entry for a specific product (e.g. "Apple iPhone 14", not
/// "Apple iPhone 14 128GB used" - condition/storage/color are variants layered on top by the normal
/// grouping pipeline). Used by <see cref="KnownProductMatcher"/> to short-circuit the rule/wildcard
/// extraction path with pre-verified brand/model/category/attribute data.
/// </summary>
public class KnownProduct
{
    /// <summary>Canonical slug, without condition/variant suffixes (e.g. "apple-iphone-14"). Partition key.</summary>
    public string Id { get; set; } = "";

    public string Brand { get; set; } = "";

    public string Model { get; set; } = "";

    /// <summary>Human-readable display name (e.g. "Apple iPhone 14").</summary>
    public string Name { get; set; } = "";

    /// <summary>German taxonomy path as used on products (e.g. Elektronik, Kommunikationsgeräte, Telefone, Mobiltelefone).</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>One of "electronics", "clothing", "cards" - informational, not used for matching.</summary>
    public string Vertical { get; set; } = "";

    /// <summary>
    /// Human forms of the product name used for matching ("iphone 14", "iphone14"). Matching is done on
    /// normalized text (see <see cref="KnownProductMatcher.Normalize"/>), so separator/case variants of the
    /// same alias do not need to be listed separately.
    /// </summary>
    public HashSet<string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Verified allowed values per attribute key (using the same keys the extraction pipeline produces,
    /// e.g. "storage_size", "os" - see <see cref="KnownProductAttributeConstraint"/>). An empty set means
    /// the key is allowed but its value is not verified (kept as-is, e.g. "color" when the full official
    /// list is not certain).
    /// </summary>
    public Dictionary<string, HashSet<string>> PossibleAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tokens that veto a match for this product when found immediately after a matched alias (e.g. a
    /// longer sibling model's suffix). Belt-and-suspenders alongside "longest alias wins" in <see cref="KnownProductMatcher"/>.
    /// </summary>
    public HashSet<string> ExcludeTerms { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Provenance, e.g. "seed:curated-2026-09" or "admin". Seed rows are never overwritten by admin edits and vice versa.</summary>
    public string Source { get; set; } = "";

    public DateTime VerifiedAt { get; set; }
}
