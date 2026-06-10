using ClosedXML.Excel;
using FluentAssertions;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Export;

namespace ShipExtract.Infrastructure.Tests.Export;

/// <summary>Unit tests for <see cref="ExcelExportService"/>.</summary>
public sealed class ExcelExportServiceTests : IDisposable
{
    private readonly string _tempFile =
        Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), ".xlsx");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private static ProcessingResult MakeSucceeded() => new()
    {
        SourceFilePath = "good.pdf",
        Status         = ProcessingStatus.Succeeded,
        Record         = new ShipmentRecord
        {
            TrackingNumber  = "T001",
            ConsigneeName   = "Corp A",
            ConfidenceScore = 0.9
        }
    };

    private static ProcessingResult MakeFailed() => new()
    {
        SourceFilePath = "bad.pdf",
        Status         = ProcessingStatus.Failed
    };

    [Fact]
    public async Task CreatesFileWithTwoWorksheets()
    {
        var svc = new ExcelExportService();
        await svc.ExportAsync([MakeSucceeded(), MakeFailed()], _tempFile);

        using var wb = new XLWorkbook(_tempFile);
        wb.Worksheets.Select(w => w.Name)
          .Should().Contain("Shipments")
          .And.Contain("Processing Log");
    }

    [Fact]
    public async Task HeaderRowIsBold()
    {
        var svc = new ExcelExportService();
        await svc.ExportAsync([MakeSucceeded()], _tempFile);

        using var wb = new XLWorkbook(_tempFile);
        wb.Worksheet("Shipments").Row(1).Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessingLogIncludesFailedResults()
    {
        var svc = new ExcelExportService();
        await svc.ExportAsync([MakeSucceeded(), MakeFailed()], _tempFile);

        using var wb = new XLWorkbook(_tempFile);
        var log = wb.Worksheet("Processing Log");
        log.RowsUsed().Count().Should().Be(3); // header + 2 data rows
    }
}
