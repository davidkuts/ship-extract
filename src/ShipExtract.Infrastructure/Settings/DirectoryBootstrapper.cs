namespace ShipExtract.Infrastructure.Settings;

/// <summary>Ensures required application directories exist on disk at startup.</summary>
public static class DirectoryBootstrapper
{
    /// <summary>
    /// Creates all required directories and writes a tessdata README if no language
    /// data files are present.
    /// </summary>
    public static void EnsureDirectories(AppSettings settings)
    {
        CreateIfMissing(settings.LogDirectory);
        CreateIfMissing(settings.TessDataDirectory);
        CreateIfMissing(settings.DefaultOutputDirectory);

        var tessDataDir    = settings.TessDataDirectory;
        bool hasTrainedData = !string.IsNullOrWhiteSpace(tessDataDir)
                              && Directory.Exists(tessDataDir)
                              && Directory.GetFiles(tessDataDir, "*.traineddata").Length > 0;

        if (!hasTrainedData && !string.IsNullOrWhiteSpace(tessDataDir))
        {
            var readmePath = Path.Combine(tessDataDir, "README.txt");
            if (!File.Exists(readmePath))
            {
                File.WriteAllText(readmePath,
                    """
                    Tesseract language data files are required for OCR on scanned PDFs.

                    To enable OCR:
                    1. Download eng.traineddata from:
                       https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
                    2. Place the file in this folder.
                    3. Restart ShipExtract.

                    Without this file, ShipExtract will only process PDFs
                    with selectable (digital) text.
                    """);
            }
        }
    }

    private static void CreateIfMissing(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            Directory.CreateDirectory(path);
    }
}
