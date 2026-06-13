using Shouldly;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Export;

namespace ShipExtract.Infrastructure.Tests.Export;

/// <summary>Tests that <see cref="CsvExportService"/> correctly splits results by confidence threshold.</summary>
public sealed class CsvExportServiceThresholdTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);

        var reviewPath = ReviewFilePath(_tempFile);
        if (File.Exists(reviewPath)) File.Delete(reviewPath);
    }

    private static string ReviewFilePath(string mainPath)
    {
        var dir      = Path.GetDirectoryName(mainPath) ?? string.Empty;
        var nameBase = Path.GetFileNameWithoutExtension(mainPath);
        var ext      = Path.GetExtension(mainPath);
        return Path.Combine(dir, $"{nameBase}_review{ext}");
    }

    private static ProcessingResult MakeResult(double confidence, string tracking = "T001") => new()
    {
        SourceFilePath = "test.pdf",
        Status         = ProcessingStatus.Succeeded,
        Record         = new ShipmentRecord
        {
            TrackingNumber  = tracking,
            ConsigneeName   = "Test Corp",
            ConfidenceScore = confidence
        }
    };

    [Fact]
    public async Task BelowThreshold_CreatesReviewFile()
    {
        var results = new[]
        {
            MakeResult(0.80, "T-ABOVE"),
            MakeResult(0.30, "T-BELOW"),
        };

        var svc = new CsvExportService();
        await svc.ExportAsync(results, _tempFile, confidenceThreshold: 0.60);

        // Main CSV should have 1 data row (above threshold)
        var mainLines = (await File.ReadAllLinesAsync(_tempFile))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
            .ToArray();
        mainLines.Length.ShouldBe(2); // header + 1 data row
        mainLines[1].ShouldContain("T-ABOVE");

        // Review CSV should exist and have 1 data row
        var reviewPath = ReviewFilePath(_tempFile);
        File.Exists(reviewPath).ShouldBeTrue();

        var reviewLines = (await File.ReadAllLinesAsync(reviewPath))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
            .ToArray();
        reviewLines.Length.ShouldBe(2); // header + 1 data row
        reviewLines[1].ShouldContain("T-BELOW");
    }

    [Fact]
    public async Task AllAboveThreshold_NoReviewFile()
    {
        var results = new[]
        {
            MakeResult(0.80, "T-A"),
            MakeResult(0.90, "T-B"),
        };

        var svc = new CsvExportService();
        await svc.ExportAsync(results, _tempFile, confidenceThreshold: 0.60);

        var reviewPath = ReviewFilePath(_tempFile);
        File.Exists(reviewPath).ShouldBeFalse();
    }
}
