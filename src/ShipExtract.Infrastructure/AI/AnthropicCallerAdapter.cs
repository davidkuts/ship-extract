using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Implements <see cref="IAnthropicCaller"/> using the real Anthropic SDK HTTP client.
/// </summary>
internal sealed class AnthropicCallerAdapter : IAnthropicCaller
{
    private readonly AnthropicClient _client;

    public AnthropicCallerAdapter(AnthropicClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<string> SendAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        int maxTokens,
        CancellationToken ct)
    {
        var parameters = new MessageParameters
        {
            Model      = model,
            MaxTokens  = maxTokens,
            System     = [new SystemMessage(systemPrompt, null)],
            Messages   =
            [
                new Message
                {
                    Role    = RoleType.User,
                    Content = [new TextContent { Text = userPrompt }]
                }
            ]
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct);

        return response.FirstMessage?.Text ?? string.Empty;
    }
}
