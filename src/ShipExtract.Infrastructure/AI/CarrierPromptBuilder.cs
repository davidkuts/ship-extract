using ShipExtract.Domain.Enums;

namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Provides carrier-specific extraction hints that are appended to the base AI prompt.
/// Hints extend the base prompt — they do not repeat rules already defined there.
/// </summary>
public static class CarrierPromptBuilder
{
    /// <summary>
    /// Returns carrier-specific field mapping rules to append to the base prompt.
    /// Returns an empty string for <see cref="CarrierType.Unknown"/> and
    /// <see cref="CarrierType.Generic"/> (no additional hints needed).
    /// </summary>
    public static string GetCarrierHints(CarrierType carrier) => carrier switch
    {
        CarrierType.DHL      => DhlHints,
        CarrierType.FedEx    => FedExHints,
        CarrierType.UPS      => UpsHints,
        CarrierType.TNT      => TntHints,
        _                    => string.Empty
    };

    // ── DHL ──────────────────────────────────────────────────────────────────

    private const string DhlHints =
        """
        DHL FIELD RULES:
        - trackingNumber: "Waybill", "AWB No", "DHL Waybill"
        - shipperName: "Shipper", "Sender", "From", "Exporter"
        - consigneeName: "Consignee", "Receiver", "To", "Importer"
        - serviceType: "Product" or "Service" field
        """;

    // ── FedEx ─────────────────────────────────────────────────────────────────

    private const string FedExHints =
        """
        FEDEX FIELD RULES:
        - trackingNumber: labels are "Air Waybill No.", "AWB No.",
          "Tracking No.", "SHP-" prefix values
        - shipperName: look in shipper/from/exporter section.
          "Contact Name:" in the shipper block = the company name.
          Ignore "Tax ID#:" values entirely.
        - consigneeName: extract company name only. Strip any
          phone numbers, email addresses, or Tax ID values.
        - serviceType: "Service Type" or "FedEx Service" field
        """;

    // ── UPS ───────────────────────────────────────────────────────────────────

    private const string UpsHints =
        """
        UPS FIELD RULES:
        - trackingNumber: starts with "1Z" = UPS tracking number.
          Also check "Pro Number", "Shipment Reference".
        - shipperName: look after "Ship From:", "Shipper:", "From:"
        - consigneeName: look after "Ship To:", "Deliver To:", "To:"
        - serviceType: "UPS Service" or "Service Level" field
        """;

    // ── TNT ───────────────────────────────────────────────────────────────────

    private const string TntHints =
        """
        TNT FIELD RULES:
        - trackingNumber: "Consignment No.", "Con Note", "Shipment Number"
        - shipperName: "Sender", "From"
        - consigneeName: "Receiver", "To", "Destination"
        """;
}
