using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Coflnet.Ane;

/// <summary>Whether a vertical is fully processed or only sampled.</summary>
public enum CategoryScopeStatus
{
    /// <summary>Crawled, extracted and grouped into products.</summary>
    Active,
    /// <summary>Planned follow-up vertical: only a deterministic sample is kept for later inspection.</summary>
    Next
}

/// <summary>A vertical from <c>Categories/CategoryScope.json</c>.</summary>
/// <param name="Key">Stable key (cards, clothing, electronics, watches, ...)</param>
public sealed record ScopeVertical(string Key, CategoryScopeStatus Status, string Name, string? Rationale)
{
    public bool IsActive => Status == CategoryScopeStatus.Active;
    public bool IsNext => Status == CategoryScopeStatus.Next;
}

/// <summary>
/// Scope of a platform category. <see cref="Vertical"/> is null when the category is known to be out of scope.
/// </summary>
/// <param name="TitleKeywords">Normalized keywords; when present the listing title must contain one of them
/// (broad platform categories such as Kleinanzeigen "Spielzeug" for LEGO)</param>
public sealed record PlatformCategoryScope(ScopeVertical? Vertical, IReadOnlyList<string> TitleKeywords)
{
    public bool IsOutOfScope => Vertical == null;

    /// <summary>True when no title keywords are required or the title contains one of them.</summary>
    public bool MatchesTitle(string? title)
    {
        if (TitleKeywords.Count == 0)
            return true;
        if (string.IsNullOrWhiteSpace(title))
            return false;
        var normalized = CategoryScopeCatalog.Normalize(title);
        return TitleKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal));
    }
}

/// <summary>
/// Resolves unified category label paths (as produced by the notifier's CategoryMapper) to a scope vertical.
/// Built once from the unified category tree; the longest configured label path that prefixes the listing path wins.
/// </summary>
public sealed class UnifiedScopeIndex
{
    private readonly List<(IReadOnlyList<string> Labels, ScopeVertical? Vertical)> entries;

    internal UnifiedScopeIndex(List<(IReadOnlyList<string> Labels, ScopeVertical? Vertical)> entries)
    {
        this.entries = entries.OrderByDescending(e => e.Labels.Count).ToList();
    }

    /// <summary>
    /// Returns the matching scope (null vertical = out of scope), or null when no configured unified category matches.
    /// </summary>
    public PlatformCategoryScope? Resolve(IReadOnlyList<string>? labelPath)
    {
        if (labelPath == null || labelPath.Count == 0)
            return null;
        foreach (var (labels, vertical) in entries)
        {
            if (labels.Count > labelPath.Count)
                continue;
            var match = true;
            for (var i = 0; i < labels.Count && match; i++)
                match = string.Equals(labels[i], labelPath[i], StringComparison.OrdinalIgnoreCase);
            if (match)
                return new PlatformCategoryScope(vertical, Array.Empty<string>());
        }
        return null;
    }
}

/// <summary>
/// Single source of truth for which marketplace categories belong to which vertical, loaded from the embedded
/// <c>Categories/CategoryScope.json</c>. Used by the scraper (ScopeFilter) and the notifier (ListingScope).
/// </summary>
public sealed class CategoryScopeCatalog
{
    public const string ResourceName = "Coflnet.Ane.Categories.CategoryScope.json";

    private static readonly Lazy<CategoryScopeCatalog> SharedInstance = new(() => new CategoryScopeCatalog());

    /// <summary>Process wide instance backed by the embedded resource.</summary>
    public static CategoryScopeCatalog Shared => SharedInstance.Value;

    private readonly Dictionary<string, ScopeVertical> verticals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Platform, Dictionary<string, PlatformCategoryScope>> byId = new();
    private readonly Dictionary<Platform, Dictionary<string, PlatformCategoryScope>> byLabel = new();
    private readonly HashSet<Platform> outOfScopePlatforms = new();
    private readonly List<(string Slug, ScopeVertical? Vertical)> unifiedSlugs = new();

    public CategoryScopeCatalog() : this(OpenEmbedded())
    {
    }

    /// <summary>Loads a scope definition from a JSON stream (tests).</summary>
    public CategoryScopeCatalog(Stream json)
    {
        using (json)
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            foreach (var property in root.GetProperty("verticals").EnumerateObject())
            {
                var status = property.Value.GetProperty("status").GetString() switch
                {
                    "active" => CategoryScopeStatus.Active,
                    "next" => CategoryScopeStatus.Next,
                    var other => throw new InvalidOperationException($"Unknown status '{other}' for vertical {property.Name}")
                };
                var vertical = new ScopeVertical(property.Name, status,
                    property.Value.TryGetProperty("name", out var name) ? name.GetString() ?? property.Name : property.Name,
                    property.Value.TryGetProperty("rationale", out var rationale) ? rationale.GetString() : null);
                verticals.Add(vertical.Key, vertical);
                ReadGroup(property.Value, vertical);
            }

            if (root.TryGetProperty("outOfScope", out var outOfScope))
            {
                ReadGroup(outOfScope, null);
                if (outOfScope.TryGetProperty("platforms", out var platforms))
                    foreach (var platform in platforms.EnumerateArray())
                        if (Enum.TryParse<Platform>(platform.GetString(), true, out var parsed))
                            outOfScopePlatforms.Add(parsed);
            }
        }
    }

    private static Stream OpenEmbedded() =>
        typeof(CategoryScopeCatalog).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found");

    private void ReadGroup(JsonElement group, ScopeVertical? vertical)
    {
        foreach (var property in group.EnumerateObject())
        {
            if (property.NameEquals("unified"))
            {
                foreach (var slug in property.Value.EnumerateArray())
                    if (slug.GetString() is { Length: > 0 } s)
                        unifiedSlugs.Add((s, vertical));
                continue;
            }
            if (!Enum.TryParse<Platform>(property.Name, true, out var platform) || property.Value.ValueKind != JsonValueKind.Object)
                continue;
            var keywords = property.Value.TryGetProperty("titleKeywords", out var kw)
                ? kw.EnumerateArray().Select(k => Normalize(k.GetString() ?? "")).Where(k => k.Length > 0).Distinct().ToArray()
                : Array.Empty<string>();
            var scope = new PlatformCategoryScope(vertical, keywords);
            if (property.Value.TryGetProperty("ids", out var ids))
                Add(byId, platform, ids, scope);
            if (property.Value.TryGetProperty("labels", out var labels))
                Add(byLabel, platform, labels, scope);
        }
    }

    private static void Add(Dictionary<Platform, Dictionary<string, PlatformCategoryScope>> target, Platform platform,
        JsonElement values, PlatformCategoryScope scope)
    {
        if (!target.TryGetValue(platform, out var map))
            target[platform] = map = new Dictionary<string, PlatformCategoryScope>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in values.EnumerateArray())
        {
            var key = entry.ValueKind == JsonValueKind.Number ? entry.GetInt64().ToString(CultureInfo.InvariantCulture) : entry.GetString();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!map.TryAdd(key.Trim(), scope))
                throw new InvalidOperationException($"Category {platform}/{key} is listed twice in {ResourceName}");
        }
    }

    public IReadOnlyCollection<ScopeVertical> Verticals => verticals.Values;
    public IEnumerable<ScopeVertical> Active => verticals.Values.Where(v => v.IsActive);
    public IEnumerable<ScopeVertical> Next => verticals.Values.Where(v => v.IsNext);

    public ScopeVertical? Get(string key) => verticals.GetValueOrDefault(key);

    /// <summary>Unified category slugs per vertical (null vertical = out of scope).</summary>
    public IReadOnlyList<(string Slug, ScopeVertical? Vertical)> UnifiedSlugs => unifiedSlugs;

    /// <summary>
    /// Resolves the scope of a marketplace category.
    /// Returns null when the category is unknown, a scope with a null vertical when it is known to be out of scope.
    /// </summary>
    /// <param name="platform">Listing platform</param>
    /// <param name="category">Listing.Category as produced by the crawler (Vinted: catalog id, Kleinanzeigen: label)</param>
    /// <param name="categoryId">Platform category id when the crawler knows it (Kleinanzeigen detail page)</param>
    public PlatformCategoryScope? ResolvePlatform(Platform platform, string? category, string? categoryId = null)
    {
        if (outOfScopePlatforms.Contains(platform))
            return new PlatformCategoryScope(null, Array.Empty<string>());
        if (byId.TryGetValue(platform, out var ids))
        {
            if (!string.IsNullOrWhiteSpace(categoryId) && ids.TryGetValue(categoryId.Trim(), out var fromHint))
                return fromHint;
            if (!string.IsNullOrWhiteSpace(category) && ids.TryGetValue(category.Trim(), out var fromId))
                return fromId;
        }
        if (byLabel.TryGetValue(platform, out var labels) && !string.IsNullOrWhiteSpace(category)
            && labels.TryGetValue(WebUtility.HtmlDecode(category).Trim(), out var fromLabel))
            return fromLabel;
        return null;
    }

    /// <summary>
    /// Builds the unified label-path index. Only the few configured slugs are resolved, the tree itself is not kept.
    /// </summary>
    public UnifiedScopeIndex BuildUnifiedIndex(UnifiedCategoryService unified)
    {
        var wanted = unifiedSlugs.ToDictionary(s => s.Slug, s => s.Vertical, StringComparer.OrdinalIgnoreCase);
        var entries = new List<(IReadOnlyList<string>, ScopeVertical?)>();
        foreach (var (path, labels, _) in unified.GetAllCategories())
        {
            if (path.Count > 0 && wanted.TryGetValue(path[^1], out var vertical))
                entries.Add((labels, vertical));
        }
        return new UnifiedScopeIndex(entries);
    }

    /// <summary>
    /// Lowercases, strips diacritics (Pokémon -> pokemon), drops apostrophes and turns every other
    /// non alphanumeric character into a single space. Same rules as the scraper's brand matching.
    /// </summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is '\'' or '’' or '`' or '´')
                continue;
            if (c == 'ß')
            {
                builder.Append("ss");
                lastWasSpace = false;
            }
            else if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }
        return builder.ToString().TrimEnd();
    }
}
