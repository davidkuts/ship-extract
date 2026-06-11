using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Exceptions;
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

    // Maximum characters extracted for AI — pages are ranked by keyword density before truncation.
    private const int MaxTextChars = 8000;

    private static readonly string[] ShipmentKeywords =
    [
        "tracking", "consignee", "shipper", "shipment", "waybill", "bill", "weight",
        "cargo", "invoice", "freight", "delivery", "address", "country", "package",
        "customs", "hawb", "mawb", "pieces", "gross"
    ];

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
        ValidatePdfFile(filePath);

        try
        {
            using var document = OpenDocument(filePath);
            var pages = new List<string>();

            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var words = page.GetWords();
                pages.Add(string.Join(" ", words.Select(w => w.Text)));
            }

            return Task.FromResult(SmartTruncate(pages));
        }
        catch (ShipExtractException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ShipExtractException(ExtractionErrorCode.PdfReadFailure,
                "The PDF could not be read. It may be damaged or in an unsupported format.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<bool> HasSelectableTextAsync(string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidatePdfFile(filePath);

        try
        {
            using var document = OpenDocument(filePath);
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                if (page.GetWords().Any())
                    return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (ShipExtractException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ShipExtractException(ExtractionErrorCode.PdfReadFailure,
                "The PDF could not be read. It may be damaged or in an unsupported format.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<byte[]>> RenderPagesToImagesAsync(
        string filePath, int dpi = 200, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidatePdfFile(filePath);

        try
        {
            return Task.FromResult(RenderWithDocnet(filePath, dpi, ct));
        }
        catch (ShipExtractException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Page rendering failed for {FilePath} — returning empty list. Reason: {Reason}",
                filePath, ex.Message);
            return Task.FromResult<IReadOnlyList<byte[]>>([]);
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that <paramref name="filePath"/> exists, is non-empty, and starts with the
    /// <c>%PDF</c> magic bytes. Throws <see cref="ShipExtractException"/> on any failure.
    /// </summary>
    private static void ValidatePdfFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new ShipExtractException(ExtractionErrorCode.PdfReadFailure,
                $"File not found: {Path.GetFileName(filePath)}");

        var info = new FileInfo(filePath);
        if (info.Length == 0)
            throw new ShipExtractException(ExtractionErrorCode.EmptyFile,
                $"The file is empty (0 bytes): {Path.GetFileName(filePath)}");

        // Check %PDF magic bytes
        using var fs = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[4];
        int read = fs.Read(header);
        if (read < 4 || header[0] != '%' || header[1] != 'P' || header[2] != 'D' || header[3] != 'F')
            throw new ShipExtractException(ExtractionErrorCode.CorruptFile,
                $"Not a valid PDF file (missing %%PDF header): {Path.GetFileName(filePath)}");
    }

    // ── Password/encryption detection ────────────────────────────────────────

    private static PdfDocument OpenDocument(string filePath)
    {
        try
        {
            return PdfDocument.Open(filePath);
        }
        catch (Exception ex) when (IsPasswordOrEncryptionError(ex))
        {
            throw new ShipExtractException(ExtractionErrorCode.PasswordProtected,
                $"The PDF is password-protected and cannot be opened: {Path.GetFileName(filePath)}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ShipExtractException(ExtractionErrorCode.CorruptFile,
                $"The PDF could not be opened. It may be corrupt or use an unsupported format: {Path.GetFileName(filePath)}", ex);
        }
    }

    private static bool IsPasswordOrEncryptionError(Exception ex) =>
        ex.Message.Contains("password",  StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("encrypt",   StringComparison.OrdinalIgnoreCase) ||
        ex.GetType().Name.Contains("Encrypt",  StringComparison.OrdinalIgnoreCase) ||
        ex.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase);

    // ── Smart truncation ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns extracted text up to <see cref="MaxTextChars"/> characters.
    /// When the combined page text exceeds the limit, pages are ranked by shipment-keyword
    /// density and the highest-scoring pages are included first.
    /// </summary>
    private static string SmartTruncate(List<string> pageTexts)
    {
        var combined = string.Join(Environment.NewLine, pageTexts);
        if (combined.Length <= MaxTextChars)
            return combined;

        // Score each page by keyword density, preserving original index for stable sort.
        var scored = pageTexts
            .Select((text, index) => (text, index, score: ScorePage(text)))
            .OrderByDescending(p => p.score)
            .ThenBy(p => p.index)
            .ToList();

        var sb = new System.Text.StringBuilder(MaxTextChars);
        foreach (var (text, _, _) in scored)
        {
            int needed = sb.Length > 0 ? text.Length + Environment.NewLine.Length : text.Length;
            if (sb.Length + needed > MaxTextChars) break;

            if (sb.Length > 0) sb.Append(Environment.NewLine);
            sb.Append(text);
        }

        return sb.ToString();
    }

    private static int ScorePage(string text)
    {
        var lower = text.ToLowerInvariant();
        return ShipmentKeywords.Count(kw => lower.Contains(kw));
    }

    // ── Docnet rendering ─────────────────────────────────────────────────────

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

            var png = ConvertBgraToPng(rawBgra, width, height);
            results.Add(png);
        }

        return results;
    }

    private static byte[] ConvertBgraToPng(byte[] bgra, int width, int height)
    {
        // Build a minimal valid BMP (which Tesseract can read) from raw BGRA data
        const int headerSize = 14 + 40;
        int rowSize = (width * 3 + 3) & ~3;
        int pixelDataSize = rowSize * height;
        int fileSize = headerSize + pixelDataSize;

        var bmp = new byte[fileSize];

        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        WriteInt32(bmp, 2, fileSize);
        WriteInt32(bmp, 10, headerSize);

        WriteInt32(bmp, 14, 40);
        WriteInt32(bmp, 18, width);
        WriteInt32(bmp, 22, -height);
        WriteInt16(bmp, 26, 1);
        WriteInt16(bmp, 28, 24);
        WriteInt32(bmp, 30, 0);
        WriteInt32(bmp, 34, pixelDataSize);

        int dest = headerSize;
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width * 4;
            int written = 0;
            for (int x = 0; x < width; x++)
            {
                int src = rowStart + x * 4;
                bmp[dest++] = bgra[src];
                bmp[dest++] = bgra[src + 1];
                bmp[dest++] = bgra[src + 2];
                written += 3;
            }
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
