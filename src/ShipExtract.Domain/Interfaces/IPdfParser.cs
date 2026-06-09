namespace ShipExtract.Domain.Interfaces;

/// <summary>Provides operations for reading and rendering PDF documents.</summary>
public interface IPdfParser
{
    /// <summary>Extracts all selectable text from a PDF file.</summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All text content found in the document.</returns>
    Task<string> ExtractTextAsync(string filePath, CancellationToken ct = default);

    /// <summary>Determines whether a PDF contains selectable (non-scanned) text.</summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the PDF has selectable text; otherwise <see langword="false"/>.</returns>
    Task<bool> HasSelectableTextAsync(string filePath, CancellationToken ct = default);

    /// <summary>Renders each page of a PDF as a PNG image byte array at the specified DPI.</summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <param name="dpi">Rendering resolution in dots per inch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of PNG byte arrays, one per page.</returns>
    Task<IReadOnlyList<byte[]>> RenderPagesToImagesAsync(string filePath, int dpi = 200, CancellationToken ct = default);
}
