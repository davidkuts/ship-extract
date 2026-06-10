using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Interfaces;

/// <summary>Encapsulates the response from an AI-powered extraction call.</summary>
/// <param name="Record">The extracted shipment record, or <see langword="null"/> if extraction failed.</param>
/// <param name="ConfidenceScore">AI-reported confidence in the extraction result, in the range 0.0–1.0.</param>
/// <param name="RawJson">The raw JSON string returned by the AI service.</param>
/// <param name="Success">Indicates whether the extraction completed without errors.</param>
/// <param name="ErrorMessage">Description of the error when <paramref name="Success"/> is <see langword="false"/>.</param>
public record AiExtractionResponse(
    ShipmentRecord? Record,
    double ConfidenceScore,
    string RawJson,
    bool Success,
    string? ErrorMessage
);

/// <summary>Uses an AI language model to extract structured shipment data from raw document text.</summary>
public interface IAiExtractionService
{
    /// <summary>Sends raw document text to the AI service and returns a structured extraction result.</summary>
    /// <param name="rawText">Plain text content of the document to be analysed.</param>
    /// <param name="hint">Optional document-type hint to improve extraction accuracy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="AiExtractionResponse"/> containing the structured data and metadata.</returns>
    Task<AiExtractionResponse> ExtractAsync(string rawText, DocumentType hint = DocumentType.Unknown, CancellationToken ct = default);

    /// <summary>
    /// Carrier-aware overload (v1.1). Implementations use <paramref name="carrier"/> to apply
    /// carrier-specific prompt hints. The default implementation ignores carrier and delegates
    /// to <see cref="ExtractAsync(string,DocumentType,CancellationToken)"/> for backward compatibility.
    /// </summary>
    Task<AiExtractionResponse> ExtractAsync(
        string rawText,
        DocumentType hint,
        CarrierType carrier,
        CancellationToken ct = default) =>
        ExtractAsync(rawText, hint, ct);
}
