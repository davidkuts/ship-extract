using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Commands;

/// <summary>Command representing a request to process a batch of PDF files.</summary>
/// <param name="FilePaths">Ordered list of absolute file paths to process.</param>
/// <param name="OutputDirectory">Directory where export output files will be written.</param>
public record ProcessBatchCommand(
    IReadOnlyList<string> FilePaths,
    string OutputDirectory
);
