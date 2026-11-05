using System.Text.RegularExpressions;

namespace Coflnet.Ane.ImageRights;

public enum ImageRightsPurpose { Training, Comparison }

/// <summary>Manual overrides, applied before any stored status. Keys <c>IMAGE_RIGHTS:DENY_PLATFORMS</c> and <c>IMAGE_RIGHTS:DENY_HOSTS</c>.</summary>
public sealed class ImageRightsOptions
{
    public const string DefaultDenyPlatforms = "";

    public HashSet<Platform> DenyPlatforms { get; init; } = [];
    public HashSet<string> DenyHosts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the comma lists. A null platform list means the default (none), an empty one means none.</summary>
    public static ImageRightsOptions Parse(string? denyPlatforms, string? denyHosts)
    {
        var platforms = new HashSet<Platform>();
        foreach (var name in Split(denyPlatforms ?? DefaultDenyPlatforms))
            if (Enum.TryParse<Platform>(name, true, out var p))
                platforms.Add(p);
        return new ImageRightsOptions
        {
            DenyPlatforms = platforms,
            DenyHosts = new HashSet<string>(Split(denyHosts).Select(h => h.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase)
        };
    }

    public static ImageRightsOptions FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        Parse(configuration["IMAGE_RIGHTS:DENY_PLATFORMS"], configuration["IMAGE_RIGHTS:DENY_HOSTS"]);

    private static IEnumerable<string> Split(string? list) =>
        (list ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>What readers may rely on for one image host.</summary>
public sealed record ImageRightsDecision(ImageRightsStatus Status, IReadOnlyList<string> Evidence, DateTime? CheckedAt);

/// <summary>Pure rules that turn a stored status row (or its absence) into the status readers must use.</summary>
public static class ImageRightsPolicy
{
    /// <summary>Age bound for non-revoked diagnostic statuses; recorded reservations never expire by age.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(48);

    /// <summary>Legacy age projection: reservations stay revoked, other stale statuses become Unknown.</summary>
    public static ImageRightsStatus EffectiveStatus(ImageRightsStatus stored, DateTime checkedAtUtc, DateTime nowUtc) =>
        stored != ImageRightsStatus.Revoked && nowUtc - checkedAtUtc > MaxAge ? ImageRightsStatus.Unknown : stored;

    /// <summary>Configuration denial for a host, or null. Evidence starts with "denied by configuration".</summary>
    public static string? DenialEvidence(string host, ImageRightsOptions options)
    {
        var mapping = ImageHostMapping.TryGet(host);
        if (options.DenyHosts.Contains(host))
            return $"denied by configuration: host {host} is in IMAGE_RIGHTS:DENY_HOSTS";
        if (mapping != null && options.DenyPlatforms.Contains(mapping.Platform))
            return $"denied by configuration: platform {mapping.Platform} is in IMAGE_RIGHTS:DENY_PLATFORMS";
        return null;
    }

    /// <summary>Explicit applicable reservations win; absent metadata is not an opt-out.</summary>
    public static ImageRightsDecision Resolve(string? host, ImageRightsRecord? stored, DateTime nowUtc, ImageRightsOptions options,
        ImageRightsPurpose purpose = ImageRightsPurpose.Training)
    {
        if (string.IsNullOrEmpty(host))
            return stored?.StatusValue == ImageRightsStatus.Revoked
                ? new(ImageRightsStatus.Revoked, stored.Evidence ?? [], stored.CheckedAt)
                : new(ImageRightsStatus.Unknown, ["no image host"], null);
        var denial = DenialEvidence(host, options);
        if (denial != null)
            return new(ImageRightsStatus.Revoked, [denial], stored?.CheckedAt);
        if (stored?.StatusValue == ImageRightsStatus.Revoked && !IsObsoleteConfigurationDenial(stored, options)
            && (stored.Evidence is not { Count: > 0 } || stored.Evidence.Any(e => AppliesTo(e, purpose))))
            return new(ImageRightsStatus.Revoked, stored.Evidence ?? [], stored.CheckedAt);
        return new(ImageRightsStatus.Allowed, stored?.Evidence ?? [$"no recorded {purpose} opt-out for {host}"], stored?.CheckedAt);
    }

    /// <summary>Only exact machine configuration evidence whose current override was removed is obsolete.</summary>
    public static bool IsObsoleteConfigurationDenial(ImageRightsRecord row, ImageRightsOptions options) =>
        row.StatusValue == ImageRightsStatus.Revoked && row.Evidence is { Count: > 0 } && DenialEvidence(row.Host, options) == null
        && row.Evidence.All(e => e == $"denied by configuration: host {row.Host} is in IMAGE_RIGHTS:DENY_HOSTS"
            || ImageHostMapping.TryGet(row.Host) is { } mapping
                && e == $"denied by configuration: platform {mapping.Platform} is in IMAGE_RIGHTS:DENY_PLATFORMS");

    // Legacy rows have no typed origin. Only exact, understood evidence may narrow their restriction.
    private static bool AppliesTo(string? evidence, ImageRightsPurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return true;
        const string robotsHeader = "image response header X-Robots-Tag: ";
        if (evidence.StartsWith(robotsHeader, StringComparison.OrdinalIgnoreCase))
        {
            var value = evidence[robotsHeader.Length..];
            // Old DTOs comma-joined physical fields; an unprefixed reserved token may have been global.
            if (Regex.IsMatch(value, @"(?:^|,)\s*(?:noai|noimageai)(?=$|[,\s])", RegexOptions.IgnoreCase)) return true;
            // The old writer only emitted this cause for these substrings; other legacy values remain ambiguous.
            return !(value.Contains("noai", StringComparison.OrdinalIgnoreCase) || value.Contains("noimageai", StringComparison.OrdinalIgnoreCase))
                || ImageRightsEvaluator.HeaderReserves("X-Robots-Tag", value, purpose);
        }
        const string signalHeader = "image response header Content-Signal: ";
        if (evidence.StartsWith(signalHeader, StringComparison.OrdinalIgnoreCase))
        {
            var value = evidence[signalHeader.Length..];
            return !(ImageRightsEvaluator.HeaderReserves("Content-Signal", value, ImageRightsPurpose.Training)
                || ImageRightsEvaluator.HeaderReserves("Content-Signal", value, ImageRightsPurpose.Comparison))
                || ImageRightsEvaluator.HeaderReserves("Content-Signal", value, purpose);
        }
        var bot = Regex.Match(evidence, """^robots\.txt of [^ :]+: AI crawler group \((?<agents>[^()]+)\) has Disallow: / - "Disallow: /\*?"$""", RegexOptions.IgnoreCase);
        if (bot.Success)
        {
            var agents = bot.Groups["agents"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return agents.Length == 0 || agents.Any(a => a == "*" || ImageRightsEvaluator.UserAgent.Contains(a, StringComparison.OrdinalIgnoreCase));
        }
        var signal = Regex.Match(evidence, """^robots\.txt of [^ :]+: Content-Signal (?:ai-train=no|ai-input=no|use=immediate) - "Content-Signal:(?<signals>[^"]+)"$""", RegexOptions.IgnoreCase);
        if (signal.Success)
        {
            var tokens = Regex.Replace(signal.Groups["signals"].Value.ToLowerInvariant(), @"\s*=\s*", "=").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var other = purpose == ImageRightsPurpose.Comparison ? "ai-train=no" : "ai-input=no";
            var positive = purpose == ImageRightsPurpose.Comparison ? "ai-input=yes" : "ai-train=yes";
            return !tokens.Contains(other) || tokens.Any(t => t != other && t != positive && t != "search=yes" && !(purpose == ImageRightsPurpose.Comparison && t == "use=reference"));
        }
        var usage = Regex.Match(evidence, """^robots\.txt of [^ :]+: Content-Usage train-ai=n - "Content-Usage:\s*train-ai\s*=\s*n\s*"$""", RegexOptions.IgnoreCase);
        return !usage.Success || purpose == ImageRightsPurpose.Training;
    }
}
