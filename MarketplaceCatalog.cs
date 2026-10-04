using System.Text.RegularExpressions;

namespace Coflnet.Ane;

/// <summary>Supported resale listing URLs. A domain alone never makes a search/profile page a listing.</summary>
public static class MarketplaceCatalog
{
    public sealed record Marketplace(Platform Platform, string[] Domains, string ListingPattern, string Country = "", string Currency = "");
    public sealed record ListingAddress(Platform Platform, string Id, string Url, string Country, string Currency);

    public static readonly IReadOnlyList<Marketplace> Marketplaces =
    [
        new(Platform.Ebay, ["ebay.de", "ebay.at", "ebay.ch", "ebay.co.uk", "ebay.fr", "ebay.it", "ebay.es", "ebay.nl", "ebay.be", "ebay.com.be", "ebay.pl", "ebay.ie", "ebay.com"], @"^/itm/(?:[^/]+/)?(?<id>\d+)(?:/|$)"),
        new(Platform.Kleinanzeigen, ["kleinanzeigen.de"], @"^/s-anzeige/[^/]+/(?<id>\d+)-\d+-\d+/?$", "DE"),
        new(Platform.Marktplaats, ["marktplaats.nl"], @"^/v/.*/(?<id>[ma]\d+)(?:-[^/]*)?/?$", "NL"),
        new(Platform.Willhaben, ["willhaben.at"], @"^/iad/(?<id>kaufen-und-verkaufen/d/[^/]+-\d+)/?$", "AT"),
        new(Platform.Vinted, ["vinted.de", "vinted.at", "vinted.fr", "vinted.it", "vinted.es", "vinted.pt", "vinted.nl", "vinted.be", "vinted.pl", "vinted.cz", "vinted.sk", "vinted.hu", "vinted.ro", "vinted.hr", "vinted.lt", "vinted.lv", "vinted.ee", "vinted.fi", "vinted.se", "vinted.dk", "vinted.gr", "vinted.ie", "vinted.co.uk", "vinted.lu", "vinted.com"], @"^/items/(?<id>\d+)(?:-[^/]*)?/?$"),
        new(Platform.Shpock, ["shpock.com"], @"^/(?:[^/]+/)?i/(?<id>[^/]+)(?:/[^/]*)?/?$"),
        new(Platform.Facebook, ["facebook.com"], @"^/marketplace/item/(?<id>\d+)/?$"),
        new(Platform.Lebecoin, ["leboncoin.fr"], @"^/ad/[^/]+/(?<id>\d+)/?$", "FR"),
        new(Platform.Wallapop, ["wallapop.com"], @"^/item/[^/]+-(?<id>\d+)/?$", "ES"),
        new(Platform.OLX, ["olx.pl", "olx.pt", "olx.ro", "olx.bg", "olx.ua"], @"^/(?:d/)?(?:oferta|anuncio|item)/[^/]*-(?<id>ID[a-zA-Z0-9]+)\.html/?$"),
        new(Platform.Subito, ["subito.it"], @"^/[^/]+/[^/]+-(?<id>\d+)\.htm$", "IT"),
        new(Platform.Milanuncios, ["milanuncios.com"], @"^/[^/]+/[^/]+-(?<id>\d+)\.htm$", "ES"),
        new(Platform.Allegro, ["allegro.pl", "allegro.cz", "allegro.sk", "allegro.hu"], @"^/(?:oferta|nabidka|ponuka|ajanlat)/[^/]+-(?<id>\d+)/?$"),
        new(Platform.AllegroLokalnie, ["allegrolokalnie.pl"], @"^/oferta/(?<id>[^/]+)/?$", "PL", "PLN"),
        new(Platform.TwoDehands, ["2dehands.be", "2ememain.be"], @"^/v/.*/(?<id>[ma]\d+)(?:-[^/]*)?/?$", "BE"),
        new(Platform.Ricardo, ["ricardo.ch"], @"^/[^/]+/a/[^/]+-(?<id>\d+)/?$", "CH", "CHF"),
        new(Platform.Tutti, ["tutti.ch"], @"^/[^/]+/vi/.*/(?<id>\d+)/?$", "CH", "CHF"),
        new(Platform.Anibis, ["anibis.ch"], @"^/[^/]+/d/[^/]+-(?<id>\d+)/?$", "CH", "CHF"),
        new(Platform.Tradera, ["tradera.com"], @"^/item/\d+/(?<id>\d+)(?:/[^/]*)?/?$", "SE", "SEK"),
        new(Platform.Blocket, ["blocket.se"], @"^/annons/(?:[^/]+/)*(?<id>\d+)/?$", "SE", "SEK"),
        new(Platform.Finn, ["finn.no"], @"^/(?:bap/forsale/ad\.html|recommerce/forsale/item/(?<id>\d+))/?$", "NO", "NOK"),
        new(Platform.Tori, ["tori.fi"], @"^/(?:recommerce/forsale/item/(?<id>\d+)|[^/]+/[^/]+_(?<id>\d+)\.htm)/?$", "FI"),
        new(Platform.DBA, ["dba.dk"], @"^/(?:recommerce/forsale/item/(?<id>\d+)|[^/]+/id-(?<id>\d+))/?$", "DK", "DKK"),
        new(Platform.Bazos, ["bazos.cz", "bazos.sk", "bazos.pl"], @"^/inzerat/(?<id>\d+)(?:/[^/]*)?/?$"),
        new(Platform.Sbazar, ["sbazar.cz"], @"^/[^/]+/detail/(?<id>\d+)(?:-[^/]*)?/?$", "CZ", "CZK"),
        new(Platform.Adverts, ["adverts.ie"], @"^/[^/]+/[^/]+/(?<id>\d+)/?$", "IE"),
        new(Platform.DoneDeal, ["donedeal.ie"], @"^/[^/]+/[^/]+/(?<id>\d+)/?$", "IE"),
        new(Platform.ParuVendu, ["paruvendu.fr"], @"^/annonces/.*/(?<id>\d+)A\d+.*$", "FR"),
        new(Platform.Rakuten, ["fr.shopping.rakuten.com"], @"^/offer/buy/(?<id>\d+)(?:/[^/]*)?/?$", "FR"),
        new(Platform.Catawiki, ["catawiki.com"], @"^/[^/]+/l/(?<id>\d+)(?:-[^/]*)?/?$"),
        new(Platform.Delcampe, ["delcampe.net"], @"^/[^/]+/collectibles/.*/[^/]+-(?<id>\d+)\.html$"),
        new(Platform.Depop, ["depop.com"], @"^/products/(?<id>[^/]+)/?$"),
        new(Platform.Gumtree, ["gumtree.com"], @"^/p/.*/(?<id>\d+)/?$", "GB", "GBP"),
        new(Platform.Quoka, ["quoka.de"], @"^/anzeigen/.*/(?:ad|anzeige)/[^/]+/(?<id>[^/]+)\.html$", "DE"),
        new(Platform.MarktDe, ["markt.de"], @"^/.*?/a/(?<id>[^/]+)/?$", "DE"),
        new(Platform.Vendora, ["vendora.gr", "vendora.cy"], @"^/items/(?<id>[^/]+)(?:/[^/]*)?/?$"),
        new(Platform.Jofogas, ["jofogas.hu"], @"^/[^/]+/[^/]+_(?<id>\d+)\.htm$", "HU", "HUF"),
        new(Platform.Njuskalo, ["njuskalo.hr"], @"^/[^/]+/[^/]+-oglas-(?<id>\d+)/?$", "HR")
    ];

    public static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    public static Platform DetectPlatform(string host) =>
        Marketplaces.FirstOrDefault(m => m.Domains.Any(d => HostMatches(host, d)))?.Platform ?? Platform.Unknown;

    public static bool TryResolve(string? url, out ListingAddress address)
    {
        address = null!;
        if (url?.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 || !uri.IsDefaultPort)
            return false;
        var marketplace = Marketplaces.FirstOrDefault(m => m.Domains.Any(d => HostMatches(uri.IdnHost, d)));
        if (marketplace == null)
            return false;
        var match = Regex.Match(uri.AbsolutePath, marketplace.ListingPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        if (!match.Success)
            return false;
        var id = match.Groups["id"].Value;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (marketplace.Platform == Platform.Finn && id.Length == 0)
            id = query["finnkode"] ?? "";
        if (id.Length == 0 || id.Length > 512 || marketplace.Platform == Platform.Finn && !id.All(char.IsAsciiDigit))
            return false;
        var canonical = new UriBuilder(uri) { Scheme = "https", Port = -1, Fragment = "", Query = "" };
        if (marketplace.Platform == Platform.Finn && uri.AbsolutePath.EndsWith("ad.html"))
            canonical.Query = "finnkode=" + id;
        var country = marketplace.Country;
        if (marketplace.Platform == Platform.Wallapop)
            country = uri.IdnHost.StartsWith("it.") ? "IT" : uri.IdnHost.StartsWith("pt.") ? "PT" : "";
        if (country.Length == 0)
        {
            var suffix = uri.IdnHost.Split('.').Last().ToUpperInvariant();
            country = suffix.Length == 2 ? suffix : suffix == "UK" ? "GB" : "";
        }
        if (country == "UK") country = "GB";
        if (marketplace.Platform == Platform.Ebay) country = "";
        var currency = country switch { "PL" => "PLN", "CZ" => "CZK", "HU" => "HUF", "RO" => "RON", "SE" => "SEK", "NO" => "NOK", "DK" => "DKK", "CH" => "CHF", "GB" => "GBP", "DE" or "AT" or "FR" or "IT" or "ES" or "PT" or "NL" or "BE" or "IE" or "FI" or "HR" or "GR" or "CY" or "BG" or "SK" or "EE" or "LV" or "LT" or "LU" => "EUR", _ => marketplace.Currency };
        if (marketplace.Platform == Platform.Ebay && HostMatches(uri.IdnHost, "ebay.com")) currency = "USD";
        if (marketplace.Platform is Platform.OLX or Platform.Bazos)
            id = uri.IdnHost.Split('.').Last().ToLowerInvariant() + ":" + id;
        address = new(marketplace.Platform, id, canonical.Uri.AbsoluteUri, country, currency);
        return true;
    }

    public static string ResolveUrl(Platform platform, string id, string? storedUrl, string locale)
        => TryResolve(storedUrl, out var address) && address.Platform == platform && address.Id == id
            ? address.Url : Listing.GetUrlForListing(locale, platform, id);
}
