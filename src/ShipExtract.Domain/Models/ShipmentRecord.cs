using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Models;

/// <summary>
/// Represents a single shipment's extracted data fields.
/// All fields are nullable because extraction may yield partial results.
/// </summary>
public sealed class ShipmentRecord
{
    /// <summary>Unique identifier for this record, assigned at creation.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    // ── Shipper ────────────────────────────────────────────────────────────

    /// <summary>Name of the shipping party.</summary>
    public string? ShipperName { get; set; }

    /// <summary>Street address of the shipper.</summary>
    public string? ShipperAddress { get; set; }

    /// <summary>City of the shipper.</summary>
    public string? ShipperCity { get; set; }

    /// <summary>Country of the shipper.</summary>
    public string? ShipperCountry { get; set; }

    /// <summary>Postal / ZIP code of the shipper.</summary>
    public string? ShipperPostalCode { get; set; }

    // ── Consignee ──────────────────────────────────────────────────────────

    /// <summary>Name of the receiving party.</summary>
    public string? ConsigneeName { get; set; }

    /// <summary>Street address of the consignee.</summary>
    public string? ConsigneeAddress { get; set; }

    /// <summary>City of the consignee.</summary>
    public string? ConsigneeCity { get; set; }

    /// <summary>Country of the consignee.</summary>
    public string? ConsigneeCountry { get; set; }

    /// <summary>Postal / ZIP code of the consignee.</summary>
    public string? ConsigneePostalCode { get; set; }

    // ── Shipment ───────────────────────────────────────────────────────────

    /// <summary>Carrier-assigned tracking number.</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>House bill of lading or air waybill number.</summary>
    public string? HouseBillNumber { get; set; }

    /// <summary>Master bill of lading or master air waybill number.</summary>
    public string? MasterBillNumber { get; set; }

    /// <summary>Name of the carrier or freight forwarder.</summary>
    public string? CarrierName { get; set; }

    /// <summary>Service level (e.g. Express, Economy, Standard).</summary>
    public string? ServiceType { get; set; }

    /// <summary>Date the shipment was dispatched.</summary>
    public DateTime? ShipDate { get; set; }

    /// <summary>Carrier's estimated delivery date.</summary>
    public DateTime? EstimatedDeliveryDate { get; set; }

    // ── Cargo ──────────────────────────────────────────────────────────────

    /// <summary>Total number of packages or pieces in the shipment.</summary>
    public int? NumberOfPieces { get; set; }

    /// <summary>Total gross weight of the shipment in kilograms.</summary>
    public decimal? GrossWeightKg { get; set; }

    /// <summary>Total volume of the shipment in cubic metres.</summary>
    public decimal? VolumeM3 { get; set; }

    /// <summary>Plain-language description of the goods.</summary>
    public string? Description { get; set; }

    /// <summary>Harmonised System commodity code.</summary>
    public string? HsCode { get; set; }

    // ── Financial ──────────────────────────────────────────────────────────

    /// <summary>Declared customs value of the shipment.</summary>
    public decimal? DeclaredValue { get; set; }

    /// <summary>ISO 4217 currency code for monetary values (e.g. USD, EUR).</summary>
    public string? Currency { get; set; }

    /// <summary>Freight charge for transporting the shipment.</summary>
    public decimal? FreightCost { get; set; }

    // ── Metadata ───────────────────────────────────────────────────────────

    /// <summary>Original file name from which this record was extracted.</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Classified type of the source document.</summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>AI confidence score for the extraction result, in the range 0.0–1.0.</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>Carrier automatically detected from the document text before AI extraction.</summary>
    public CarrierType DetectedCarrier { get; set; } = CarrierType.Unknown;

    // ── Custom Fields ──────────────────────────────────────────────────────

    /// <summary>User-defined extra fields extracted by the AI based on the current custom-field configuration.</summary>
    public List<CustomFieldValue> CustomFields { get; set; } = new();
}
