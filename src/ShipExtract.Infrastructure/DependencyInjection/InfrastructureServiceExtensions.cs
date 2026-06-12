using System.Net.Http;
using Anthropic.SDK;
using Microsoft.Extensions.DependencyInjection;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Carriers;
using ShipExtract.Infrastructure.Export;
using ShipExtract.Infrastructure.History;
using ShipExtract.Infrastructure.Logging;
using ShipExtract.Infrastructure.Network;
using ShipExtract.Infrastructure.Ocr;
using ShipExtract.Infrastructure.Pdf;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.Infrastructure.TextProcessing;
using ShipExtract.Infrastructure.Update;

namespace ShipExtract.Infrastructure.DependencyInjection;

/// <summary>Extension methods for registering Infrastructure layer services with the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all Infrastructure services with the supplied service collection.
    /// </summary>
    /// <param name="services">The DI service collection to configure.</param>
    /// <param name="logDirectory">Directory where log files are written.</param>
    /// <param name="tessDataPath">Path to the Tesseract tessdata directory.</param>
    /// <param name="anthropicSettings">Anthropic API configuration.</param>
    /// <param name="appDataRoot">Root app-data directory used for settings persistence.</param>
    /// <param name="appSettings">The loaded application settings singleton.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string logDirectory,
        string tessDataPath,
        AnthropicSettings anthropicSettings,
        string appDataRoot,
        AppSettings appSettings)
    {
        // Settings & credentials
        services.AddSingleton<ISettingsService>(_ => new SettingsService(appDataRoot));
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton(appSettings);

        // Logging
        var serilogLogger = ShipExtractLoggerFactory.CreateLogger(logDirectory);
        services.AddSingleton(serilogLogger);
        services.AddSingleton<ILoggingService, SerilogLoggingService>();

        // PDF parsing
        services.AddSingleton<IPdfParser, PdfPigParser>();

        // OCR (degrades gracefully if tessdata absent; language list from AppSettings)
        services.AddSingleton<IOcrService>(_ =>
            new TesseractOcrService(tessDataPath, appSettings.OcrLanguages));

        // AI extraction — Anthropic
        services.AddSingleton(anthropicSettings);
        services.AddSingleton<AnthropicClient>(_ => new AnthropicClient(
            new APIAuthentication(anthropicSettings.ApiKey)));
        services.AddSingleton<IAnthropicCaller, AnthropicCallerAdapter>();
        services.AddSingleton<AnthropicExtractionService>(sp => new AnthropicExtractionService(
            sp.GetRequiredService<IAnthropicCaller>(),
            sp.GetRequiredService<AnthropicSettings>(),
            sp.GetRequiredService<ILoggingService>(),
            sp.GetRequiredService<ICredentialService>()));

        // AI extraction — Ollama
        services.AddHttpClient<OllamaHealthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<IOllamaHealthService>(sp =>
            new OllamaHealthService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OllamaHealthService))));

        services.AddHttpClient<OllamaExtractionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(300); // 5 minutes — local models can be slow
        });
        services.AddSingleton<OllamaExtractionService>(sp =>
            new OllamaExtractionService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OllamaExtractionService)),
                sp.GetRequiredService<AppSettings>(),
                sp.GetRequiredService<ILoggingService>()));

        // Factory + provider-dispatched IAiExtractionService
        services.AddSingleton<AiExtractionServiceFactory>();
        services.AddSingleton<IAiExtractionService>(sp =>
            sp.GetRequiredService<AiExtractionServiceFactory>().GetService());

        // Carrier detection
        services.AddSingleton<ICarrierDetector, CarrierDetector>();

        // Batch history
        services.AddSingleton<IBatchHistoryService>(_ =>
            new BatchHistoryService(appSettings.HistoryDirectory));

        // Text pre-processing (order determines execution sequence)
        services.AddSingleton<ITextPreProcessor, FormAnnotationCleaner>();
        services.AddSingleton<ITextPreProcessor, WhitespaceNormalizer>();
        services.AddSingleton<ITextPreProcessor, SpacedCharacterNormalizer>();
        services.AddSingleton<ITextPreProcessor, PdfControlSequenceRemover>();
        services.AddSingleton<ITextPreProcessor, DuplicateLineRemover>();
        services.AddSingleton<ITextPreProcessor, LabelValueSeparator>();
        services.AddSingleton<ITextPreProcessingPipeline, TextPreProcessingPipeline>();

        // Export
        services.AddSingleton<IExportService, CsvExportService>();
        services.AddSingleton<IExportService, ExcelExportService>();
        services.AddSingleton<ExportServiceFactory>();

        // Network connectivity checker
        services.AddHttpClient(nameof(NetworkChecker), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<INetworkChecker>(sp =>
            new NetworkChecker(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(NetworkChecker))));

        // Update checker — GitHub Releases API
        services.AddHttpClient(nameof(GitHubUpdateService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ShipExtract/1.0 (github.com/davidkuts/ship-extract)");
        });
        services.AddSingleton<IUpdateService>(sp =>
            new GitHubUpdateService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GitHubUpdateService)),
                repoOwner: "davidkuts",
                repoName:  "ship-extract"));

        return services;
    }
}
