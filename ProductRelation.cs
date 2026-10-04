namespace Coflnet.Ane;

/// <summary>
/// One product page in a relation group (table <c>product_relations</c>). Pages of one relation key are RELATED, not merged: they stay separate products with their own listings
/// (unlike <see cref="Product.CanonicalSeoId"/>/<see cref="Product.RelatedSeoIds"/>, which belong to merged variants). Today the only kind is the language editions of one game on one platform,
/// key <c>game:&lt;Wikidata Qid&gt;:&lt;platform&gt;</c> (see <see cref="GameKey"/>): "Harry Potter und der Feuerkelch", "Harry Potter e il Calice di Fuoco" and "Harry Potter and the Goblet of Fire" on PlayStation 2.
/// Partition key: <see cref="RelationKey"/>; clustering key: <see cref="SeoId"/> (an upsert of the same page replaces its row).
/// </summary>
public class ProductRelation
{
    public string RelationKey { get; set; } = "";
    public string SeoId { get; set; } = "";
    /// <summary>The edition language of the page (en, de, fr, it, nl, es) when the title says it, else null.</summary>
    public string? Language { get; set; }
    /// <summary>The name of the page.</summary>
    public string Name { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    /// <summary>The relation key of the editions of one catalogue game on one platform. Never spans platforms.</summary>
    public static string GameKey(string qid, string platform) => $"game:{qid}:{platform}";
}

/// <summary>Another product page related to a product: its id, edition language (may be null) and name.</summary>
public record RelatedEdition(string SeoId, string? Language, string Name);
