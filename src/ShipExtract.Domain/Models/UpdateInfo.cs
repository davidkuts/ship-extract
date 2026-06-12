namespace ShipExtract.Domain.Models;

/// <summary>Contains the result of a GitHub Releases update check.</summary>
/// <param name="CurrentVersion">The version of the running application.</param>
/// <param name="LatestVersion">The latest version reported by GitHub Releases.</param>
/// <param name="IsUpdateAvailable">True when <paramref name="LatestVersion"/> is strictly newer than <paramref name="CurrentVersion"/>.</param>
/// <param name="ReleaseNotesUrl">HTML URL of the latest GitHub release page.</param>
/// <param name="DownloadUrl">Direct download URL for the latest release asset (or the release page URL as fallback).</param>
/// <param name="PublishedAt">When the latest release was published.</param>
public record UpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseNotesUrl,
    string DownloadUrl,
    DateTimeOffset PublishedAt
);
