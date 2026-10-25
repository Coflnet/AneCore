namespace Coflnet.Ane.VisualPairs;

/// <summary>Reference to one photo of a listing.</summary>
public sealed record VisualPairImage(Platform Platform, string ListingId, int ImageIndex, string ImageUrl);

/// <summary>Frozen capture facts. Text identity hints are unverified grouping output, never product ground truth.
/// Null model revision/seller hashes mean unavailable evidence; do not infer them from model names or listing ids.</summary>
public sealed record VisualPairCaptureMetadata
{
    public int SchemaVersion { get; init; } = 1;
    public string Domain { get; init; } = "clothing";
    public string PolicyVersion { get; init; } = "clothing-visual-v1";
    public DateTime CapturedAt { get; init; }
    public string Decision { get; init; } = "";
    public string Source { get; init; } = "";
    public double? SecondBestSimilarity { get; init; }
    public string Mode { get; init; } = "";
    public string ModelId { get; init; } = "";
    public string? ModelRevision { get; init; }
    public int EmbeddingDimensions { get; init; }
    public double JoinThreshold { get; init; }
    public double ColorConflictExtra { get; init; }
    public string ColourVeto { get; init; } = "";
    public string PatternVeto { get; init; } = "";
    public int MinClusterSize { get; init; }
    public int CandidateK { get; init; }
    public int MaxPhotosPerListing { get; init; }
    public int MaxReferencesPerProduct { get; init; }
    public double ReferenceDiversityMaxSimilarity { get; init; }
    public bool ImageSignals { get; init; }
    public double PatternMinProbability { get; init; }
    public double ImageColourMinProbability { get; init; }
    public double NearMissMinSimilarity { get; init; }
    public string SellerHashNamespace { get; init; } = "coflnet:ane:seller-blacklist:v1";
    public string? ASellerHash { get; init; }
    public string? BSellerHash { get; init; }
    public string? ATextIdentityHint { get; init; }
    public string? BTextIdentityHint { get; init; }
    public string IdentityHintSource { get; init; } = "text_grouping_unverified";
}

/// <summary>
/// One photo pair the visual grouping looked at. Row of <c>visual_pair_candidates</c>, partition
/// <c>(day, shard)</c>, clustering <c>pair_id</c>, TTL 90 days. Platforms are stored as int.
/// </summary>
public class VisualPairCandidate
{
    /// <summary>Partition key part 1: UTC day <c>yyyy-MM-dd</c> of <see cref="CreatedAt"/>.</summary>
    public string Day { get; set; } = "";
    /// <summary>Partition key part 2: 0..7, derived from the pair id.</summary>
    public int Shard { get; set; }
    public string PairId { get; set; } = "";
    public int APlatformValue { get; set; }
    public string AListingId { get; set; } = "";
    public int AImageIndex { get; set; }
    public string AImageUrl { get; set; } = "";
    public int BPlatformValue { get; set; }
    public string BListingId { get; set; } = "";
    public int BImageIndex { get; set; }
    public string BImageUrl { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public double Similarity { get; set; }
    public double? SecondBest { get; set; }
    /// <summary>join, seeded or near_miss.</summary>
    public string Decision { get; set; } = "";
    public string? ClusterSeoId { get; set; }
    public string? TextSeoId { get; set; }
    public string ModelId { get; set; } = "";
    /// <summary>Frozen provenance JSON; null on rows captured before provenance collection.</summary>
    public string? CaptureMetadataJson { get; set; }
    public string Source { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public VisualPairImage A
    {
        get => new((Platform)APlatformValue, AListingId, AImageIndex, AImageUrl);
        set { APlatformValue = (int)value.Platform; AListingId = value.ListingId; AImageIndex = value.ImageIndex; AImageUrl = value.ImageUrl; }
    }

    public VisualPairImage B
    {
        get => new((Platform)BPlatformValue, BListingId, BImageIndex, BImageUrl);
        set { BPlatformValue = (int)value.Platform; BListingId = value.ListingId; BImageIndex = value.ImageIndex; BImageUrl = value.ImageUrl; }
    }
}

/// <summary>Row of <c>visual_pair_candidate_days</c>: finds the partition of a candidate by pair id (also TTL 90 days).</summary>
public class VisualPairCandidateLocation
{
    public string PairId { get; set; } = "";
    public string Day { get; set; } = "";
}

/// <summary>Row of <c>visual_pairs_by_listing</c>, partition <c>(platform, listing_id)</c>, clustering <c>pair_id</c>: which pairs reference an offer (either side).</summary>
public class VisualPairByListing
{
    public int PlatformValue { get; set; }
    public string ListingId { get; set; } = "";
    public string PairId { get; set; } = "";
}

/// <summary>
/// One person's judgement of a pair. Row of <c>visual_pair_labels</c>, partition <c>pair_id</c>, clustering
/// <c>user_id</c> (so one label per user and pair, writing again overwrites). No TTL; carries a copy of both
/// image references so it stays usable after the candidate expired.
/// </summary>
public class VisualPairLabel
{
    public string PairId { get; set; } = "";
    public string UserId { get; set; } = "";
    /// <summary>blind or diagnostic, supplied by the review client; null means unknown.</summary>
    public string? ReviewMode { get; set; }
    /// <summary>same_item, same_model_other_variant, different or unsure.</summary>
    public string Label { get; set; } = "";
    public string ModelId { get; set; } = "";
    /// <summary>Frozen provenance JSON; null on rows captured before provenance collection.</summary>
    public string? CaptureMetadataJson { get; set; }
    /// <summary>Similarity at the time of labeling.</summary>
    public double Similarity { get; set; }
    public string GroupKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int APlatformValue { get; set; }
    public string AListingId { get; set; } = "";
    public int AImageIndex { get; set; }
    public string AImageUrl { get; set; } = "";
    public int BPlatformValue { get; set; }
    public string BListingId { get; set; } = "";
    public int BImageIndex { get; set; }
    public string BImageUrl { get; set; } = "";

    public VisualPairImage A
    {
        get => new((Platform)APlatformValue, AListingId, AImageIndex, AImageUrl);
        set { APlatformValue = (int)value.Platform; AListingId = value.ListingId; AImageIndex = value.ImageIndex; AImageUrl = value.ImageUrl; }
    }

    public VisualPairImage B
    {
        get => new((Platform)BPlatformValue, BListingId, BImageIndex, BImageUrl);
        set { BPlatformValue = (int)value.Platform; BListingId = value.ListingId; BImageIndex = value.ImageIndex; BImageUrl = value.ImageUrl; }
    }

    /// <summary>A later submission cannot erase diagnostic or unknown exposure already recorded for this reviewer.</summary>
    internal static string? PreserveReviewMode(string? previous, string? requested) =>
        previous == "diagnostic" || requested == "diagnostic" ? "diagnostic" : previous == null ? null : requested;

    public static VisualPairLabel From(VisualPairCandidate candidate, string userId, string label, DateTime now) => new()
    {
        PairId = candidate.PairId, UserId = userId, Label = label, ModelId = candidate.ModelId,
        Similarity = candidate.Similarity, GroupKey = candidate.GroupKey, CreatedAt = now,
        A = candidate.A, B = candidate.B, CaptureMetadataJson = candidate.CaptureMetadataJson
    };
}
