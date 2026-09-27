using System.Globalization;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Filters a listing's merged attributes against a matched <see cref="KnownProduct"/>'s verified
/// <see cref="KnownProduct.PossibleAttributes"/>, dropping values that are not on the verified list
/// (e.g. a storage size the model was never sold in) so they cannot create "impossible" product variants.
/// </summary>
/// <remarks>
/// <para>
/// Design decision (see AneNotifier's <c>ProductGrouper.GenerateSeoId</c>): only the attribute keys that
/// method actually uses to build a product's SEO id / variant slug can create a bogus *variant* when
/// wrong - <c>storage_size</c>, <c>ram_size</c>, <c>screen_size</c>, <c>color</c>, <c>size</c>,
/// <c>platform</c>, <c>registration_year</c>, plus the fashion-only <c>gender</c>/<c>fashion_type</c>/
/// <c>material</c>. Those are the keys this constraint actually verifies/drops. For that "slug-affecting"
/// set: if the known product lists the key with a non-empty verified value set, an out-of-list value is
/// dropped; if the key is not listed on the known product at all, the value is dropped too (we have
/// curated data for this product - an undeclared variant-differentiating attribute is unverified and
/// safer to omit than to risk a wrong variant); an explicitly listed key with an *empty* set means
/// "allowed, free value" (used for attributes such as colour where we are not certain of the complete
/// official list) and is kept unmodified.
/// </para>
/// <para>
/// Every other attribute key (e.g. <c>condition</c>, <c>os</c>, <c>os_version</c>, <c>device_type</c>,
/// <c>product_line</c>, <c>brand</c>, <c>model</c>, ...) is always kept regardless of
/// <see cref="KnownProduct.PossibleAttributes"/>. <c>condition</c> is generic across all products (already
/// normalized elsewhere) and does not need per-product verification. <c>os</c>/<c>os_version</c> are read
/// directly by AneApi's search filters, so silently dropping them for a known product that happens not to
/// declare them would be a functional regression, not a data-quality improvement.
/// </para>
/// </remarks>
public static class KnownProductAttributeConstraint
{
    /// <summary>Attribute keys that feed <c>GenerateSeoId</c>'s variant slug and are therefore verified/dropped.</summary>
    private static readonly HashSet<string> SlugDifferentiatingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "storage_size", "ram_size", "screen_size", "color", "size", "platform", "registration_year",
        "gender", "fashion_type", "material",
    };

    /// <summary>Kept unconditionally - generic across all products, not product-specific verified data.</summary>
    private static readonly HashSet<string> UniversalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "condition",
    };

    /// <summary>
    /// Applies the constraint in place, removing invalid entries from <paramref name="attributes"/>.
    /// Returns the dropped key/value pairs for logging/metrics.
    /// </summary>
    public static List<(string Key, string Value)> Apply(KnownProduct product, IDictionary<string, string> attributes)
    {
        var dropped = new List<(string Key, string Value)>();
        if (attributes.Count == 0)
            return dropped;

        foreach (var key in attributes.Keys.ToList())
        {
            if (UniversalKeys.Contains(key))
                continue;
            if (!SlugDifferentiatingKeys.Contains(key))
                continue;

            var value = attributes[key];
            if (!product.PossibleAttributes.TryGetValue(key, out var allowed))
            {
                // Not declared on this known product at all: unverified variant differentiator, drop.
                dropped.Add((key, value));
                attributes.Remove(key);
                continue;
            }

            if (allowed.Count == 0)
                continue; // explicitly allowed, free value (e.g. colour without a certain full list)

            if (!allowed.Any(a => string.Equals(NormalizeForComparison(a), NormalizeForComparison(value), StringComparison.Ordinal)))
            {
                dropped.Add((key, value));
                attributes.Remove(key);
            }
        }

        return dropped;
    }

    /// <summary>
    /// Normalizes a value for comparison so unit variants match: "128 GB" / "128GB" / "128" are the same
    /// value, as are "1TB" and "1000GB". Case/whitespace differences are also ignored.
    /// </summary>
    internal static string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var v = value.Trim().Replace(" ", "").ToLowerInvariant();
        var m = Regex.Match(v, @"^(\d+(?:\.\d+)?)(gb|tb)?$");
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            var unit = m.Groups[2].Success ? m.Groups[2].Value : "gb";
            var gb = unit == "tb" ? num * 1000 : num;
            return gb % 1 == 0 ? $"{(long)gb}gb" : $"{gb.ToString(CultureInfo.InvariantCulture)}gb";
        }
        return v;
    }
}
