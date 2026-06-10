using ClosedXML.Excel;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.Export;

/// <summary>
/// Implements <see cref="IExportService"/> to export shipment results as a formatted Excel (.xlsx) file
/// containing a "Shipments" data sheet and a "Processing Log" diagnostic sheet.
/// </summary>
public sealed class ExcelExportService : IExportService
{
    private static readonly string[] ShipmentHeaders =
    [
        "Tracking Number", "House Bill", "Master Bill", "Carrier", "Service Type",
        "Ship Date", "Est. Delivery Date",
        "Shipper Name", "Shipper Address", "Shipper City", "Shipper Country", "Shipper Postal",
        "Consignee Name", "Consignee Address", "Consignee City", "Consignee Country", "Consignee Postal",
        "Pieces", "Gross Weight (kg)", "Volume (m³)", "Description", "HS Code",
        "Declared Value", "Currency", "Freight Cost",
        "Document Type", "Confidence Score", "OCR Used", "Source File"
    ];

    private static readonly string[] LogHeaders =
    [
        "File Name", "Status", "OCR Used", "Duration (s)", "Error Count", "Errors"
    ];

    /// <inheritdoc/>
    public ExportFormat SupportedFormat => ExportFormat.Excel;

    /// <inheritdoc/>
    public Task ExportAsync(
        IReadOnlyList<ProcessingResult> results,
        string outputPath,
        CancellationToken ct = default) =>
        Task.Run(() => WriteWorkbook(results, outputPath, ct), ct);

    private static void WriteWorkbook(
        IReadOnlyList<ProcessingResult> results, string outputPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var wb = new XLWorkbook();
        WriteShipmentsSheet(wb, results, ct);
        WriteProcessingLogSheet(wb, results, ct);
        wb.SaveAs(outputPath);
    }

    private static void WriteShipmentsSheet(
        XLWorkbook wb, IReadOnlyList<ProcessingResult> results, CancellationToken ct)
    {
        var ws = wb.Worksheets.Add("Shipments");

        // Header row
        for (int i = 0; i < ShipmentHeaders.Length; i++)
            ws.Cell(1, i + 1).Value = ShipmentHeaders[i];

        StyleHeaderRow(ws.Row(1), "#1F4E79");
        ws.SheetView.FreezeRows(1);

        var exportable = results
            .Where(r => r.Record is not null &&
                        r.Status is ProcessingStatus.Succeeded or ProcessingStatus.PartialSuccess)
            .ToList();

        int row = 2;
        foreach (var result in exportable)
        {
            ct.ThrowIfCancellationRequested();
            var r = result.Record!;

            ws.Cell(row,  1).Value = r.TrackingNumber      ?? string.Empty;
            ws.Cell(row,  2).Value = r.HouseBillNumber     ?? string.Empty;
            ws.Cell(row,  3).Value = r.MasterBillNumber    ?? string.Empty;
            ws.Cell(row,  4).Value = !string.IsNullOrWhiteSpace(r.CarrierName)
                ? r.CarrierName
                : (r.DetectedCarrier != Domain.Enums.CarrierType.Unknown ? r.DetectedCarrier.ToString() : string.Empty);
            ws.Cell(row,  5).Value = r.ServiceType         ?? string.Empty;
            ws.Cell(row,  6).Value = r.ShipDate?.ToString("yyyy-MM-dd")              ?? string.Empty;
            ws.Cell(row,  7).Value = r.EstimatedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ws.Cell(row,  8).Value = r.ShipperName         ?? string.Empty;
            ws.Cell(row,  9).Value = r.ShipperAddress      ?? string.Empty;
            ws.Cell(row, 10).Value = r.ShipperCity         ?? string.Empty;
            ws.Cell(row, 11).Value = r.ShipperCountry      ?? string.Empty;
            ws.Cell(row, 12).Value = r.ShipperPostalCode   ?? string.Empty;
            ws.Cell(row, 13).Value = r.ConsigneeName       ?? string.Empty;
            ws.Cell(row, 14).Value = r.ConsigneeAddress    ?? string.Empty;
            ws.Cell(row, 15).Value = r.ConsigneeCity       ?? string.Empty;
            ws.Cell(row, 16).Value = r.ConsigneeCountry    ?? string.Empty;
            ws.Cell(row, 17).Value = r.ConsigneePostalCode ?? string.Empty;
            SetNumericCell(ws.Cell(row, 18), r.NumberOfPieces.HasValue  ? (double?)r.NumberOfPieces.Value  : null);
            SetNumericCell(ws.Cell(row, 19), r.GrossWeightKg.HasValue   ? (double?)r.GrossWeightKg.Value   : null);
            SetNumericCell(ws.Cell(row, 20), r.VolumeM3.HasValue        ? (double?)r.VolumeM3.Value        : null);
            ws.Cell(row, 21).Value = r.Description         ?? string.Empty;
            ws.Cell(row, 22).Value = r.HsCode              ?? string.Empty;
            SetNumericCell(ws.Cell(row, 23), r.DeclaredValue.HasValue   ? (double?)r.DeclaredValue.Value   : null);
            ws.Cell(row, 24).Value = r.Currency            ?? string.Empty;
            SetNumericCell(ws.Cell(row, 25), r.FreightCost.HasValue     ? (double?)r.FreightCost.Value     : null);
            ws.Cell(row, 26).Value = r.DocumentType.ToString();
            ws.Cell(row, 27).Value = r.ConfidenceScore;
            ws.Cell(row, 28).Value = result.UsedOcrFallback;
            ws.Cell(row, 29).Value = r.SourceFileName      ?? string.Empty;
            row++;
        }

        if (exportable.Count > 0)
        {
            var range = ws.Range(1, 1, row - 1, ShipmentHeaders.Length);
            var table = range.CreateTable("ShipmentsTable");
            table.Theme = XLTableTheme.TableStyleMedium2;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteProcessingLogSheet(
        XLWorkbook wb, IReadOnlyList<ProcessingResult> results, CancellationToken ct)
    {
        var ws = wb.Worksheets.Add("Processing Log");

        for (int i = 0; i < LogHeaders.Length; i++)
            ws.Cell(1, i + 1).Value = LogHeaders[i];

        StyleHeaderRow(ws.Row(1), "#833C00");

        int row = 2;
        foreach (var result in results)
        {
            ct.ThrowIfCancellationRequested();
            ws.Cell(row, 1).Value = Path.GetFileName(result.SourceFilePath);
            ws.Cell(row, 2).Value = result.Status.ToString();
            ws.Cell(row, 3).Value = result.UsedOcrFallback;
            ws.Cell(row, 4).Value = Math.Round(result.ProcessingDuration.TotalSeconds, 2);
            ws.Cell(row, 5).Value = result.Errors.Count;
            ws.Cell(row, 6).Value = string.Join("; ", result.Errors.Select(e => e.Message));
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void StyleHeaderRow(IXLRow row, string hexColor)
    {
        row.Style.Font.Bold            = true;
        row.Style.Font.FontColor       = XLColor.White;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml(hexColor);
        row.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        row.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
    }

    private static void SetNumericCell(IXLCell cell, double? value)
    {
        if (value.HasValue)
            cell.Value = value.Value;
        else
            cell.Value = Blank.Value;
    }
}
