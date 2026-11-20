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
/// Stick 4K Max exists with 8 and 16 GB, an iPhone with several storage sizes). A listing that states another value ("PS5 Pro 1TB",
/// a "Fire TV Stick 4K 16GB" that is most likely a 4K Max) is mislabelled or no plain unit: <see cref="KnownProductAttributeConstraint.Apply"/>
/// drops that value and this fill leaves the key empty, so the offer keeps the page without the size instead of joining the clean one.
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

    /// <summary>
    /// Sets every implied attribute the listing does not state. <paramref name="dropped"/> are the values the attribute constraint removed
    /// from the same listing: a key whose stated value (or raw twin) was dropped there is never filled. Returns the filled key/value pairs.
    /// </summary>
    public static List<(string Key, string Value)> Fill(KnownProduct product, IDictionary<string, string> attributes,
        IReadOnlyCollection<(string Key, string Value)> dropped)
    {
        var filled = new List<(string Key, string Value)>();
        foreach (var key in ImpliedKeys(product))
        {
            if (attributes.TryGetValue(key, out var stated) && !string.IsNullOrWhiteSpace(stated))
                continue;
            if (StatedOtherwise(key, dropped))
                continue;
            if (ImpliedValue(product, key) is not { } value)
                continue;
            attributes[key] = value;
            filled.Add((key, value));
        }
        return filled;
    }

    /// <summary>The listing stated a value of <paramref name="key"/> (or its raw twin) that is off the verified list.</summary>
    private static bool StatedOtherwise(string key, IReadOnlyCollection<(string Key, string Value)> dropped)
    {
        var twin = KnownProductAttributeConstraint.TwinKeyOf(key);
        return dropped.Any(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)
                                || twin != null && string.Equals(d.Key, twin, StringComparison.OrdinalIgnoreCase));
    }
}
