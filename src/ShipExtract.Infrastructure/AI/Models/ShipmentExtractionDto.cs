using System.Text.Json.Serialization;

namespace ShipExtract.Infrastructure.AI.Models;

/// <summary>Internal DTO used to deserialize the AI JSON response for shipment extraction.</summary>
internal sealed class ShipmentExtractionDto
{
    [JsonPropertyName("shipperName")]        public string? ShipperName { get; set; }
    [JsonPropertyName("shipperAddress")]     public string? ShipperAddress { get; set; }
    [JsonPropertyName("shipperCity")]        public string? ShipperCity { get; set; }
    [JsonPropertyName("shipperCountry")]     public string? ShipperCountry { get; set; }
    [JsonPropertyName("shipperPostalCode")]  public string? ShipperPostalCode { get; set; }
    [JsonPropertyName("consigneeName")]      public string? ConsigneeName { get; set; }
    [JsonPropertyName("consigneeAddress")]   public string? ConsigneeAddress { get; set; }
    [JsonPropertyName("consigneeCity")]      public string? ConsigneeCity { get; set; }
    [JsonPropertyName("consigneeCountry")]   public string? ConsigneeCountry { get; set; }
    [JsonPropertyName("consigneePostalCode")]public string? ConsigneePostalCode { get; set; }
    [JsonPropertyName("trackingNumber")]     public string? TrackingNumber { get; set; }
    [JsonPropertyName("houseBillNumber")]    public string? HouseBillNumber { get; set; }
    [JsonPropertyName("masterBillNumber")]   public string? MasterBillNumber { get; set; }
    [JsonPropertyName("carrierName")]        public string? CarrierName { get; set; }
    [JsonPropertyName("serviceType")]        public string? ServiceType { get; set; }
    [JsonPropertyName("shipDate")]           public string? ShipDate { get; set; }
    [JsonPropertyName("estimatedDeliveryDate")] public string? EstimatedDeliveryDate { get; set; }
    [JsonPropertyName("numberOfPieces")]     public int? NumberOfPieces { get; set; }
    [JsonPropertyName("grossWeightKg")]      public decimal? GrossWeightKg { get; set; }
    [JsonPropertyName("volumeM3")]           public decimal? VolumeM3 { get; set; }
    [JsonPropertyName("description")]        public string? Description { get; set; }
    [JsonPropertyName("hsCode")]             public string? HsCode { get; set; }
    [JsonPropertyName("declaredValue")]      public decimal? DeclaredValue { get; set; }
    [JsonPropertyName("currency")]           public string? Currency { get; set; }
    [JsonPropertyName("freightCost")]        public decimal? FreightCost { get; set; }
    [JsonPropertyName("confidenceScore")]    public double? ConfidenceScore { get; set; }
    [JsonPropertyName("documentType")]       public string? DocumentType { get; set; }
}
