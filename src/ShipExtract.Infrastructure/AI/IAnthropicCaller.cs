namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Internal abstraction over the Anthropic API message call, enabling unit testing
/// without making real HTTP requests.
/// </summary>
public interface IAnthropicCaller
{
    /// <summary>Sends a message to the Anthropic API and returns the raw text response.</summary>
    Task<string> SendAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        int maxTokens,
        CancellationToken ct);
}
