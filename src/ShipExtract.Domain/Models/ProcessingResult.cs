using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Models;

/// <summary>Contains the outcome of processing a single document file through the extraction pipeline.</summary>
public sealed class ProcessingResult
{
    /// <summary>Unique identifier for this processing job.</summary>
    public Guid JobId { get; init; } = Guid.NewGuid();

    /// <summary>Full path to the source file that was processed.</summary>
    public required string SourceFilePath { get; init; }

    /// <summary>Current processing status.</summary>
    public ProcessingStatus Status { get; set; }

    /// <summary>The extracted shipment record, or <see langword="null"/> if extraction failed.</summary>
    public ShipmentRecord? Record { get; set; }

    /// <summary>Collection of errors encountered during processing.</summary>
    public List<ExtractionError> Errors { get; init; } = [];

    /// <summary>Indicates whether OCR was used as a fallback because the PDF lacked selectable text.</summary>
    public bool UsedOcrFallback { get; set; }

    /// <summary>Wall-clock time taken to process this document.</summary>
    public TimeSpan ProcessingDuration { get; set; }

    /// <summary>UTC timestamp when processing completed.</summary>
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Raw JSON string returned by the AI service, useful for debugging.</summary>
    public string? RawAiResponse { get; set; }

    /// <summary>Raw text extracted from the document (native PDF text or OCR output), truncated to 50,000 characters.</summary>
    public string? ExtractedRawText { get; set; }

    /// <summary>Report describing what the text pre-processing pipeline removed before AI extraction.</summary>
    public PreProcessingReport? PreProcessingReport { get; set; }

    /// <summary>Carrier automatically detected from the document text before AI extraction.</summary>
    public CarrierType DetectedCarrier { get; set; } = CarrierType.Unknown;
}
