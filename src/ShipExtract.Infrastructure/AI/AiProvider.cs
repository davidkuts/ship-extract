namespace ShipExtract.Infrastructure.AI;

/// <summary>Identifies the AI provider used for shipment data extraction.</summary>
public enum AiProvider
{
    /// <summary>Ollama local inference server (free, private, offline-capable).</summary>
    Ollama,

    /// <summary>Anthropic Claude API (cloud-based, highest accuracy).</summary>
    Anthropic
}
