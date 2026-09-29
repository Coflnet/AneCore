namespace Coflnet.Ane.ImageRights;

/// <summary>Manual overrides, applied before any stored status. Keys <c>IMAGE_RIGHTS:DENY_PLATFORMS</c> and <c>IMAGE_RIGHTS:DENY_HOSTS</c>.</summary>
public sealed class ImageRightsOptions
{
    public const string DefaultDenyPlatforms = "Willhaben";

    public HashSet<Platform> DenyPlatforms { get; init; } = [Platform.Willhaben];
    public HashSet<string> DenyHosts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the comma lists. A null platform list means the default (Willhaben), an empty one means none.</summary>
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
    /// <summary>A stored status older than this is treated as Unknown.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(48);

    /// <summary>Stored status if fresh, otherwise <see cref="ImageRightsStatus.Unknown"/>.</summary>
    public static ImageRightsStatus EffectiveStatus(ImageRightsStatus stored, DateTime checkedAtUtc, DateTime nowUtc) =>
        nowUtc - checkedAtUtc > MaxAge ? ImageRightsStatus.Unknown : stored;

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

    /// <summary>Decision for <paramref name="host"/>: override first, then the mapping, then staleness of the stored row.</summary>
    public static ImageRightsDecision Resolve(string? host, ImageRightsRecord? stored, DateTime nowUtc, ImageRightsOptions options)
    {
        if (string.IsNullOrEmpty(host))
            return new(ImageRightsStatus.Unknown, ["no image host"], null);
        var denial = DenialEvidence(host, options);
        if (denial != null)
            return new(ImageRightsStatus.Revoked, [denial], stored?.CheckedAt);
        if (ImageHostMapping.TryGet(host) == null)
            return new(ImageRightsStatus.Unknown, [$"host {host} is not in the image host mapping"], null);
        if (stored == null)
            return new(ImageRightsStatus.Unknown, [$"no rights status stored for {host}"], null);
        var effective = EffectiveStatus(stored.StatusValue, stored.CheckedAt, nowUtc);
        if (effective != stored.StatusValue)
            return new(effective, [$"status of {host} is older than {MaxAge.TotalHours:0} hours", .. stored.Evidence], stored.CheckedAt);
        return new(effective, stored.Evidence, stored.CheckedAt);
    }
}
