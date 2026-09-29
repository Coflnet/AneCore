using System.Security.Cryptography;
using System.Text;
using Coflnet.Ane.Embeddings;
using Coflnet.Ane.ImageRights;

namespace Coflnet.Ane.TrainingPhotos;

/// <summary>
/// One archived, downscaled photo of an offer whose image host allows training. Row of <c>training_photos</c>,
/// partition <c>(platform, listing_id)</c>, clustering <c>image_index</c>, no TTL. Holds no personal data of the
/// seller: no name, location, title or description. Platforms are stored as int, timestamps are UTC.
/// </summary>
public class TrainingPhoto
{
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    public int ImageIndex { get; set; }
    public string ImageUrl { get; set; } = "";
    /// <summary>Image host (lower case), the unit the rights status is kept for.</summary>
    public string Host { get; set; } = "";
    /// <summary>Downscaled JPEG (about 20 to 60 KB). Null when read with <c>includeJpeg: false</c>.</summary>
    public byte[]? Jpeg { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Lower-hex SHA-256 of the original bytes.</summary>
    public string Sha256 { get; set; } = "";
    /// <summary>float32 little-endian, L2 normalised, encoded with <see cref="EmbeddingBlobCodec"/>.</summary>
    public byte[]? Embedding { get; set; }
    public string ModelId { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public string Brand { get; set; } = "";
    public string GarmentType { get; set; } = "";
    public string Category { get; set; } = "";
    /// <summary>When the offer was first seen.</summary>
    public DateTime? FirstSeenAt { get; set; }
    /// <summary>When the offer was last verified to be still online.</summary>
    public DateTime? VerifiedOnlineAt { get; set; }
    /// <summary>When the photo was archived; set by the store when missing. Also decides the day index bucket.</summary>
    public DateTime? ArchivedAt { get; set; }
    /// <summary>Name of <see cref="ImageRightsStatus"/> at archive time (text).</summary>
    public string RightsStatus { get; set; } = "";
    public DateTime? RightsCheckedAt { get; set; }
    /// <summary><see cref="TrainingPhotoSources.Pair"/> or <see cref="TrainingPhotoSources.Catalogue"/>.</summary>
    public string Source { get; set; } = "";

    /// <summary>Not a column.</summary>
    public Platform Platform
    {
        get => (Platform)PlatformValue;
        set => PlatformValue = (int)value;
    }

    /// <summary>Not a column: the decoded embedding, or null when there is none.</summary>
    public float[]? EmbeddingVector => Embedding is { Length: EmbeddingBlobCodec.ByteLength } b ? EmbeddingBlobCodec.Decode(b) : null;

    public TrainingPhoto Clone() => (TrainingPhoto)MemberwiseClone();

    public void SetEmbedding(float[] vector) => Embedding = EmbeddingBlobCodec.Encode(vector);
}

public static class TrainingPhotoSources
{
    public const string Pair = "pair";
    public const string Catalogue = "catalogue";
}

/// <summary>Row of <c>training_photos_by_day</c>, partition <c>(day, shard)</c>: lets the archive be listed and counted per day.</summary>
public class TrainingPhotoDayEntry
{
    /// <summary>UTC day <c>yyyy-MM-dd</c> of <c>archived_at</c>.</summary>
    public string Day { get; set; } = "";
    /// <summary>0..7, derived from platform and listing id.</summary>
    public int Shard { get; set; }
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    public int ImageIndex { get; set; }
    public string Host { get; set; } = "";
    public DateTime ArchivedAt { get; set; }

    public Platform Platform => (Platform)PlatformValue;
}

/// <summary>Counts of one day of the archive (see <see cref="ITrainingPhotoStore.GetDaySummaryAsync"/>).</summary>
public sealed record TrainingPhotoDaySummary(long Total, IReadOnlyDictionary<string, long> ByHost, long Since);

public static class TrainingPhotoKeys
{
    public const int ShardCount = 8;

    /// <summary>The archive did not exist before; walks over days start here.</summary>
    public static readonly DateOnly EarliestDay = new(2026, 9, 1);

    public static string DayBucket(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd");

    public static string DayBucket(DateOnly day) => day.ToString("yyyy-MM-dd");

    /// <summary>Stable shard of a listing, so all photos of one listing share it.</summary>
    public static int ShardOf(int platform, string listingId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{platform}:{listingId}"))[0] % ShardCount;

    public static string PhotoKey(int platform, string listingId, int imageIndex) => $"{platform}:{listingId}:{imageIndex}";
}

/// <summary>Pure rules for when an archived photo may be used for training.</summary>
public static class TrainingPhotoRules
{
    /// <summary>An offer must be online this long before its photos are taken as real (not accidentally posted).</summary>
    public static readonly TimeSpan DefaultMinAge = TimeSpan.FromHours(24);

    /// <summary>True when <paramref name="firstSeenAt"/> is at least <paramref name="minAge"/> (default 24 hours) before <paramref name="now"/>.</summary>
    public static bool IsOldEnough(DateTime firstSeenAt, DateTime now, TimeSpan? minAge = null) =>
        now - firstSeenAt >= (minAge ?? DefaultMinAge);

    /// <summary>
    /// Archived, verified online at a time at least 24 hours after the offer was first seen (so it survived a day),
    /// and the host allows training right now (<paramref name="rightsStatusNow"/>, not the status at archive time).
    /// </summary>
    public static bool IsExportable(TrainingPhoto photo, ImageRightsStatus rightsStatusNow)
    {
        if (rightsStatusNow != ImageRightsStatus.Allowed)
            return false;
        if (photo.ArchivedAt == null || photo.FirstSeenAt == null || photo.VerifiedOnlineAt == null)
            return false;
        return IsOldEnough(photo.FirstSeenAt.Value, photo.VerifiedOnlineAt.Value);
    }
}
