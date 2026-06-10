using ShipExtract.Infrastructure.AI;

namespace ShipExtract.Infrastructure.Settings;

/// <summary>Application-wide settings persisted to disk (excluding the API key, which is stored in Windows Credential Manager).</summary>
public sealed class AppSettings
{
    /// <summary>Anthropic API key placeholder — always empty in the persisted file.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Anthropic model identifier.</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>Maximum tokens for AI responses.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Path to the Tesseract tessdata directory.</summary>
    public string TessDataDirectory { get; set; } = string.Empty;

    /// <summary>Default directory for export output files.</summary>
    public string DefaultOutputDirectory { get; set; } = string.Empty;

    /// <summary>Directory where log files are written.</summary>
    public string LogDirectory { get; set; } = string.Empty;

    /// <summary>AI provider to use for extraction.</summary>
    public AiProvider AiProvider { get; set; } = AiProvider.Ollama;

    /// <summary>Base URL for the local Ollama server.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Ollama model name to use for extraction.</summary>
    public string OllamaModel { get; set; } = "mistral";
}
