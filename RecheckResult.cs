using MessagePack;

namespace Coflnet.Ane;

/// <summary>Verdict of one availability recheck, see <see cref="RecheckResult"/>.</summary>
public enum RecheckStatus
{
    Available = 0,
    Gone = 1,
    Unknown = 2
}

/// <summary>
/// Outcome of every recheck the scraper ran for a <see cref="RecrawlRequest"/> (topic
/// <c>TOPICS:RECHECK_RESULT</c>, default <c>ane-notifier-recheck-result</c>). The older <see cref="MissingListing"/>
/// message is still produced for <see cref="RecheckStatus.Gone"/>; this one also reports still-online and unknown results.
/// Platform and status travel as plain ints so the wire format never depends on an enum type.
/// </summary>
[MessagePackObject]
public class RecheckResult
{
    [Key(0)]
    public string ListingId { get; set; } = string.Empty;
    [Key(1)]
    public int PlatformValue { get; set; }
    [Key(2)]
    public int StatusValue { get; set; }
    /// <summary>Bounded reason when the status is Unknown (blocked, timeout, paused, ...).</summary>
    [Key(3)]
    public string? UnknownReason { get; set; }
    [Key(4)]
    public DateTime CheckedAt { get; set; }

    /// <summary>Explicit observed sale signal, never inferred from disappearance.</summary>
    [Key(5)]
    public string? SaleEvidence { get; set; }

    [Key(6)]
    public string? AgeBasis { get; set; }

    [IgnoreMember]
    public Platform Platform
    {
        get => (Platform)PlatformValue;
        set => PlatformValue = (int)value;
    }

    [IgnoreMember]
    public RecheckStatus Status
    {
        get => (RecheckStatus)StatusValue;
        set => StatusValue = (int)value;
    }
}
