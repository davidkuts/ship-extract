using System.Net;
using System.Net.Http;
using Shouldly;
using ShipExtract.Infrastructure.Network;

namespace ShipExtract.Infrastructure.Tests.Network;

/// <summary>Tests for <see cref="NetworkChecker"/>.</summary>
public sealed class NetworkCheckerTests
{
    [Fact]
    public async Task IsAnthropicReachable_WhenServerResponds_ReturnsTrue()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK);
        var client  = new HttpClient(handler);
        var checker = new NetworkChecker(client);

        var result = await checker.IsAnthropicReachableAsync();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAnthropicReachable_WhenConnectionFails_ReturnsFalse()
    {
        var handler = new ThrowingHttpHandler();
        var client  = new HttpClient(handler);
        var checker = new NetworkChecker(client);

        var result = await checker.IsAnthropicReachableAsync();

        result.ShouldBeFalse();
    }
}

// ── Test helpers ──────────────────────────────────────────────────────────────

file sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    public StubHttpHandler(HttpStatusCode status) => _status = status;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(new HttpResponseMessage(_status));
}

file sealed class ThrowingHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct) =>
        throw new HttpRequestException("Simulated connection failure");
}
