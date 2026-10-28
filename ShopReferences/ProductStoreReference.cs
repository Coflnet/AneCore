using System.Text.RegularExpressions;
using Coflnet.Ane.KnownProducts;

namespace Coflnet.Ane.ShopReferences;

public sealed record ProductStoreReference(string KnownProductId, string Provider, string Url, string MerchantSku,
    string Brand, string Mpn, string? Gtin, decimal Price, string Currency, string Availability, string Condition,
    DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt);

public interface IProductStoreReferenceStore
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductStoreReference>> GetAsync(string productId, CancellationToken ct = default);
    Task UpsertAsync(string productId, ProductStoreReference reference, CancellationToken ct = default);
}

/// <summary>A dated store offer for one verified article, independently of the used offer's condition.</summary>
public static class ProductStoreReferencePolicy
{
    private static readonly Regex Bundle = new(@"\b(?:set|satz|bundle|konvolut|paket|sortiment|multipack|doppelpack|[2-9]\d*\s*(?:stück|stk|er[ -]*pack|[x×]))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string Article(string? value) => Regex.Replace(value ?? "", @"[\s-]+", "").ToUpperInvariant();

    public static bool IsCandidate(Product product) => string.IsNullOrWhiteSpace(product.CanonicalSeoId)
        && !product.SeoId.StartsWith("unspecific-", StringComparison.OrdinalIgnoreCase)
        && !product.SeoId.EndsWith("-unspecific", StringComparison.OrdinalIgnoreCase)
        && product.Attributes?.Any(attribute => string.Equals(attribute.Key, "specificity", StringComparison.OrdinalIgnoreCase)
            && string.Equals(attribute.Value?.Trim(), "unspecific", StringComparison.OrdinalIgnoreCase)) != true
        && product.IdentifierType != "hand_tool_offer"
        && (string.Equals(product.Brand, "KNIPEX", StringComparison.OrdinalIgnoreCase) || string.Equals(product.Brand, "Bahco", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(product.Model) && product.Model.Any(char.IsAsciiDigit)
        && !Bundle.IsMatch(product.Name ?? "")
        && (product.Attributes?.TryGetValue("product_kind", out var kind) != true || kind == "hand_tool");

    public static bool IsFresh(ProductStoreReference reference, DateTimeOffset now) => reference.Provider == "Lefeld"
        && reference.Price > 0 && reference.Currency == "EUR" && reference.Availability is "InStock" or "LimitedAvailability"
        && reference.Condition is "new" or "unknown" && reference.ObservedAt <= now && reference.ExpiresAt > now
        && reference.ExpiresAt == reference.ObservedAt.AddMinutes(15)
        && !string.IsNullOrWhiteSpace(reference.KnownProductId) && !string.IsNullOrWhiteSpace(reference.MerchantSku)
        && !string.IsNullOrWhiteSpace(reference.Brand) && !string.IsNullOrWhiteSpace(reference.Mpn)
        && Uri.TryCreate(reference.Url, UriKind.Absolute, out var url) && url.Scheme == "https" && url.Host == "www.lefeld.de"
        && url.IsDefaultPort && url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0
        && Regex.IsMatch(url.AbsolutePath, @"^/(?:knipex|bahco)-[a-zA-Z0-9%-]+$");

    public static bool IsEligible(ProductStoreReference reference, Product product, IReadOnlyList<KnownProduct> catalogue, DateTimeOffset now)
    {
        if (!IsCandidate(product) || !IsFresh(reference, now)) return false;
        var known = catalogue.FirstOrDefault(row => row.Id == reference.KnownProductId);
        return known != null && string.Equals(known.Brand, product.Brand, StringComparison.OrdinalIgnoreCase)
            && string.Equals(known.Brand, reference.Brand, StringComparison.OrdinalIgnoreCase)
            && known.PossibleAttributes.GetValueOrDefault("product_kind")?.Contains("hand_tool") == true
            && known.PossibleAttributes.TryGetValue("article_number", out var articles) && articles.Count == 1
            && Article(articles.Single()) == Article(reference.Mpn) && Article(known.Model) == Article(reference.Mpn)
            && Article(product.Model) == Article(reference.Mpn)
            && (product.IdentifierType != "catalogued_goods" || string.IsNullOrWhiteSpace(product.IdentifierValue) || product.IdentifierValue == known.Id)
            && (product.Attributes?.TryGetValue("article_number", out var claimed) != true || Article(claimed) == Article(reference.Mpn));
    }
}
