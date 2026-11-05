using System.Text.Json;

namespace Coflnet.Ane.ImageRights;

/// <summary>Exact fetched policy and crawler identity; response-header or manual denials are not policy context.</summary>
public sealed record ImageRightsPolicySnapshot(string ImageHost, string SiteHost, FetchedFile ImageRobots,
    FetchedFile SiteRobots, FetchedFile ImageTdm, FetchedFile SiteTdm, string UserAgent)
{
    public ImageRightsResult Evaluate(string imagePath) => ImageRightsEvaluator.Evaluate(new(
        ImageHost, SiteHost, ImageRobots, SiteRobots, ImageTdm.Text, SiteTdm.Text, ImagePath: imagePath, UserAgent: UserAgent));

    public string? RootCrawlerCause()
    {
        var cause = $"robots.txt of {ImageHost}: crawler {UserAgent} is disallowed for /";
        return Evaluate("/").Evidence.Contains(cause) ? cause : null;
    }
}

/// <summary>Only durably bound crawler causes may narrow comparison; legacy matching text is not origin proof.</summary>
public sealed record ImageRightsContext(int Version, List<ImageRightsPolicySnapshot> Snapshots, List<string>? ProvenRootCauses = null)
{
    public static ImageRightsContext? Read(string? json, string host)
    {
        if (json == null) return null;
        try
        {
            var value = JsonSerializer.Deserialize<ImageRightsContext>(json);
            return value is { Version: 1, Snapshots.Count: > 0 }
                && value.Snapshots.All(s => s != null && s.ImageHost == host && !string.IsNullOrWhiteSpace(s.SiteHost)
                    && !string.IsNullOrWhiteSpace(s.UserAgent) && Valid(s.ImageRobots) && Valid(s.SiteRobots)
                    && Valid(s.ImageTdm) && Valid(s.SiteTdm))
                && (value.ProvenRootCauses == null || value.ProvenRootCauses.All(e => e != null
                    && value.Snapshots.Any(s => s.RootCrawlerCause() == e))) ? value : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool Valid(FetchedFile? file) => file != null && Enum.IsDefined(file.State)
        && (file.State != FetchState.Ok || file.Text != null);

    public static string? Merge(string host, string? previous, string? current, IReadOnlyList<string>? previousRevokedEvidence = null)
    {
        if (current == null) return previous;
        var next = Read(current, host) ?? throw new InvalidOperationException("Invalid new image policy context");
        var old = previous == null ? null : (Read(previous, host)
            ?? throw new InvalidOperationException("Cannot replace an unrecognized image policy context"));
        var oldProof = old?.ProvenRootCauses ?? [];
        var unproven = (previousRevokedEvidence ?? []).Except(oldProof, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var proof = oldProof.Concat(next.ProvenRootCauses ?? []).Where(e => !unproven.Contains(e)).Distinct().ToList();
        var unique = (old?.Snapshots ?? []).Concat(next.Snapshots).DistinctBy(s => JsonSerializer.Serialize(s)).ToList();
        return JsonSerializer.Serialize(new ImageRightsContext(1, unique, proof));
    }
}
