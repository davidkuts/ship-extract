using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.History;

// ─────────────────────────────────────────────────────────────────────────────
// Internal serialization DTOs for history persistence.
//
// Why DTOs instead of direct model serialization?
//   ExtractionError.Exception is typed as System.Exception, which contains
//   MethodBase (TargetSite), StackTrace objects, and other runtime types that
//   System.Text.Json cannot serialize. These DTOs strip the Exception to a pair
//   of plain strings (ExceptionType, ExceptionMessage) that are safe and compact.
//
// What is intentionally NOT persisted:
//   - RawAiResponse    — can be hundreds of KB per file; not useful in history UI
//   - ExtractedRawText — same reason
//   - PreProcessingReport.CleanedText — same reason
//   - ExtractionError.Exception — replaced by ExceptionType/ExceptionMessage strings
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Serialization-safe snapshot of a <see cref="BatchHistoryEntry"/>.</summary>
internal sealed class BatchHistoryEntryDto
{
    public Guid           BatchId              { get; init; }
    public DateTimeOffset CompletedAt          { get; init; }
    public int            TotalFiles           { get; init; }
    public int            SuccessCount         { get; init; }
    public int            FailureCount         { get; init; }
    public double         TotalDurationSeconds { get; init; }
    public CarrierType    PrimaryCarrier       { get; init; } = CarrierType.Unknown;
    public List<ProcessingResultDto> Results   { get; init; } = [];
}

/// <summary>Serialization-safe snapshot of a <see cref="ProcessingResult"/>.</summary>
internal sealed class ProcessingResultDto
{
    public Guid            JobId              { get; init; }
    public string          SourceFilePath     { get; init; } = "";
    public ProcessingStatus Status            { get; init; }
    public ShipmentRecord? Record             { get; init; }
    public List<ExtractionErrorDto> Errors    { get; init; } = [];
    public bool            UsedOcrFallback    { get; init; }
    public bool            UsedFallbackExtraction { get; init; }
    public TimeSpan        ProcessingDuration { get; init; }
    public DateTimeOffset  ProcessedAt        { get; init; }
    public CarrierType     DetectedCarrier    { get; init; } = CarrierType.Unknown;
    public PreProcessingReportDto? PreProcessingReport { get; init; }
    // RawAiResponse and ExtractedRawText intentionally omitted (large, not needed in history)
}

/// <summary>Serialization-safe snapshot of an <see cref="ExtractionError"/>.</summary>
internal sealed class ExtractionErrorDto
{
    public ExtractionErrorCode Code             { get; init; }
    public string              Message          { get; init; } = "";
    public string?             FieldName        { get; init; }
    // Exception replaced by plain strings to avoid MethodBase/StackTrace serialization issues
    public string?             ExceptionType    { get; init; }
    public string?             ExceptionMessage { get; init; }
}

/// <summary>
/// Serialization-safe snapshot of a <see cref="Domain.Models.PreProcessingReport"/>.
/// Computed properties (CharactersRemoved, ReductionPercent) and CleanedText are omitted.
/// </summary>
internal sealed class PreProcessingReportDto
{
    public int          OriginalCharacterCount { get; init; }
    public int          CleanedCharacterCount  { get; init; }
    public List<string> StepsApplied           { get; init; } = [];
    // CleanedText intentionally omitted (large, not needed in history)
}
