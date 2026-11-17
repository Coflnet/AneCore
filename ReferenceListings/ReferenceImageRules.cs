using System.Text.RegularExpressions;

namespace Coflnet.Ane.ReferenceListings;

/// <summary>Image URL classification: site chrome (logos, icons) and photo identity across renditions.</summary>
internal static class ReferenceImageRules
{
    private static readonly string[] ChromeDirectories = ["icon", "icons", "logo", "logos", "sprite", "sprites", "placeholders"];
    private static readonly string[] ChromeFileNames = ["logo", "placeholder", "sprite", "spacer", "blank", "pixel", "connection-issue", "no-image", "noimage"];
    private static readonly HashSet<string> RenditionParams = new(StringComparer.OrdinalIgnoreCase)
        { "rule", "w", "width", "h", "height", "quality", "q", "v", "s", "fit", "auto" };

    public static bool IsSiteChrome(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return true;
        var path = PathOf(url);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;
        var file = segments[^1];
        if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return true;
        var baseName = Path.GetFileNameWithoutExtension(file);
        return ChromeFileNames.Contains(baseName, StringComparer.OrdinalIgnoreCase)
            || segments[..^1].Any(d => ChromeDirectories.Contains(d, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>URL without rendition parameters; photos differing in other parameters (id=, url=) stay distinct.</summary>
    public static string Identity(string url)
    {
        var fragment = url.IndexOf('#');
        if (fragment >= 0)
            url = url[..fragment];
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
            return url;
        var kept = url[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !RenditionParams.Contains(p.Split('=')[0]))
            .OrderBy(p => p, StringComparer.Ordinal);
        return url[..queryStart] + "?" + string.Join('&', kept);
    }

    private static string PathOf(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : Regex.Replace(url, @"[?#].*$", string.Empty);
}
