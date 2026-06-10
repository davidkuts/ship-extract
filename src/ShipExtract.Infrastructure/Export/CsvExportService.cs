using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.Export;

/// <summary>
/// Implements <see cref="IExportService"/> to export shipment results as a UTF-8 CSV file.
/// The output includes a BOM so that Excel auto-detects encoding correctly.
/// </summary>
public sealed class CsvExportService : IExportService
{
    /// <inheritdoc/>
    public ExportFormat SupportedFormat => ExportFormat.Csv;

    /// <inheritdoc/>
    public Task ExportAsync(
        IReadOnlyList<ProcessingResult> results,
        string outputPath,
        CancellationToken ct = default) =>
        Task.Run(() => WriteFile(results, outputPath, ct), ct);

    private static void WriteFile(
        IReadOnlyList<ProcessingResult> results, string outputPath, CancellationToken ct)
    {
        var exportable = results
            .Where(r => r.Record is not null &&
                        r.Status is ProcessingStatus.Succeeded or ProcessingStatus.PartialSuccess)
            .ToList();

        using var writer = new StreamWriter(
            outputPath, append: false,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        csv.Context.RegisterClassMap<CsvExportMap>();
        csv.WriteHeader<ShipmentCsvRow>();
        csv.NextRecord();

        foreach (var result in exportable)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteRecord(new ShipmentCsvRow(result));
            csv.NextRecord();
        }
    }
}

/// <summary>Flat row projection of a <see cref="ProcessingResult"/> for CSV output.</summary>
internal sealed class ShipmentCsvRow
{
    public string? TrackingNumber   { get; }
    public string? HouseBill        { get; }
    public string? MasterBill       { get; }
    public string? Carrier          { get; }
    public string? ServiceType      { get; }
    public string? ShipDate         { get; }
    public string? EstDeliveryDate  { get; }
    public string? ShipperName      { get; }
    public string? ShipperAddress   { get; }
    public string? ShipperCity      { get; }
    public string? ShipperCountry   { get; }
    public string? ShipperPostal    { get; }
    public string? ConsigneeName    { get; }
    public string? ConsigneeAddress { get; }
    public string? ConsigneeCity    { get; }
    public string? ConsigneeCountry { get; }
    public string? ConsigneePostal  { get; }
    public int?    Pieces           { get; }
    public decimal? GrossWeightKg   { get; }
    public decimal? VolumeM3        { get; }
    public string? Description      { get; }
    public string? HsCode           { get; }
    public decimal? DeclaredValue   { get; }
    public string? Currency         { get; }
    public decimal? FreightCost     { get; }
    public string  DocumentType     { get; }
    public double  ConfidenceScore  { get; }
    public bool    OcrUsed          { get; }
    public string? SourceFile       { get; }

    public ShipmentCsvRow(ProcessingResult result)
    {
        var r = result.Record!;
        TrackingNumber   = r.TrackingNumber;
        HouseBill        = r.HouseBillNumber;
        MasterBill       = r.MasterBillNumber;
        Carrier          = !string.IsNullOrWhiteSpace(r.CarrierName)
            ? r.CarrierName
            : (r.DetectedCarrier != Domain.Enums.CarrierType.Unknown ? r.DetectedCarrier.ToString() : null);
        ServiceType      = r.ServiceType;
        ShipDate         = r.ShipDate?.ToString("yyyy-MM-dd");
        EstDeliveryDate  = r.EstimatedDeliveryDate?.ToString("yyyy-MM-dd");
        ShipperName      = r.ShipperName;
        ShipperAddress   = r.ShipperAddress;
        ShipperCity      = r.ShipperCity;
        ShipperCountry   = r.ShipperCountry;
        ShipperPostal    = r.ShipperPostalCode;
        ConsigneeName    = r.ConsigneeName;
        ConsigneeAddress = r.ConsigneeAddress;
        ConsigneeCity    = r.ConsigneeCity;
        ConsigneeCountry = r.ConsigneeCountry;
        ConsigneePostal  = r.ConsigneePostalCode;
        Pieces           = r.NumberOfPieces;
        GrossWeightKg    = r.GrossWeightKg;
        VolumeM3         = r.VolumeM3;
        Description      = r.Description;
        HsCode           = r.HsCode;
        DeclaredValue    = r.DeclaredValue;
        Currency         = r.Currency;
        FreightCost      = r.FreightCost;
        DocumentType     = r.DocumentType.ToString();
        ConfidenceScore  = r.ConfidenceScore;
        OcrUsed          = result.UsedOcrFallback;
        SourceFile       = r.SourceFileName;
    }
}

/// <summary>CsvHelper class map that defines column headers and their output order.</summary>
internal sealed class CsvExportMap : ClassMap<ShipmentCsvRow>
{
    public CsvExportMap()
    {
        Map(m => m.TrackingNumber).Name("Tracking Number").Index(0);
        Map(m => m.HouseBill).Name("House Bill").Index(1);
        Map(m => m.MasterBill).Name("Master Bill").Index(2);
        Map(m => m.Carrier).Name("Carrier").Index(3);
        Map(m => m.ServiceType).Name("Service Type").Index(4);
        Map(m => m.ShipDate).Name("Ship Date").Index(5);
        Map(m => m.EstDeliveryDate).Name("Est. Delivery Date").Index(6);
        Map(m => m.ShipperName).Name("Shipper Name").Index(7);
        Map(m => m.ShipperAddress).Name("Shipper Address").Index(8);
        Map(m => m.ShipperCity).Name("Shipper City").Index(9);
        Map(m => m.ShipperCountry).Name("Shipper Country").Index(10);
        Map(m => m.ShipperPostal).Name("Shipper Postal").Index(11);
        Map(m => m.ConsigneeName).Name("Consignee Name").Index(12);
        Map(m => m.ConsigneeAddress).Name("Consignee Address").Index(13);
        Map(m => m.ConsigneeCity).Name("Consignee City").Index(14);
        Map(m => m.ConsigneeCountry).Name("Consignee Country").Index(15);
        Map(m => m.ConsigneePostal).Name("Consignee Postal").Index(16);
        Map(m => m.Pieces).Name("Pieces").Index(17);
        Map(m => m.GrossWeightKg).Name("Gross Weight (kg)").Index(18);
        Map(m => m.VolumeM3).Name("Volume (m³)").Index(19);
        Map(m => m.Description).Name("Description").Index(20);
        Map(m => m.HsCode).Name("HS Code").Index(21);
        Map(m => m.DeclaredValue).Name("Declared Value").Index(22);
        Map(m => m.Currency).Name("Currency").Index(23);
        Map(m => m.FreightCost).Name("Freight Cost").Index(24);
        Map(m => m.DocumentType).Name("Document Type").Index(25);
        Map(m => m.ConfidenceScore).Name("Confidence Score").Index(26);
        Map(m => m.OcrUsed).Name("OCR Used").Index(27);
        Map(m => m.SourceFile).Name("Source File").Index(28);
    }
}
