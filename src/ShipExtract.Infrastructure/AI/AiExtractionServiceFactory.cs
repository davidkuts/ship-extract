using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Resolves the active <see cref="IAiExtractionService"/> implementation
/// based on the current <see cref="AppSettings.AiProvider"/> value.
/// Provider switching is instant — no restart required.
/// </summary>
public sealed class AiExtractionServiceFactory
{
    private readonly AnthropicExtractionService _anthropic;
    private readonly OllamaExtractionService    _ollama;
    private readonly AppSettings                _settings;

    /// <summary>Initialises a new instance of <see cref="AiExtractionServiceFactory"/>.</summary>
    public AiExtractionServiceFactory(
        AnthropicExtractionService anthropicService,
        OllamaExtractionService    ollamaService,
        AppSettings                settings)
    {
        _anthropic = anthropicService;
        _ollama    = ollamaService;
        _settings  = settings;
    }

    /// <summary>Returns the service for the currently configured provider.</summary>
    public IAiExtractionService GetService() => GetService(_settings.AiProvider);

    /// <summary>Returns the service for the specified <paramref name="provider"/>.</summary>
    public IAiExtractionService GetService(AiProvider provider) =>
        provider switch
        {
            AiProvider.Anthropic => _anthropic,
            AiProvider.Ollama    => _ollama,
            _                    => _ollama
        };
}
