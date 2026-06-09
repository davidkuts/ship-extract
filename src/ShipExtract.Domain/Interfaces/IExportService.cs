using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Interfaces;

/// <summary>Exports a collection of processing results to a structured file format.</summary>
public interface IExportService
{
    /// <summary>Writes the supplied results to a file at the given path.</summary>
    /// <param name="results">The processing results to export.</param>
    /// <param name="outputPath">Destination file path (will be created or overwritten).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExportAsync(IReadOnlyList<ProcessingResult> results, string outputPath, CancellationToken ct = default);

    /// <summary>Gets the file format produced by this exporter.</summary>
    ExportFormat SupportedFormat { get; }
}
