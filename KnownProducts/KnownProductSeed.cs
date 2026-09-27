using System.Text.Json;

namespace Coflnet.Ane.KnownProducts;

/// <summary>Loads the embedded curated seed data (small, high-confidence entries only - see Seed/*.json).</summary>
public static class KnownProductSeed
{
    public const string SourcePrefix = "seed:";
    public const string CurrentSource = "seed:curated-2026-09";

    private static readonly string[] ResourceNames =
    {
        "Coflnet.Ane.KnownProducts.Seed.apple-iphones.json",
    };

    /// <summary>Parses all embedded seed resources. <see cref="KnownProduct.Source"/> is set to <see cref="CurrentSource"/>.</summary>
    public static IReadOnlyList<KnownProduct> LoadAll()
    {
        var assembly = typeof(KnownProductSeed).Assembly;
        var result = new List<KnownProduct>();
        foreach (var resourceName in ResourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found");
            using var doc = JsonDocument.Parse(stream);
            foreach (var item in doc.RootElement.GetProperty("products").EnumerateArray())
                result.Add(ParseProduct(item));
        }
        return result;
    }

    private static KnownProduct ParseProduct(JsonElement item)
    {
        var product = new KnownProduct
        {
            Id = item.GetProperty("id").GetString() ?? "",
            Brand = item.GetProperty("brand").GetString() ?? "",
            Model = item.GetProperty("model").GetString() ?? "",
            Name = item.GetProperty("name").GetString() ?? "",
            Vertical = item.TryGetProperty("vertical", out var vertical) ? vertical.GetString() ?? "" : "",
            Source = CurrentSource,
            VerifiedAt = DateTime.UtcNow,
        };

        if (item.TryGetProperty("categories", out var categories))
            product.Categories = categories.EnumerateArray().Select(c => c.GetString() ?? "").ToList();

        if (item.TryGetProperty("aliases", out var aliases))
            product.Aliases = new HashSet<string>(aliases.EnumerateArray().Select(a => a.GetString() ?? ""), StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("excludeTerms", out var excludeTerms))
            product.ExcludeTerms = new HashSet<string>(excludeTerms.EnumerateArray().Select(t => t.GetString() ?? ""), StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("possibleAttributes", out var possibleAttributes))
        {
            foreach (var prop in possibleAttributes.EnumerateObject())
            {
                product.PossibleAttributes[prop.Name] = new HashSet<string>(
                    prop.Value.EnumerateArray().Select(v => v.GetString() ?? ""), StringComparer.OrdinalIgnoreCase);
            }
        }

        return product;
    }
}
