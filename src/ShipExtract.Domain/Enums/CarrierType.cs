namespace ShipExtract.Domain.Enums;

/// <summary>Identifies the logistics carrier or freight forwarder detected in a document.</summary>
public enum CarrierType
{
    Unknown,
    DHL,
    FedEx,
    UPS,
    TNT,
    DPD,
    GLS,
    Schenker,
    Kuehne,      // Kuehne+Nagel
    Panalpina,
    Generic      // Detected as a carrier but not a known one
}
