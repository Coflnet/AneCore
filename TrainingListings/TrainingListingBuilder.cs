namespace Coflnet.Ane.TrainingListings;

/// <summary>Pure conversion of a scraped <see cref="Listing"/> into a <see cref="TrainingListing"/> without personal data.</summary>
public static class TrainingListingBuilder
{
    /// <summary>
    /// Substrings of an attribute key that mark seller data: the first six are a copy of the notifier's <c>SellerDataKeyPatterns</c>, the
    /// rest are the German, Dutch and French words of the raw keys the scrapers store (the notifier sees normalized English keys).
    /// </summary>
    public static readonly string[] SellerDataKeyPatterns =
    {
        "address", "contact", "phone", "email", "org", "seller",
        "kontakt", "anbieter", "verkaufer", "verkäufer", "telefon", "adresse", "strasse", "straße", "impressum",
        "vendeur", "telephone", "téléphone", "adresse", "verkoper", "telefoon", "adres", "straat"
    };

    /// <summary>True for keys like <c>phone</c>, <c>contact/name</c>, <c>E-Mail</c> or <c>Anbieter Kontakt</c>; case, spaces and punctuation do not matter.</summary>
    public static bool IsSellerDataAttributeKey(string key)
    {
        var compact = new string(key.Where(char.IsLetterOrDigit).ToArray());
        return SellerDataKeyPatterns.Any(p => compact.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The training row of <paramref name="listing"/>, or null when it must not be published: a platform with a rights reservation
    /// (<see cref="TrainingListingPolicy.Forbidden"/>), a missing id, or a listing without any text (error, ban or empty page).
    /// </summary>
    /// <param name="scope">One of <see cref="TrainingListingScope"/>.</param>
    /// <param name="scopeReason">Drop reason or vertical of the scope decision.</param>
    /// <param name="imageUrls">All photo urls when the crawler knows more than <see cref="Listing.ImageUrls"/>.</param>
    public static TrainingListing? Build(Listing listing, string scope, string? scopeReason = null, IEnumerable<string>? imageUrls = null)
    {
        if (TrainingListingPolicy.IsForbidden(listing.Platform) || listing.Platform == Platform.Unknown)
            return null;
        if (string.IsNullOrWhiteSpace(listing.Id))
            return null;
        if (string.IsNullOrWhiteSpace(listing.Title) && string.IsNullOrWhiteSpace(listing.Description) && string.IsNullOrWhiteSpace(listing.DescriptionShort))
            return null;
        var attributes = listing.Attributes?
            .Where(a => !string.IsNullOrWhiteSpace(a.Key) && !IsSellerDataAttributeKey(a.Key))
            .ToDictionary(a => a.Key, a => TrainingTextSanitizer.Mask(a.Value) ?? "");
        string? condition = null;
        attributes?.TryGetValue("condition", out condition);
        var photos = (imageUrls ?? listing.ImageUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u) && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            .Select(u => u.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        var locale = listing.Platform == Platform.Marktplaats ? "nl-NL" : "de-DE";
        return new TrainingListing
        {
            Platform = listing.Platform,
            ListingId = listing.Id,
            Url = Listing.GetUrlForListing(locale, listing.Platform, listing.Id),
            Title = TrainingTextSanitizer.Mask(listing.Title),
            Description = TrainingTextSanitizer.Mask(listing.Description),
            DescriptionShort = TrainingTextSanitizer.Mask(listing.DescriptionShort),
            Category = listing.Category,
            Categories = listing.Categories,
            Attributes = attributes,
            Price = listing.Price is >= 0 ? listing.Price : null, // the scrapers use -1 for "no price"
            Currency = listing.Currency,
            PriceKind = listing.PriceKind,
            Condition = condition,
            ImageUrls = photos.Length == 0 ? null : photos,
            Country = listing.Country,
            Region = listing.Region,
            Locality = TrainingTextSanitizer.Mask(listing.Locality),
            Commercial = listing.Commercial,
            Shipping = TrainingTextSanitizer.Mask(listing.Shipping),
            CreatedAt = listing.CreatedAt,
            FirstSeenAt = listing.FoundAt,
            Scope = scope,
            ScopeReason = scopeReason
        };
    }
}
