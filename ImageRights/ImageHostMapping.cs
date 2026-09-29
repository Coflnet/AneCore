namespace Coflnet.Ane.ImageRights;

/// <summary>Maps the host of a photo URL to the marketplace site whose robots.txt/tdmrep also applies and to its platform.</summary>
public static class ImageHostMapping
{
    public sealed record Entry(string ImageHost, string SiteHost, Platform Platform);

    /// <summary>Hosts the refresher reads at start and every 24 hours. Anything else has no stored status and is Unknown.</summary>
    public static readonly IReadOnlyList<Entry> KnownHosts =
    [
        new("images1.vinted.net", "www.vinted.de", Platform.Vinted),
        new("img.kleinanzeigen.de", "www.kleinanzeigen.de", Platform.Kleinanzeigen),
        new("i.ebayimg.com", "www.ebay.de", Platform.Ebay),
        new("a.marktplaats.nl", "www.marktplaats.nl", Platform.Marktplaats),
        new("images.marktplaats.nl", "www.marktplaats.nl", Platform.Marktplaats),
        new("cache.willhaben.at", "www.willhaben.at", Platform.Willhaben),
    ];

    /// <summary>Suffix rules for hosts that are not listed explicitly (e.g. <c>images2.vinted.net</c>).</summary>
    private static readonly (string Suffix, string SiteHost, Platform Platform)[] SuffixRules =
    [
        (".vinted.net", "www.vinted.de", Platform.Vinted),
        (".ebayimg.com", "www.ebay.de", Platform.Ebay),
        (".marktplaats.nl", "www.marktplaats.nl", Platform.Marktplaats),
        (".willhaben.at", "www.willhaben.at", Platform.Willhaben),
    ];

    public static Entry? TryGet(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;
        host = host.Trim().ToLowerInvariant();
        var known = KnownHosts.FirstOrDefault(e => e.ImageHost == host);
        if (known != null)
            return known;
        foreach (var (suffix, site, platform) in SuffixRules)
            if (host.EndsWith(suffix, StringComparison.Ordinal))
                return new Entry(host, site, platform);
        return null;
    }

    /// <summary>Host of an absolute photo URL (protocol-relative <c>//host/x</c> is accepted), or null.</summary>
    public static string? HostOf(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;
        var url = imageUrl.StartsWith("//") ? "https:" + imageUrl : imageUrl;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;
    }
}
