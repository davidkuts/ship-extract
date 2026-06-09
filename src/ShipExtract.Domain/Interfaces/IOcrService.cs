namespace ShipExtract.Domain.Interfaces;

/// <summary>Provides optical character recognition services for image-based documents.</summary>
public interface IOcrService
{
    /// <summary>Recognises text from a single image supplied as a byte array.</summary>
    /// <param name="imageBytes">Raw PNG or JPEG image bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recognised text content.</returns>
    Task<string> RecognizeTextAsync(byte[] imageBytes, CancellationToken ct = default);

    /// <summary>Recognises and concatenates text from multiple page images.</summary>
    /// <param name="pageImages">Ordered list of page image byte arrays.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Combined recognised text from all pages.</returns>
    Task<string> RecognizeTextFromPagesAsync(IReadOnlyList<byte[]> pageImages, CancellationToken ct = default);

    /// <summary>Gets a value indicating whether the OCR service is configured and available.</summary>
    bool IsAvailable { get; }
}
