using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Loads the embedded curated seed data. Every *.json file under KnownProducts/Seed/ is embedded (see
/// AneCore.csproj's wildcard EmbeddedResource glob) and picked up automatically here - adding a new
/// category seed file (e.g. Seed/game-consoles.json) needs no code change, only the csproj glob (already
/// a wildcard) and the seed integrity tests picking it up transparently via <see cref="LoadAll"/>.
/// </summary>
public static class KnownProductSeed
{
    public const string SourcePrefix = "seed:";
    public const string CurrentSource = "seed:curated-2026-09";

    /// <summary>Manifest resource name suffix identifying an embedded seed file, regardless of the SDK's chosen prefix.</summary>
    private const string ResourceMarker = "KnownProducts.Seed.";

    /// <summary>
    /// The original, hand-curated seed file - <see cref="LoadAll"/> without arguments returns only this
    /// one, unchanged from before the wider catalogue (tools/catalog-import) was added, so existing
    /// callers that predate the expansion (e.g. AneNotifier's own KnownProductCore.Tests.cs, which this
    /// repo cannot edit) keep exactly their original behaviour and pass unmodified. Pass
    /// <c>includeExpandedCatalog: true</c> to load every category (smartphones beyond Apple, laptops,
    /// GPUs/CPUs, consoles, ...) - see tools/catalog-import/README.md. AneNotifier/AneApi need a small
    /// follow-up change to opt into that (see this task's final report), the same way they need one to
    /// use <see cref="IKnownProductStore.GetAppliedSeedVersionAsync"/>.
    /// </summary>
    private const string CoreResourceFileName = "apple-iphones.json";

    /// <summary>
    /// Stable hash over the embedded seed files' bytes (name + content), independent of enumeration
    /// order. Changes whenever any seed file's content changes, so callers (e.g. AneNotifier's startup
    /// seeding) can compare it against a persisted "applied seed version" and skip re-upserting tens of
    /// thousands of unchanged rows on every restart - see <see cref="IKnownProductStore"/>.
    /// </summary>
    public static string Version => version.Value;

    private static readonly Lazy<string> version = new(ComputeVersion);

    private static string ComputeVersion()
    {
        var assembly = typeof(KnownProductSeed).Assembly;
        var resourceNames = SeedResourceNames(assembly).OrderBy(n => n, StringComparer.Ordinal).ToList();
        using var sha = SHA256.Create();
        using var buffer = new MemoryStream();
        foreach (var resourceName in resourceNames)
        {
            var nameBytes = Encoding.UTF8.GetBytes(resourceName);
            buffer.Write(nameBytes, 0, nameBytes.Length);
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found");
            stream.CopyTo(buffer);
        }
        var hash = sha.ComputeHash(buffer.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IEnumerable<string> SeedResourceNames(System.Reflection.Assembly assembly) =>
        assembly.GetManifestResourceNames()
            .Where(n => n.Contains(ResourceMarker, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses embedded seed resources. Without arguments, returns only the original curated seed
    /// (apple-iphones.json) - unchanged behaviour for existing callers. Pass
    /// <paramref name="includeExpandedCatalog"/> = true to also load every other Seed/*.json category
    /// file. Each product's <see cref="KnownProduct.Source"/> comes from its own JSON "source" field.
    /// </summary>
    public static IReadOnlyList<KnownProduct> LoadAll(bool includeExpandedCatalog = false)
    {
        var assembly = typeof(KnownProductSeed).Assembly;
        var resourceNames = SeedResourceNames(assembly).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (resourceNames.Count == 0)
            throw new InvalidOperationException("No embedded KnownProducts seed resources found");
        if (!includeExpandedCatalog)
        {
            resourceNames = resourceNames.Where(n => n.EndsWith("." + CoreResourceFileName, StringComparison.Ordinal)).ToList();
            if (resourceNames.Count == 0)
                throw new InvalidOperationException($"Embedded resource for {CoreResourceFileName} not found");
        }

        var result = new List<KnownProduct>();
        foreach (var resourceName in resourceNames)
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
            // Each seed file's own products carry their real provenance (e.g. "seed:techapi-2026-09",
            // "seed:docyx-pc-part-dataset-2026-09" - see tools/catalog-import/build_seed.py); CurrentSource
            // is only a fallback for a malformed/legacy entry missing the field entirely.
            Source = item.TryGetProperty("source", out var source) ? source.GetString() ?? CurrentSource : CurrentSource,
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
