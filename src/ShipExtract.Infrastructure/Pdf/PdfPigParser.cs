using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ShipExtract.Infrastructure.Pdf;

/// <summary>
/// Implements <see cref="IPdfParser"/> using the PdfPig library for text extraction
/// and Docnet.Core for page rendering.
/// </summary>
public sealed class PdfPigParser : IPdfParser
{
    private readonly Domain.Interfaces.ILoggingService _logger;

    /// <summary>Initialises a new instance of <see cref="PdfPigParser"/>.</summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PdfPigParser(Domain.Interfaces.ILoggingService logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<string> ExtractTextAsync(string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(filePath);
        var pages = new List<string>();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var words = page.GetWords();
            pages.Add(string.Join(" ", words.Select(w => w.Text)));
        }

        return Task.FromResult(string.Join(Environment.NewLine, pages));
    }

    /// <inheritdoc/>
    public Task<bool> HasSelectableTextAsync(string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            if (page.GetWords().Any())
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<byte[]>> RenderPagesToImagesAsync(
        string filePath, int dpi = 200, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult(RenderWithDocnet(filePath, dpi, ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Page rendering failed for {FilePath} — returning empty list. Reason: {Reason}",
                filePath, ex.Message);
            return Task.FromResult<IReadOnlyList<byte[]>>([]);
        }
    }

    private static IReadOnlyList<byte[]> RenderWithDocnet(string filePath, int dpi, CancellationToken ct)
    {
        var results = new List<byte[]>();

        using var lib = Docnet.Core.DocLib.Instance;
        using var docReader = lib.GetDocReader(filePath, new Docnet.Core.Models.PageDimensions(dpi, dpi));

        int pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var pageReader = docReader.GetPageReader(i);
            int width  = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();
            byte[] rawBgra = pageReader.GetImage();

            // Convert raw BGRA to PNG using System.Drawing or SkiaSharp
            // Use a simple BMP-style conversion: write a 24-bit bitmap in memory
            var png = ConvertBgraToPng(rawBgra, width, height);
            results.Add(png);
        }

        return results;
    }

    private static byte[] ConvertBgraToPng(byte[] bgra, int width, int height)
    {
        // Build a minimal valid BMP (which Tesseract can read) from raw BGRA data
        // BMP file header (14 bytes) + DIB header (40 bytes) + pixel data
        const int headerSize = 14 + 40;
        int rowSize = (width * 3 + 3) & ~3; // 24-bit, padded to 4 bytes
        int pixelDataSize = rowSize * height;
        int fileSize = headerSize + pixelDataSize;

        var bmp = new byte[fileSize];

        // BMP file header
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        WriteInt32(bmp, 2, fileSize);
        WriteInt32(bmp, 10, headerSize);

        // DIB header (BITMAPINFOHEADER)
        WriteInt32(bmp, 14, 40);          // header size
        WriteInt32(bmp, 18, width);
        WriteInt32(bmp, 22, -height);     // negative = top-down
        WriteInt16(bmp, 26, 1);           // color planes
        WriteInt16(bmp, 28, 24);          // bits per pixel
        WriteInt32(bmp, 30, 0);           // BI_RGB compression
        WriteInt32(bmp, 34, pixelDataSize);

        // Pixel data: convert BGRA → BGR row by row
        int dest = headerSize;
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width * 4;
            int written = 0;
            for (int x = 0; x < width; x++)
            {
                int src = rowStart + x * 4;
                bmp[dest++] = bgra[src];     // B
                bmp[dest++] = bgra[src + 1]; // G
                bmp[dest++] = bgra[src + 2]; // R
                written += 3;
            }
            // Row padding
            int pad = rowSize - written;
            dest += pad;
        }

        return bmp;
    }

    private static void WriteInt32(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteInt16(byte[] buf, int offset, short value)
    {
        buf[offset]     = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
    }
}
