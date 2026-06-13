using Shouldly;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Export;

namespace ShipExtract.Infrastructure.Tests.Export;

/// <summary>Unit tests for <see cref="CsvExportService"/>.</summary>
public sealed class CsvExportServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private static ProcessingResult MakeSucceeded(string tracking = "TRACK001") => new()
    {
        SourceFilePath = "test.pdf",
        Status         = ProcessingStatus.Succeeded,
        Record         = new ShipmentRecord
        {
            TrackingNumber  = tracking,
            ConsigneeName   = "Acme Corp",
            ConfidenceScore = 0.9
        }
    };

    private static ProcessingResult MakeFailed() => new()
    {
        SourceFilePath = "bad.pdf",
        Status         = ProcessingStatus.Failed
    };

    [Fact]
    public async Task NoResults_WritesHeaderOnly()
    {
        var svc = new CsvExportService();
        await svc.ExportAsync([], _tempFile);

        var lines = (await File.ReadAllLinesAsync(_tempFile))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        lines.Length.ShouldBe(1);
        lines[0].ShouldContain("Tracking Number");
    }

    [Fact]
    public async Task SingleSuccessRecord_WritesOneDataRow()
    {
        var svc = new CsvExportService();
        await svc.ExportAsync([MakeSucceeded("AWB12345")], _tempFile);

        var lines = (await File.ReadAllLinesAsync(_tempFile))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        lines.Length.ShouldBeGreaterThanOrEqualTo(2);
        lines[1].ShouldContain("AWB12345");
    }

    [Fact]
    public async Task FailedResultsExcluded()
    {
        var svc = new CsvExportService();
        await svc.ExportAsync([MakeSucceeded(), MakeFailed()], _tempFile);

        var lines = (await File.ReadAllLinesAsync(_tempFile))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        // Header + exactly 1 data row
        lines.Length.ShouldBe(2);
    }
}
