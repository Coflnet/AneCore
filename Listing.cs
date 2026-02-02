using System.ComponentModel.DataAnnotations;
using MessagePack;
using Newtonsoft.Json;
using KeyAttribute = MessagePack.KeyAttribute;

namespace Coflnet.Ane;

[MessagePackObject]
public class Listing
{
    [Key(0)]
    public string Id { get; set; }

    [Key(1)]
    public string? Title { get; set; }

    [Key(2)]
    public string? DescriptionShort { get; set; }

    [Key(3)]
    public string? Description { get; set; }

    [Key(4)]
    public string? Category { get; set; }

    [Key(5)]
    public string? Street { get; set; }

    [Key(6)]
    public string? Locality { get; set; }

    [Key(7)]
    public string? Region { get; set; }

    [Key(8)]
    public string? Country { get; set; }

    [Key(9)]
    public string[]? ImageUrls { get; set; }

    [Key(10)]
    public double? Price { get; set; }

    [Key(11)]
    public string? Currency { get; set; }

    [Key(12)]
    public string? Contact { get; set; }

    [Key(13)]
    public string? UserId { get; set; } = null;

    [Key(14)]
    public float? Latitude { get; set; }

    [Key(15)]
    public float? Longitude { get; set; }

    [Key(16)]
    public string? Shipping { get; set; }

    [Key(17)]
    public string? ReturnPolicy { get; set; }

    [Key(18)]
    public virtual PriceKind PriceKind { get; set; }

    [Key(19)]
    public Dictionary<string, string>? Attributes { get; set; }

    [Key(20)]
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;

    [Key(21)]
    public DateTime? CreatedAt { get; set; }

    [Key(22)]
    public DateTime? SoldBefore { get; set; }
    /// <summary>
    /// Is this offered by a busniness or a private person
    /// </summary>
    [Key(23)]
    public bool? Commercial { get; set; }
    /// <summary>
    /// Extra metadata for the listing, aggregations from other listings etc.
    /// </summary>
    [Key(24)]
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, string>? Metadata { get; set; }
    [Key(25)]
    public Platform Platform { get; set; }
    /// <summary>
    /// SEO Id used for product indexing
    /// </summary>
    [Key(26)]
    public string SeoId { get; set; }
    [Key(27)]
    public string[]? Categories { get; set; }

    public Listing()
    {
        Id = Guid.NewGuid().ToString();
        PriceKind = PriceKind.Unknown;
        Attributes = new Dictionary<string, string>();
        Metadata = new Dictionary<string, string>();
    }

    public Listing(Listing other)
    {
        Id = other.Id;
        Title = other.Title;
        DescriptionShort = other.DescriptionShort;
        Description = other.Description;
        Category = other.Category;
        Street = other.Street;
        Locality = other.Locality;
        Region = other.Region;
        Country = other.Country;
        ImageUrls = other.ImageUrls;
        Price = other.Price;
        Currency = other.Currency;
        Contact = other.Contact;
        UserId = other.UserId;
        Latitude = other.Latitude;
        Longitude = other.Longitude;
        Shipping = other.Shipping;
        ReturnPolicy = other.ReturnPolicy;
        PriceKind = other.PriceKind;
        Attributes = other.Attributes == null ? null : new Dictionary<string, string>(other.Attributes);
        FoundAt = other.FoundAt;
        CreatedAt = other.CreatedAt;
        SoldBefore = other.SoldBefore;
        Commercial = other.Commercial;
        Metadata = other.Metadata == null ? null : new Dictionary<string, string>(other.Metadata);
        Platform = other.Platform;
    }

    public static string GetUrlForListing(string locale, Platform platform, string id)
    {
        var localDomain = locale switch
        {
            "de-DE" => "de",
            "de-AT" => "at",
            "de-CH" => "ch",
            "nl-NL" => "nl",
            "nl-BE" => "be",
            "pl-PL" => "pl",
            "it-IT" => "it",
            "pt-PT" => "pt",
            "pt-BR" => "br",
            "da-DK" => "dk",
            "sv-SE" => "se",
            "no-NO" => "no",
            "fi-FI" => "fi",
            "en-GB" => "co.uk",
            "en-US" => "com",
            "fr-FR" => "fr",
            "es-ES" => "es",
            _ => "com"
        };
        if (platform == Platform.Ebay)
            return $"https://ebay.{localDomain}/itm/{id}";
        if (platform == Platform.Kleinanzeigen)
            return $"https://www.kleinanzeigen.de/s-anzeige/copy/{id}-1-1";
        if (platform == Platform.Craigslist)
            return "https://" + id; // craiglist has multiple domains
        if (platform == Platform.Facebook)
            return $"https://www.facebook.com/marketplace/item/{id}";
        if (platform == Platform.Willhaben)
            return $"https://www.willhaben.at/iad/{id.TrimStart('/')}";
        if (platform == Platform.Gumtree)
            return $"https://www.gumtree.com/p/redirect/redirect/{id}";
        if (platform == Platform.Shpock)
            return $"https://www.shpock.com/{locale}/i/{id}";
        if (platform == Platform.Marktplaats)
            if (id.Contains('/'))
                return $"https://www.marktplaats.nl{id}";
            else
                return $"https://www.marktplaats.nl/v/redirect/redirect/{id}-test";
        return "https://ane.deals/auctions?platform=" + platform.ToString().ToLower() + "&id=" + Uri.EscapeDataString(id);
    }
}