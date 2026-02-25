using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coflnet.Ane;

/// <summary>
/// Represents a unified category with slug and display label
/// </summary>
public record UnifiedCategory(
    string Slug, 
    string Label, 
    string? AttributeExtractionPrompt = null,
    List<UnifiedCategory>? SubCategories = null,
    Dictionary<string, string>? Attributes = null
);

/// <summary>
/// Root structure for the unified categories JSON
/// </summary>
public record UnifiedCategoriesRoot(
    string Version,
    string DefaultLanguage,
    List<UnifiedCategory> Categories
);

/// <summary>
/// Service for accessing the unified category system.
/// Provides read-only access to the category hierarchy and attribute extraction information.
/// Platform-specific mappings are handled by CategoryMapper in AneNotifier.
/// </summary>
public class UnifiedCategoryService
{
    private readonly UnifiedCategoriesRoot _unifiedCategories;
    private readonly Dictionary<string, UnifiedCategory> _categoryLookup;

    /// <summary>
    /// Creates a new UnifiedCategoryService instance, loading category data from UnifiedCategories.json.
    /// </summary>
    public UnifiedCategoryService()
    {
        var unifiedPath = FindUnifiedCategoriesFile();
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var unifiedJson = File.ReadAllText(unifiedPath);
        _unifiedCategories = JsonSerializer.Deserialize<UnifiedCategoriesRoot>(unifiedJson, options)
            ?? throw new InvalidOperationException("Failed to load unified categories");

        _categoryLookup = BuildCategoryLookup();
    }

    /// <summary>
    /// Finds the UnifiedCategories.json file by searching multiple possible locations
    /// </summary>
    private static string FindUnifiedCategoriesFile()
    {
        var fileName = "UnifiedCategories.json";
        var categoryDirName = "Categories";
        
        // Get the directory where this assembly is located
        var assemblyLocation = System.Reflection.Assembly.GetAssembly(typeof(UnifiedCategoryService))?.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                // Try {assemblyDir}/Categories/UnifiedCategories.json
                var path1 = Path.Combine(assemblyDir, categoryDirName, fileName);
                if (File.Exists(path1)) return path1;

                // Try {assemblyDir}/../AneCore/Categories/UnifiedCategories.json (when referenced from other projects)
                var path2 = Path.Combine(assemblyDir, "..", "AneCore", categoryDirName, fileName);
                if (File.Exists(path2)) return Path.GetFullPath(path2);
            }
        }

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        
        // Try to find the file in the Categories directory (relative to app base)
        var unifiedPath = Path.Combine(basePath, categoryDirName, fileName);
        if (File.Exists(unifiedPath)) return unifiedPath;

        // Fallback to current directory structure for development
        unifiedPath = Path.Combine(Directory.GetCurrentDirectory(), categoryDirName, fileName);
        if (File.Exists(unifiedPath)) return unifiedPath;

        // Try searching up the directory tree from the current directory
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDir != null && currentDir.Parent != null)
        {
            var searchPath = Path.Combine(currentDir.FullName, "AneCore", categoryDirName, fileName);
            if (File.Exists(searchPath)) return searchPath;

            // Also try in current directory
            var searchPath2 = Path.Combine(currentDir.FullName, categoryDirName, fileName);
            if (File.Exists(searchPath2)) return searchPath2;

            currentDir = currentDir.Parent;
        }

        throw new FileNotFoundException($"Could not find {categoryDirName}/{fileName} in any expected location. " +
            $"Searched from assembly location and working directory: {Directory.GetCurrentDirectory()}");
    }

    private Dictionary<string, UnifiedCategory> BuildCategoryLookup()
    {
        var lookup = new Dictionary<string, UnifiedCategory>(StringComparer.OrdinalIgnoreCase);
        
        void AddCategoryRecursive(UnifiedCategory category)
        {
            lookup[category.Slug] = category;
            if (category.SubCategories != null)
            {
                foreach (var sub in category.SubCategories)
                {
                    AddCategoryRecursive(sub);
                }
            }
        }

        foreach (var category in _unifiedCategories.Categories)
        {
            AddCategoryRecursive(category);
        }
        
        return lookup;
    }

    /// <summary>
    /// Gets the attribute extraction prompt for a specific category path.
    /// </summary>
    public string? GetAttributeExtractionPrompt(List<string> categoryPath)
    {
        if (categoryPath.Count == 0) return null;

        // Navigate through the category tree, matching by slug or label
        UnifiedCategory? current = null;
        List<UnifiedCategory> searchIn = _unifiedCategories.Categories;

        foreach (var segment in categoryPath)
        {
            current = searchIn.FirstOrDefault(c =>
                c.Slug.Equals(segment, StringComparison.OrdinalIgnoreCase) ||
                c.Label.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (current == null) return null;
            
            if (current.SubCategories != null)
            {
                searchIn = current.SubCategories;
            }
        }

        return current?.AttributeExtractionPrompt;
    }

    /// <summary>
    /// Gets structured attributes to extract for a category path.
    /// Returns a dictionary of attribute_name -> sample_value for the LLM.
    /// </summary>
    public Dictionary<string, string>? GetAttributesToExtract(List<string> categoryPath)
    {
        if (categoryPath.Count == 0) return null;

        // Navigate through the category tree, matching by slug or label
        UnifiedCategory? current = null;
        List<UnifiedCategory> searchIn = _unifiedCategories.Categories;

        foreach (var segment in categoryPath)
        {
            current = searchIn.FirstOrDefault(c =>
                c.Slug.Equals(segment, StringComparison.OrdinalIgnoreCase) ||
                c.Label.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (current == null) return null;
            
            if (current.SubCategories != null)
            {
                searchIn = current.SubCategories;
            }
        }

        return current?.Attributes;
    }

    /// <summary>
    /// Resolves a category slug to its full path in the unified category tree.
    /// </summary>
    /// <param name="slug">The category slug to resolve (e.g., "netzwerk")</param>
    /// <returns>Full path (e.g., ["elektronik", "netzwerk"]) or null if not found</returns>
    public List<string>? ResolveCategoryPath(string slug)
    {
        if (_categoryLookup.TryGetValue(slug, out var category))
        {
            return FindPathToSlug(slug);
        }
        return null;
    }

    private List<string>? FindPathToSlug(string targetSlug)
    {
        List<string>? SearchRecursive(List<UnifiedCategory> categories, List<string> currentPath)
        {
            foreach (var cat in categories)
            {
                var newPath = new List<string>(currentPath) { cat.Slug };
                
                if (cat.Slug.Equals(targetSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return newPath;
                }
                
                if (cat.SubCategories != null)
                {
                    var found = SearchRecursive(cat.SubCategories, newPath);
                    if (found != null) return found;
                }
            }
            return null;
        }

        return SearchRecursive(_unifiedCategories.Categories, new List<string>());
    }

    /// <summary>
    /// Gets all unified categories as a flat list with their full paths.
    /// </summary>
    public IEnumerable<(List<string> Path, List<string> Labels, string? AttributePrompt)> GetAllCategories()
    {
        IEnumerable<(List<string>, List<string>, string?)> Traverse(UnifiedCategory category, List<string> parentPath, List<string> parentLabels)
        {
            var currentPath = new List<string>(parentPath) { category.Slug };
            var currentLabels = new List<string>(parentLabels) { category.Label };
            
            yield return (currentPath, currentLabels, category.AttributeExtractionPrompt);
            
            if (category.SubCategories != null)
            {
                foreach (var sub in category.SubCategories)
                {
                    foreach (var result in Traverse(sub, currentPath, currentLabels))
                    {
                        yield return result;
                    }
                }
            }
        }

        foreach (var category in _unifiedCategories.Categories)
        {
            foreach (var result in Traverse(category, new List<string>(), new List<string>()))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Gets category information by slug.
    /// </summary>
    public UnifiedCategory? GetCategory(string slug)
    {
        return _categoryLookup.GetValueOrDefault(slug);
    }

    /// <summary>
    /// Gets all top-level categories.
    /// </summary>
    public IEnumerable<UnifiedCategory> GetTopLevelCategories()
    {
        return _unifiedCategories.Categories;
    }

    /// <summary>
    /// Gets subcategories for a given parent slug.
    /// </summary>
    public IEnumerable<UnifiedCategory>? GetSubCategories(string parentSlug)
    {
        var parent = _unifiedCategories.Categories.FirstOrDefault(c => 
            c.Slug.Equals(parentSlug, StringComparison.OrdinalIgnoreCase));
        return parent?.SubCategories;
    }

    /// <summary>
    /// Gets the supported marketplaces.
    /// (This is a placeholder for compatibility; actual marketplace support
    /// is managed by CategoryMapper in AneNotifier)
    /// </summary>
    public virtual IEnumerable<string> GetSupportedMarketplaceKeys()
    {
        return new[] { "kleinanzeigen.de", "willhaben.at", "marktplaats.nl", "leboncoin.fr" };
    }

    /// <summary>
    /// Checks if a given path matches or is a parent of another path.
    /// Useful for filtering listings by category hierarchy.
    /// </summary>
    public static bool CategoryMatches(List<string> filterPath, List<string> listingPath)
    {
        if (filterPath.Count > listingPath.Count) return false;
        
        for (int i = 0; i < filterPath.Count; i++)
        {
            if (!filterPath[i].Equals(listingPath[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        
        return true;
    }
}
