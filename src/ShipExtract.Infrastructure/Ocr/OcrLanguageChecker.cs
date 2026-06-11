namespace ShipExtract.Infrastructure.Ocr;

/// <summary>Checks which Tesseract language data files are present and provides download URLs for missing ones.</summary>
public static class OcrLanguageChecker
{
    private static readonly Dictionary<string, string> KnownDownloadUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata",
        ["deu"] = "https://github.com/tesseract-ocr/tessdata/raw/main/deu.traineddata",
        ["fra"] = "https://github.com/tesseract-ocr/tessdata/raw/main/fra.traineddata",
    };

    /// <summary>
    /// Checks which of the requested language codes have .traineddata files present in
    /// <paramref name="tessDataPath"/> and which are missing.
    /// </summary>
    /// <param name="tessDataPath">Path to the tessdata directory.</param>
    /// <param name="requestedLanguages">Language codes to check (e.g. ["eng", "deu", "fra"]).</param>
    /// <returns>An <see cref="OcrLanguageStatus"/> describing availability and download URLs.</returns>
    public static OcrLanguageStatus Check(string tessDataPath, List<string> requestedLanguages)
    {
        var available = new List<string>();
        var missing   = new List<string>();

        foreach (var lang in requestedLanguages)
        {
            var trainedDataFile = Path.Combine(tessDataPath ?? string.Empty, $"{lang}.traineddata");
            if (File.Exists(trainedDataFile))
                available.Add(lang);
            else
                missing.Add(lang);
        }

        // Return download URLs for all known languages (not just missing ones, so the UI can always show them)
        return new OcrLanguageStatus(available, missing, KnownDownloadUrls);
    }

    /// <summary>Gets the download URL for a known language code, or null if unknown.</summary>
    public static string? GetDownloadUrl(string languageCode) =>
        KnownDownloadUrls.TryGetValue(languageCode, out var url) ? url : null;
}

/// <summary>Result of an OCR language availability check.</summary>
/// <param name="AvailableLanguages">Language codes whose .traineddata files were found.</param>
/// <param name="MissingLanguages">Language codes whose .traineddata files were not found.</param>
/// <param name="DownloadUrls">Known download URLs keyed by language code.</param>
public record OcrLanguageStatus(
    List<string> AvailableLanguages,
    List<string> MissingLanguages,
    Dictionary<string, string> DownloadUrls
);
