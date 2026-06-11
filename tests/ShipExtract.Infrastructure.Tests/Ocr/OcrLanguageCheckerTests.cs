using FluentAssertions;
using ShipExtract.Infrastructure.Ocr;

namespace ShipExtract.Infrastructure.Tests.Ocr;

/// <summary>Tests for <see cref="OcrLanguageChecker"/>.</summary>
public sealed class OcrLanguageCheckerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public OcrLanguageCheckerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ReturnsAvailable_WhenTrainedDataExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "eng.traineddata"), "fake");

        var status = OcrLanguageChecker.Check(_tempDir, new List<string> { "eng" });

        status.AvailableLanguages.Should().Contain("eng");
        status.MissingLanguages.Should().NotContain("eng");
    }

    [Fact]
    public void ReturnsMissing_WhenFileAbsent()
    {
        // tempDir is empty — no .traineddata files
        var status = OcrLanguageChecker.Check(_tempDir, new List<string> { "eng" });

        status.MissingLanguages.Should().Contain("eng");
        status.AvailableLanguages.Should().NotContain("eng");
    }

    [Fact]
    public void DownloadUrls_PresentForKnownLanguages()
    {
        var status = OcrLanguageChecker.Check(_tempDir, new List<string> { "eng", "deu", "fra" });

        status.DownloadUrls.Should().ContainKey("eng");
        status.DownloadUrls.Should().ContainKey("deu");
        status.DownloadUrls.Should().ContainKey("fra");
    }
}
