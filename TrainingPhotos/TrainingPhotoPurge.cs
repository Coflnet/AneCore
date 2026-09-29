using Coflnet.Ane.ImageRights;

namespace Coflnet.Ane.TrainingPhotos;

/// <summary>Pure decision which hosts' archived photos are deleted.</summary>
public static class TrainingPhotoPurge
{
    public const int DefaultRevokedAfterDays = 7;

    /// <summary>
    /// Hosts whose stored status is <see cref="ImageRightsStatus.Revoked"/> since more than <paramref name="revokedFor"/>
    /// (<see cref="ImageRightsRecord.StatusSince"/>, or the check time for rows without it). <see cref="ImageRightsStatus.Unknown"/>
    /// never purges (it only excludes from export), and a fresh revocation is given the grace period.
    /// </summary>
    public static IReadOnlyList<string> HostsToPurge(IEnumerable<ImageRightsRecord> rows, DateTime now, TimeSpan revokedFor) =>
        rows.Where(r => r.StatusValue == ImageRightsStatus.Revoked && r.Status == nameof(ImageRightsStatus.Revoked)
                        && now - (r.StatusSince ?? r.CheckedAt) > revokedFor)
            .Select(r => r.Host.ToLowerInvariant()).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
}
