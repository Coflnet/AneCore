using System.Security.Cryptography;
using System.Text;

namespace Coflnet.Ane.TrainingListings;

/// <summary>
/// Coarse, keyed and non-reversible seller grouping for the training data: the first 14 bits of
/// <c>HMAC-SHA256(key, "{platform}:{userId}")</c>, a number 0..16383. Only this number is ever put into a <see cref="TrainingListing"/>
/// (Kafka, store, export); the raw seller id and the full hash never leave <see cref="TrainingListingBuilder.Build"/>. With ~16k buckets
/// many sellers share a bucket, so it cannot identify a seller, but listings of one seller always share it, which lets the training
/// split sellers between train and test and weights "same seller" pairs. There is no unkeyed fallback: without a key the bucket is null.
/// The platform enum is hashed by its number, so renaming a platform does not change the buckets.
/// </summary>
public static class SellerBucket
{
    public const int Bits = 14;
    public const int Count = 1 << Bits;
    /// <summary>Configuration key (environment <c>TRAINING_LISTINGS__SELLER_BUCKET_KEY</c>, secret in OpenBao next to the other secrets of the service).</summary>
    public const string ConfigKey = "TRAINING_LISTINGS:SELLER_BUCKET_KEY";

    /// <summary>The key bytes of the configured text, null when it is missing or blank (no bucket is computed then).</summary>
    public static byte[]? ParseKey(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? null : Encoding.UTF8.GetBytes(configured.Trim());

    /// <summary>The bucket, or null without a key or without a usable seller id (empty, or the <c>0</c> the Kleinanzeigen parser uses when the page names no seller).</summary>
    public static int? Compute(byte[]? key, Platform platform, string? userId)
    {
        if (key == null || key.Length == 0 || string.IsNullOrWhiteSpace(userId) || userId.Trim() == "0")
            return null;
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{(int)platform}:{userId.Trim()}"));
        return (mac[0] << 6) | (mac[1] >> 2);
    }
}
