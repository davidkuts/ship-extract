namespace ShipExtract.Domain.Enums;

/// <summary>Classifies the type of shipping document being processed.</summary>
public enum DocumentType
{
    /// <summary>Document type could not be determined.</summary>
    Unknown,
    /// <summary>Air waybill document for air freight shipments.</summary>
    AirWaybill,
    /// <summary>Bill of lading for sea freight shipments.</summary>
    BillOfLading,
    /// <summary>Commercial invoice listing goods and their value.</summary>
    CommercialInvoice,
    /// <summary>Packing list detailing shipment contents.</summary>
    PackingList,
    /// <summary>Customs declaration form for cross-border shipments.</summary>
    CustomsDeclaration,
    /// <summary>Courier shipping label.</summary>
    CourierLabel
}
