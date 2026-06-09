using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Commands;

/// <summary>Command representing a request to export processing results to a file.</summary>
/// <param name="Results">The processing results to be exported.</param>
/// <param name="OutputPath">Absolute path of the output file to create or overwrite.</param>
/// <param name="Format">The desired export file format.</param>
public record ExportResultsCommand(
    IReadOnlyList<ProcessingResult> Results,
    string OutputPath,
    ExportFormat Format
);
