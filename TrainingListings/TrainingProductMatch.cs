using Coflnet.Ane.KnownProducts;

namespace Coflnet.Ane.TrainingListings;

/// <summary>
/// Adds the verified catalog product to a training row (<see cref="TrainingListing.KnownProductId"/>). The matcher is the in-memory
/// alias index of <see cref="KnownProductCatalog"/> (a token lookup, no I/O), so this is cheap enough for every stored row.
/// </summary>
public static class TrainingProductMatch
{
    /// <summary>The title matched an alias of exactly one known product (the accessory, brand, category and ambiguity vetoes of <see cref="KnownProductMatcher"/> applied).</summary>
    public const string CatalogAlias = "catalog-alias";

    /// <summary>
    /// Sets the product fields of <paramref name="row"/> when the matcher finds a product; leaves a field that is set already (a replay or a
    /// newer producer) and returns false without a matcher, a title or a match. Never throws: a matcher fault must not stop the export.
    /// </summary>
    public static bool Apply(TrainingListing row, KnownProductMatcher? matcher)
    {
        if (matcher == null || !string.IsNullOrEmpty(row.KnownProductId) || string.IsNullOrWhiteSpace(row.Title))
            return false;
        try
        {
            var product = matcher.Match(row.Title, categoryPath: row.Categories);
            if (product == null)
                return false;
            row.KnownProductId = product.Id;
            row.KnownProductMethod = CatalogAlias;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
