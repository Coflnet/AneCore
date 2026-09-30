namespace Coflnet.Ane;

/// <summary>
/// One non-unchanged decision of a regroup run (see AneNotifier's <c>RegroupRunService</c>), persisted so a
/// dry run can be reviewed after the fact instead of only being logged. Partition = run id, clustering =
/// (listing platform, listing id, from slug). Rows expire after <see cref="TtlSeconds"/>.
/// The platform is stored as a plain int (the Cassandra driver cannot map enum/byte CLR types).
/// </summary>
public class RegroupMove
{
    public const int TtlSeconds = 30 * 24 * 3600;

    public string RunId { get; set; } = "";
    public int ListingPlatform { get; set; }
    public string ListingId { get; set; } = "";
    public string FromSlug { get; set; } = "";
    public string? Title { get; set; }
    public double Price { get; set; }
    /// <summary>Target product slug; null when the listing is detached.</summary>
    public string? ToSlug { get; set; }
    /// <summary>"moved" | "detached".</summary>
    public string Outcome { get; set; } = "";
    public string? DetachReason { get; set; }
    public string? TargetName { get; set; }
    /// <summary>True when the run was in apply mode and this change was actually written.</summary>
    public bool Applied { get; set; }
    public DateTime CreatedAt { get; set; }
}
