namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Fills a variant attribute that every unit of a catalogue product has, when the listing does not state it: the Fire TV Stick 4K
/// exists with 8 GB only, so "Fire TV Stick 4K" and "Fire TV Stick 4K 8GB" are the same product and share the 8 GB page.
/// </summary>
/// <remarks>
/// Opt-in per catalogue entry: <see cref="ImpliedAttributesKey"/> in <see cref="KnownProduct.PossibleAttributes"/> names the keys whose
/// verified value set is complete and has exactly one value. A single verified value alone is not enough: imported entries often list
/// only the one value their source knew (a phone sold with 128 and 256 GB that lists 128 GB), and filling it would put the other
/// variant on the wrong page. A key whose set does not have exactly one value is never filled, whatever the marker says (the Fire TV
/// Stick 4K Max exists with 8 and 16 GB, an iPhone with several storage sizes). Runs after <see cref="KnownProductAttributeConstraint.Apply"/>,
/// so a stated value off the verified list is dropped first and then replaced by the implied one.
/// </remarks>
public static class KnownProductImpliedAttributes
{
    /// <summary>Possible-attributes key listing the attribute keys whose single verified value every unit of the product has.</summary>
    public const string ImpliedAttributesKey = "implied_attributes";

    /// <summary>The keys the product marks as implied (none for most entries).</summary>
    public static IEnumerable<string> ImpliedKeys(KnownProduct product) =>
        product.PossibleAttributes.TryGetValue(ImpliedAttributesKey, out var keys) ? keys : Enumerable.Empty<string>();

    /// <summary>The one value of <paramref name="key"/> every unit of <paramref name="product"/> has, or null when the product does not imply it.</summary>
    public static string? ImpliedValue(KnownProduct product, string key)
    {
        if (!ImpliedKeys(product).Contains(key, StringComparer.OrdinalIgnoreCase))
            return null;
        if (!product.PossibleAttributes.TryGetValue(key, out var values) || values.Count != 1)
            return null;
        return values.First();
    }

    /// <summary>Sets every implied attribute the listing does not state. Returns the filled key/value pairs.</summary>
    public static List<(string Key, string Value)> Fill(KnownProduct product, IDictionary<string, string> attributes)
    {
        var filled = new List<(string Key, string Value)>();
        foreach (var key in ImpliedKeys(product))
        {
            if (attributes.TryGetValue(key, out var stated) && !string.IsNullOrWhiteSpace(stated))
                continue;
            if (ImpliedValue(product, key) is not { } value)
                continue;
            attributes[key] = value;
            filled.Add((key, value));
        }
        return filled;
    }
}
