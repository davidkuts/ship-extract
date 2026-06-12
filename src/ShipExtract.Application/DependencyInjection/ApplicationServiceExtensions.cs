using Microsoft.Extensions.DependencyInjection;
using ShipExtract.Application.Pipelines;
using ShipExtract.Application.Services;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.DependencyInjection;

/// <summary>Extension methods for registering Application layer services with the DI container.</summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all Application layer services (pipeline and batch processor)
    /// with the supplied <paramref name="services"/> collection.
    /// </summary>
    /// <param name="services">The DI service collection to configure.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent extractions.</param>
    /// <param name="customFieldsProvider">
    /// Optional factory that returns the current custom-fields list at pipeline creation time.
    /// This is called each time a scoped <see cref="ExtractionPipeline"/> is created so that
    /// changes made in Settings take effect for the next batch.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        int maxConcurrency,
        Func<List<CustomField>?>? customFieldsProvider = null)
    {
        services.AddScoped<ExtractionPipeline>(sp => new ExtractionPipeline(
            sp.GetRequiredService<IPdfParser>(),
            sp.GetRequiredService<IOcrService>(),
            sp.GetRequiredService<IAiExtractionService>(),
            sp.GetRequiredService<ILoggingService>(),
            sp.GetService<ITextPreProcessingPipeline>(),
            sp.GetService<ICarrierDetector>(),
            customFieldsProvider?.Invoke()));

        services.AddScoped<IBatchProcessingService>(sp =>
            new BatchProcessingService(
                sp.GetRequiredService<ExtractionPipeline>(),
                sp.GetRequiredService<ILoggingService>(),
                maxConcurrency,
                sp.GetService<IBatchHistoryService>(),
                sp.GetService<INetworkChecker>()));
        return services;
    }
}
