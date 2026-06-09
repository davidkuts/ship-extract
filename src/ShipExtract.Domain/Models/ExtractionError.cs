using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Models;

/// <summary>Describes a single error that occurred during the extraction pipeline.</summary>
public sealed class ExtractionError
{
    /// <summary>Categorised error code identifying the failure type.</summary>
    public required ExtractionErrorCode Code { get; init; }

    /// <summary>Human-readable description of what went wrong.</summary>
    public required string Message { get; init; }

    /// <summary>The name of the field that failed validation or extraction, if applicable.</summary>
    public string? FieldName { get; init; }

    /// <summary>The underlying exception, if one was caught.</summary>
    public Exception? Exception { get; init; }
}
