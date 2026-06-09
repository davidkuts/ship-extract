using ShipExtract.Domain.Interfaces;
using Tesseract;

namespace ShipExtract.Infrastructure.Ocr;

/// <summary>
/// Implements <see cref="IOcrService"/> using the Tesseract OCR engine.
/// If the engine cannot be initialised the service degrades gracefully,
/// returning empty strings rather than throwing.
/// </summary>
public sealed class TesseractOcrService : IOcrService, IDisposable
{
    private readonly TesseractEngine? _engine;
    private readonly bool _isAvailable;
    private readonly Domain.Interfaces.ILoggingService? _logger;

    /// <summary>
    /// Initialises the Tesseract engine with English language data from
    /// <paramref name="tessDataPath"/>.
    /// </summary>
    /// <param name="tessDataPath">Path to the tessdata directory containing trained data files.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public TesseractOcrService(string tessDataPath, Domain.Interfaces.ILoggingService? logger = null)
    {
        _logger = logger;
        try
        {
            _engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            _isAvailable = true;
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger?.LogWarning(
                "Tesseract OCR engine could not be initialised (tessdata path: {Path}). " +
                "OCR will not be available. Error: {Error}", tessDataPath, ex.Message);
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable => _isAvailable;

    /// <inheritdoc/>
    public Task<string> RecognizeTextAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_isAvailable || _engine is null)
            return Task.FromResult(string.Empty);

        try
        {
            using var pix  = Pix.LoadFromMemory(imageBytes);
            using var page = _engine.Process(pix);
            return Task.FromResult(page.GetText() ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("OCR recognition failed: {Error}", ex.Message);
            return Task.FromResult(string.Empty);
        }
    }

    /// <inheritdoc/>
    public async Task<string> RecognizeTextFromPagesAsync(
        IReadOnlyList<byte[]> pageImages, CancellationToken ct = default)
    {
        var pageTexts = new List<string>(pageImages.Count);

        foreach (var imageBytes in pageImages)
        {
            ct.ThrowIfCancellationRequested();
            var text = await RecognizeTextAsync(imageBytes, ct).ConfigureAwait(false);
            pageTexts.Add(text);
        }

        return string.Join("\n\n--- PAGE BREAK ---\n\n", pageTexts);
    }

    /// <summary>Disposes the underlying Tesseract engine.</summary>
    public void Dispose() => _engine?.Dispose();
}
