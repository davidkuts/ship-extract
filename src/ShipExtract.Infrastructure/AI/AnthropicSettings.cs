namespace ShipExtract.Infrastructure.AI;

/// <summary>Configuration settings for the Anthropic Claude API client.</summary>
public sealed class AnthropicSettings
{
    /// <summary>Anthropic API key used to authenticate requests.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model identifier to use for extraction requests.</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>Maximum number of tokens to generate in the model response.</summary>
    public int MaxTokens { get; set; } = 2048;
}
