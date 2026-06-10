using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.Export;

/// <summary>
/// Resolves the correct <see cref="IExportService"/> implementation
/// for a requested <see cref="ExportFormat"/>.
/// </summary>
public sealed class ExportServiceFactory
{
    private readonly IEnumerable<IExportService> _services;

    /// <summary>Initialises the factory with all registered export service implementations.</summary>
    /// <param name="services">All <see cref="IExportService"/> registrations from DI.</param>
    public ExportServiceFactory(IEnumerable<IExportService> services)
    {
        _services = services;
    }

    /// <summary>
    /// Returns the <see cref="IExportService"/> that supports the requested format.
    /// </summary>
    /// <param name="format">The export format to resolve.</param>
    /// <returns>The matching service.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no registered service supports <paramref name="format"/>.
    /// </exception>
    public IExportService GetService(ExportFormat format) =>
        _services.FirstOrDefault(s => s.SupportedFormat == format)
        ?? throw new InvalidOperationException(
            $"No export service is registered for format '{format}'.");
}
