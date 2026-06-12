using System.Net;
using System.Net.Http;
using FluentAssertions;
using ShipExtract.Infrastructure.Update;

namespace ShipExtract.Infrastructure.Tests.Update;

/// <summary>Tests for <see cref="GitHubUpdateService"/>.</summary>
public sealed class GitHubUpdateServiceTests
{
    private static GitHubUpdateService CreateService(HttpResponseMessage response)
    {
        var handler = new FakeHttpMessageHandler(response);
        var client  = new HttpClient(handler);
        return new GitHubUpdateService(client, "owner", "repo");
    }

    [Fact]
    public async Task CheckForUpdate_NewerVersion_ReturnsUpdateAvailable()
    {
        // Tag "v99.0.0" is definitely newer than the test runner's assembly version
        var json = """
                   {"tag_name":"v99.0.0","html_url":"https://example.com","published_at":"2026-01-01T00:00:00Z","assets":[{"browser_download_url":"https://example.com/download"}]}
                   """;

        var sut    = CreateService(OkJson(json));
        var result = await sut.CheckForUpdateAsync();

        result.Should().NotBeNull();
        result!.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version(99, 0, 0));
        result.DownloadUrl.Should().Be("https://example.com/download");
    }

    [Fact]
    public async Task CheckForUpdate_SameVersion_IsUpdateAvailableFalse()
    {
        // Use the actual running version (always "equal")
        var asm     = System.Reflection.Assembly.GetExecutingAssembly();
        var v       = asm.GetName().Version ?? new Version(1, 0, 0);
        var tagName = $"v{v.Major}.{v.Minor}.{v.Build}";

        var json = $$"""
                     {"tag_name":"{{tagName}}","html_url":"https://example.com","published_at":"2026-01-01T00:00:00Z","assets":[]}
                     """;

        var sut    = CreateService(OkJson(json));
        var result = await sut.CheckForUpdateAsync();

        result.Should().NotBeNull();
        result!.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_OlderVersion_IsUpdateAvailableFalse()
    {
        var json = """
                   {"tag_name":"v0.0.1","html_url":"https://example.com","published_at":"2024-01-01T00:00:00Z","assets":[]}
                   """;

        var sut    = CreateService(OkJson(json));
        var result = await sut.CheckForUpdateAsync();

        result.Should().NotBeNull();
        result!.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_NetworkFailure_ReturnsNull()
    {
        var handler = new ThrowingHttpMessageHandler();
        var client  = new HttpClient(handler);
        var sut     = new GitHubUpdateService(client, "owner", "repo");

        var result = await sut.CheckForUpdateAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_InvalidJson_ReturnsNull()
    {
        var sut    = CreateService(OkJson("not-json-at-all!!!"));
        var result = await sut.CheckForUpdateAsync();

        result.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}

// ── Fake handlers ─────────────────────────────────────────────────────────────

file sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(response);
}

file sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Simulated network failure");
}
