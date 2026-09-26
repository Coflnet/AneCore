using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Coflnet.Ane;

/// <summary>
/// Deterministic listing sampling. The decision only depends on platform + listing id (salted SHA-256),
/// so replays and restarts keep or drop exactly the same listings. Never uses <see cref="Random"/>
/// or <see cref="string.GetHashCode()"/> (randomized per process).
/// </summary>
public static class ListingSampling
{
    /// <summary>Metadata key the scraper sets on sampled next-vertical listings.</summary>
    public const string ScopeMetadataKey = "scope";
    /// <summary>Value of <see cref="ScopeMetadataKey"/> for sampled listings.</summary>
    public const string SampleScope = "sample";
    /// <summary>Metadata key carrying the <see cref="ScopeVertical.Key"/> of a sampled listing.</summary>
    public const string VerticalMetadataKey = "scope_vertical";

    /// <summary>Salt for the next-vertical sample decision in the scraper.</summary>
    public const string NextVerticalSalt = "ane-next-vertical-sample-v1";

    /// <summary>
    /// Uniform value in [0, 1) derived from the salted listing identity.
    /// </summary>
    public static double Bucket(Platform platform, string listingId, string salt = NextVerticalSalt)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}|{platform}|{listingId.Trim()}"), hash);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(hash) >> 11; // 53 bits -> exact double
        return value / (double)(1UL << 53);
    }

    /// <summary>True when the listing falls into the sampled fraction <paramref name="rate"/>.</summary>
    public static bool IsSampled(Platform platform, string listingId, double rate, string salt = NextVerticalSalt)
    {
        if (rate >= 1)
            return true;
        if (rate <= 0 || string.IsNullOrWhiteSpace(listingId))
            return false;
        return Bucket(platform, listingId, salt) < rate;
    }

    /// <summary>Tags a listing as a next-vertical sample (travels via Listing.Metadata / MessagePack).</summary>
    public static void MarkSample(Listing listing, string verticalKey)
    {
        listing.Metadata ??= new Dictionary<string, string>();
        listing.Metadata[ScopeMetadataKey] = SampleScope;
        listing.Metadata[VerticalMetadataKey] = verticalKey;
    }

    /// <summary>True when the scraper tagged the listing as a next-vertical sample.</summary>
    public static bool IsSample(Listing listing, out string? verticalKey)
    {
        verticalKey = null;
        if (listing.Metadata == null
            || !listing.Metadata.TryGetValue(ScopeMetadataKey, out var scope)
            || !string.Equals(scope, SampleScope, StringComparison.Ordinal))
            return false;
        listing.Metadata.TryGetValue(VerticalMetadataKey, out verticalKey);
        return true;
    }
}
