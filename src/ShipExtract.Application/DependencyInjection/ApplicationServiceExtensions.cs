using Microsoft.Extensions.DependencyInjection;
using ShipExtract.Application.Pipelines;
using ShipExtract.Application.Services;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Application.DependencyInjection;

/// <summary>Extension methods for registering Application layer services with the DI container.</summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all Application layer services (pipeline and batch processor)
    /// with the supplied <paramref name="services"/> collection.
    /// </summary>
    /// <param name="services">The DI service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, int maxConcurrency)
    {
        services.AddScoped<ExtractionPipeline>();
        services.AddScoped<IBatchProcessingService>(sp =>
            new BatchProcessingService(
                sp.GetRequiredService<ExtractionPipeline>(),
                sp.GetRequiredService<ILoggingService>(),
                maxConcurrency,
                sp.GetService<IBatchHistoryService>()));
        return services;
    }
}
