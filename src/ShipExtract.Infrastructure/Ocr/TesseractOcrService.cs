using ShipExtract.Domain.Interfaces;
using Tesseract;

namespace ShipExtract.Infrastructure.Ocr;

/// <summary>
/// Implements <see cref="IOcrService"/> using the Tesseract OCR engine.
/// Supports multiple languages specified by Tesseract language codes (e.g. "eng", "deu", "fra").
/// If the combined language string fails to initialise, falls back to English only.
/// If that also fails, the service degrades gracefully returning empty strings.
/// </summary>
public sealed class TesseractOcrService : IOcrService, IDisposable
{
    private readonly TesseractEngine? _engine;
    private readonly bool _isAvailable;
    private readonly Domain.Interfaces.ILoggingService? _logger;

    /// <summary>Gets the list of language codes that were successfully loaded into the engine.</summary>
    public IReadOnlyList<string> ActiveLanguages { get; private set; } = [];

    /// <summary>
    /// Initialises the Tesseract engine with the specified languages from <paramref name="tessDataPath"/>.
    /// Falls back to English-only on failure, and disables OCR if English also fails.
    /// </summary>
    /// <param name="tessDataPath">Path to the tessdata directory containing trained data files.</param>
    /// <param name="languages">Tesseract language codes to load (e.g. ["eng", "deu"]). "eng" is always included.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public TesseractOcrService(
        string tessDataPath,
        List<string>? languages = null,
        Domain.Interfaces.ILoggingService? logger = null)
    {
        _logger = logger;

        // Ensure "eng" is always present and deduplicate
        var requestedLangs = (languages ?? new List<string> { "eng" })
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .ToList();

        if (!requestedLangs.Contains("eng", StringComparer.OrdinalIgnoreCase))
            requestedLangs.Insert(0, "eng");

        // Attempt initialisation with the combined language string
        var langString = string.Join("+", requestedLangs);
        try
        {
            _engine      = new TesseractEngine(tessDataPath, langString, EngineMode.Default);
            _isAvailable = true;
            ActiveLanguages = requestedLangs.AsReadOnly();
        }
        catch (Exception ex) when (requestedLangs.Count > 1)
        {
            // Log which languages failed and fall back to English only
            _logger?.LogWarning(
                "Tesseract failed to load combined languages [{Langs}]: {Message} — falling back to English only",
                langString, ex.Message);

            try
            {
                _engine      = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                _isAvailable = true;
                ActiveLanguages = new[] { "eng" };
            }
            catch (Exception fallbackEx)
            {
                _isAvailable = false;
                _logger?.LogWarning(
                    "Tesseract unavailable even with English only: {Message} — OCR fallback disabled",
                    fallbackEx.Message);
            }
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger?.LogWarning(
                "Tesseract unavailable: {Message} — OCR fallback disabled", ex.Message);
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
