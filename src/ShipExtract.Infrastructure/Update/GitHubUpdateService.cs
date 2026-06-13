using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.Update;

/// <summary>
/// Checks the GitHub Releases API for a newer version of the application.
/// Never throws — all errors are swallowed and null is returned.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    private readonly HttpClient _http;
    private readonly string _repoOwner;
    private readonly string _repoName;
    private readonly ILoggingService? _logger;

    /// <summary>Initialises a new instance of <see cref="GitHubUpdateService"/>.</summary>
    /// <param name="httpClient">Named HttpClient with User-Agent and timeout pre-configured.</param>
    /// <param name="repoOwner">GitHub repository owner (e.g. "davidkuts").</param>
    /// <param name="repoName">GitHub repository name (e.g. "ship-extract").</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public GitHubUpdateService(HttpClient httpClient, string repoOwner, string repoName,
        ILoggingService? logger = null)
    {
        _http      = httpClient;
        _repoOwner = repoOwner;
        _repoName  = repoName;
        _logger    = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
            var release = await _http.GetFromJsonAsync<GitHubRelease>(url, ct).ConfigureAwait(false);
            if (release is null) return null;

            // Strip leading 'v' from tag (e.g. "v1.3.0" → "1.3.0")
            var tagName = release.TagName?.TrimStart('v') ?? string.Empty;
            if (!Version.TryParse(tagName, out var latestVersion)) return null;

            var currentVersion = GetCurrentVersion();
            var isNewer        = latestVersion > currentVersion;

            _logger?.LogDebug(
                "Update check: current={Current}, latest={Latest}, available={Available}",
                currentVersion, latestVersion, isNewer);

            var releasePageUrl = release.HtmlUrl ?? string.Empty;

            return new UpdateInfo(
                CurrentVersion:    currentVersion,
                LatestVersion:     latestVersion,
                IsUpdateAvailable: isNewer,
                ReleaseNotesUrl:   releasePageUrl,
                DownloadUrl:       releasePageUrl,
                PublishedAt:       release.PublishedAt);
        }
        catch
        {
            return null;
        }
    }

    private static Version GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        return ver is null ? new Version(1, 0, 0) : new Version(ver.Major, ver.Minor, ver.Build);
    }
}

// ── Internal GitHub API DTOs ──────────────────────────────────────────────────

file sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]    public string?        TagName     { get; set; }
    [JsonPropertyName("html_url")]    public string?        HtmlUrl     { get; set; }
    [JsonPropertyName("published_at")]public DateTimeOffset PublishedAt { get; set; }
}
