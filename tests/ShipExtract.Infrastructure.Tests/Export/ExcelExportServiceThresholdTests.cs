using ClosedXML.Excel;
using Shouldly;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Export;

namespace ShipExtract.Infrastructure.Tests.Export;

/// <summary>Tests that <see cref="ExcelExportService"/> correctly splits results by confidence threshold.</summary>
public sealed class ExcelExportServiceThresholdTests : IDisposable
{
    private readonly string _tempFile =
        Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), ".xlsx");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private static ProcessingResult MakeResult(double confidence) => new()
    {
        SourceFilePath = "test.pdf",
        Status         = ProcessingStatus.Succeeded,
        Record         = new ShipmentRecord
        {
            TrackingNumber  = $"T{confidence:F2}",
            ConsigneeName   = "Test Corp",
            ConfidenceScore = confidence
        }
    };

    [Fact]
    public async Task ResultsBelowThreshold_GoToReviewSheet()
    {
        var results = new[]
        {
            MakeResult(0.80),
            MakeResult(0.80),
            MakeResult(0.40),
        };

        var svc = new ExcelExportService();
        await svc.ExportAsync(results, _tempFile, confidenceThreshold: 0.60);

        using var wb = new XLWorkbook(_tempFile);

        wb.Worksheets.Any(w => w.Name == "Review Required").ShouldBeTrue();

        var shipments = wb.Worksheet("Shipments");
        // Row 1 = header, rows 2+ = data
        shipments.RowsUsed().Count().ShouldBe(3); // header + 2 above-threshold rows

        var review = wb.Worksheet("Review Required");
        // Row 1 = header, row 2 = note, row 3+ = data
        review.RowsUsed().Count().ShouldBe(3); // header + note + 1 below-threshold row
    }

    [Fact]
    public async Task AllResultsAboveThreshold_NoReviewSheet()
    {
        var results = new[]
        {
            MakeResult(0.80),
            MakeResult(0.75),
        };

        var svc = new ExcelExportService();
        await svc.ExportAsync(results, _tempFile, confidenceThreshold: 0.60);

        using var wb = new XLWorkbook(_tempFile);
        wb.Worksheets.Any(w => w.Name == "Review Required").ShouldBeFalse();
    }

    [Fact]
    public async Task ThresholdZero_AllInMainSheet()
    {
        var results = new[]
        {
            MakeResult(0.10),
            MakeResult(0.30),
            MakeResult(0.90),
        };

        var svc = new ExcelExportService();
        await svc.ExportAsync(results, _tempFile, confidenceThreshold: 0.0);

        using var wb = new XLWorkbook(_tempFile);
        wb.Worksheets.Any(w => w.Name == "Review Required").ShouldBeFalse();
        var shipments = wb.Worksheet("Shipments");
        shipments.RowsUsed().Count().ShouldBe(4); // header + 3 data rows
    }
}
