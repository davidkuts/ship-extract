using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.Network;

/// <summary>
/// Checks whether the Anthropic API host is reachable by making a lightweight HEAD/GET request
/// with a short timeout. Failures are swallowed and reported as <see langword="false"/>.
/// </summary>
public sealed class NetworkChecker : INetworkChecker
{
    private readonly HttpClient _http;

    /// <summary>Initialises a new instance of <see cref="NetworkChecker"/>.</summary>
    public NetworkChecker(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAnthropicReachableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com");
            await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                       .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
