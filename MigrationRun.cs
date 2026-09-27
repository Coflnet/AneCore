namespace Coflnet.Ane;

/// <summary>
/// Tracks a one-time background migration/regroup run (see AneNotifier's <c>RegroupRunService</c>) so
/// it is never started twice for the same (run_id, mode) pair, and so a pod restart resumes from the
/// last saved checkpoint instead of scanning the products table from the start again.
/// </summary>
public class MigrationRun
{
    /// <summary>Operator-chosen id for the run, e.g. "2026-09-28-a" (config <c>REGROUP:RUN_ID</c>).</summary>
    public string RunId { get; set; } = "";

    /// <summary>"dryrun" or "apply" - the same run id can be dry-run and later applied independently.</summary>
    public string Mode { get; set; } = "";

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>"running" | "completed" | "failed". Only "completed" blocks a re-run of this id+mode.</summary>
    public string Status { get; set; } = "running";

    /// <summary>Cumulative counters (products_scanned, listings_scanned, moved, detached, unchanged, ...).</summary>
    public Dictionary<string, long> Counters { get; set; } = new();

    /// <summary>Base64 of the Cassandra paging state for the products table scan; null before the first page.</summary>
    public string? Checkpoint { get; set; }
}
