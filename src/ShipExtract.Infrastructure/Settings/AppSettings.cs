using System.Text.Json.Serialization;
using ShipExtract.Infrastructure.AI;

namespace ShipExtract.Infrastructure.Settings;

/// <summary>Application-wide settings persisted to disk (excluding the API key, which is stored in Windows Credential Manager).</summary>
public sealed class AppSettings
{
    /// <summary>Anthropic API key placeholder — never written to or read from disk; lives in Windows Credential Manager.</summary>
    [JsonIgnore]
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

    /// <summary>Directory where batch history files are stored.</summary>
    public string HistoryDirectory { get; set; } = string.Empty;

    /// <summary>Minimum confidence score required for a result to appear in the main export sheet.
    /// Results below this threshold are exported to a separate review file/sheet.</summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.60;

    /// <summary>Tesseract language codes to use for OCR. The "eng" language is always included.</summary>
    public List<string> OcrLanguages { get; set; } = new() { "eng" };

    /// <summary>Whether the first-use onboarding tour has been completed.</summary>
    public bool OnboardingCompleted { get; set; } = false;

    /// <summary>Prefix applied to generated export filenames (e.g. "ShipExtract_20250611_…").</summary>
    public string ExportFilePrefix { get; set; } = "ShipExtract";

    /// <summary>Saved window left position; -1 means centre on load.</summary>
    public double WindowLeft { get; set; } = -1;

    /// <summary>Saved window top position; -1 means centre on load.</summary>
    public double WindowTop { get; set; } = -1;

    /// <summary>Saved window width.</summary>
    public double WindowWidth { get; set; } = 1100;

    /// <summary>Saved window height.</summary>
    public double WindowHeight { get; set; } = 680;

    /// <summary>Whether the window was maximised when last closed.</summary>
    public bool WindowMaximized { get; set; } = false;

    /// <summary>User-defined custom extraction fields appended to every AI prompt.</summary>
    public List<Domain.Models.CustomField> CustomFields { get; set; } = new();

    // ── Auto-Update ────────────────────────────────────────────────────────

    /// <summary>Whether to automatically check for updates on startup.</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>Date of the last successful update check (UTC). Null means never checked.</summary>
    public DateTime? LastUpdateCheckDate { get; set; }

    /// <summary>Version tag that the user has chosen to skip (e.g. "1.4.0"). Null means no version is skipped.</summary>
    public string? SkippedVersion { get; set; }
}
