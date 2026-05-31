using MessagePack;

namespace Coflnet.Ane.Crowd;

/// <summary>
/// A unit of crowd-sourced scraping work handed to the browser extension.
/// The extension opens <see cref="Url"/> in the user's own browser session
/// (residential IP, real fingerprint), extracts the node(s) matching
/// <see cref="Xpath"/> and posts the result back to the API.
/// </summary>
[MessagePackObject]
public class CrowdScrapeTask
{
    /// <summary>Stable id used to correlate submissions with the task.</summary>
    [Key(0)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Page the extension should open and read.</summary>
    [Key(1)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// XPath the extension evaluates against the loaded document. Usually selects
    /// the container that holds the listing data (or a list of listing links).
    /// </summary>
    [Key(2)]
    public string Xpath { get; set; } = string.Empty;

    /// <summary>Marketplace the URL belongs to, used by the extractor.</summary>
    [Key(3)]
    public Platform Platform { get; set; }

    /// <summary>Points awarded once a valid submission for this task is processed.</summary>
    [Key(4)]
    public int Reward { get; set; } = 1;

    /// <summary>Free-form reason/category for diagnostics (e.g. "discover", "refresh").</summary>
    [Key(5)]
    public string? Kind { get; set; }
}

/// <summary>
/// Result the extension posts after it scraped a <see cref="CrowdScrapeTask"/>.
/// </summary>
[MessagePackObject]
public class CrowdScrapeSubmission
{
    [Key(0)]
    public string TaskId { get; set; } = string.Empty;

    [Key(1)]
    public string Url { get; set; } = string.Empty;

    [Key(2)]
    public Platform Platform { get; set; }

    /// <summary>Raw outer HTML of the node(s) matched by the task XPath.</summary>
    [Key(3)]
    public string Html { get; set; } = string.Empty;

    /// <summary>Plain text content of the matched node(s) (fallback / validation).</summary>
    [Key(4)]
    public string? Text { get; set; }

    /// <summary>When the extension captured the content (unix ms).</summary>
    [Key(5)]
    public long CapturedAtMs { get; set; }
}

/// <summary>
/// Envelope AneApi forwards to AneScrapper: the authenticated contributor plus the
/// raw submissions to extract. AneScrapper performs text extraction and credits points.
/// </summary>
[MessagePackObject]
public class CrowdScrapeForward
{
    [Key(0)]
    public string UserId { get; set; } = string.Empty;

    [Key(1)]
    public List<CrowdScrapeSubmission> Submissions { get; set; } = new();
}

/// <summary>
/// Response returned to the extension after a batch of submissions is accepted.
/// Points are credited asynchronously, so this reports what was queued.
/// </summary>
public class CrowdScrapeSubmitResult
{
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public long PointsQueued { get; set; }
}
