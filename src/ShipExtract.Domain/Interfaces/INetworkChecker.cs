namespace ShipExtract.Domain.Interfaces;

/// <summary>Checks whether external AI API endpoints are reachable.</summary>
public interface INetworkChecker
{
    /// <summary>
    /// Returns <see langword="true"/> if the Anthropic API host is reachable within a 5-second timeout.
    /// </summary>
    Task<bool> IsAnthropicReachableAsync(CancellationToken ct = default);
}
