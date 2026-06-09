using Microsoft.Extensions.DependencyInjection;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.Logging;
using ShipExtract.Infrastructure.Ocr;
using ShipExtract.Infrastructure.Pdf;

namespace ShipExtract.Infrastructure.DependencyInjection;

/// <summary>Extension methods for registering Infrastructure layer services with the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all Infrastructure services (logging, PDF parsing, OCR) with
    /// the supplied <paramref name="services"/> collection.
    /// </summary>
    /// <param name="services">The DI service collection to configure.</param>
    /// <param name="logDirectory">Directory where log files will be written.</param>
    /// <param name="tessDataPath">Path to the Tesseract tessdata directory.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string logDirectory,
        string tessDataPath)
    {
        // Logging
        var serilogLogger = ShipExtractLoggerFactory.CreateLogger(logDirectory);
        services.AddSingleton(serilogLogger);
        services.AddSingleton<ILoggingService, SerilogLoggingService>();

        // PDF parsing
        services.AddSingleton<IPdfParser, PdfPigParser>();

        // OCR
        services.AddSingleton<IOcrService>(_ => new TesseractOcrService(tessDataPath));

        return services;
    }
}
