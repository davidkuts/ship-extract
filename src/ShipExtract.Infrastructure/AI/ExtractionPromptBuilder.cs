using ShipExtract.Domain.Enums;

namespace ShipExtract.Infrastructure.AI;


/// <summary>Builds the system and user prompts sent to the AI extraction service.</summary>
internal static class ExtractionPromptBuilder
{
    /// <summary>Returns the system prompt that instructs the model on its extraction role.</summary>
    public static string BuildSystemPrompt() =>
        """
        You are an expert logistics data extraction assistant. Your role is to extract
        structured shipment information from raw document text. You are trained to handle
        Air Waybills, Bills of Lading, Commercial Invoices,
        Packing Lists, and Courier Labels.

        You must always respond with a single valid JSON object that matches
        the schema provided. Never include explanation, markdown, or any text
        outside the JSON object. If a field cannot be found in the document,
        set it to null. Dates must be ISO 8601 format (YYYY-MM-DD).
        Numeric fields must be numbers, not strings.
        """;

    /// <summary>
    /// Builds the user prompt for a given raw document text and document-type hint.
    /// </summary>
    /// <param name="rawText">The plain text extracted from the document.</param>
    /// <param name="hint">Optional document type hint to improve extraction accuracy.</param>
    /// <param name="carrier">Optional detected carrier to append carrier-specific hints.</param>
    public static string BuildUserPrompt(
        string rawText,
        DocumentType hint,
        CarrierType carrier = CarrierType.Unknown)
    {
        var base_ =
            $$"""
            Document type hint: {{hint}}

            Extract all shipment fields from the following document text.
            Respond only with a JSON object matching this exact schema:

            {
              "shipperName": "string | null",
              "shipperAddress": "string | null",
              "shipperCity": "string | null",
              "shipperCountry": "string | null",
              "shipperPostalCode": "string | null",
              "consigneeName": "string | null",
              "consigneeAddress": "string | null",
              "consigneeCity": "string | null",
              "consigneeCountry": "string | null",
              "consigneePostalCode": "string | null",
              "trackingNumber": "string | null",  // look for: Tracking Number, Tracking #, AWB, AWB No, Air Waybill, Airway Bill, Shipment ID, Consignment Number, Pro Number, Pro #, UPS Tracking Number, Parcel Number, Freight Bill Number
              "houseBillNumber": "string | null",
              "masterBillNumber": "string | null",
              "carrierName": "string | null",
              "serviceType": "string | null",
              "shipDate": "YYYY-MM-DD | null",
              "estimatedDeliveryDate": "YYYY-MM-DD | null",
              "numberOfPieces": "integer | null",
              "grossWeightKg": "number | null",
              "volumeM3": "number | null",
              "description": "string | null",
              "hsCode": "string | null",
              "declaredValue": "number | null",
              "currency": "string | null",
              "freightCost": "number | null",
              "confidenceScore": "number between 0.0 and 1.0",
              "documentType": "Unknown|AirWaybill|BillOfLading|CommercialInvoice|PackingList|CustomsDeclaration|CourierLabel"
            }

            Document text:
            {{rawText}}
            """;

        var hints = CarrierPromptBuilder.GetCarrierHints(carrier);
        return string.IsNullOrEmpty(hints) ? base_ : base_ + "\n\n" + hints;
    }

    /// <summary>
    /// Builds a single combined prompt optimised for Ollama local models.
    /// Includes explicit field-mapping rules to compensate for inconsistent
    /// label recognition in smaller open-weight models.
    /// </summary>
    /// <param name="rawText">The plain text extracted from the document.</param>
    /// <param name="hint">Optional document type hint to improve extraction accuracy.</param>
    /// <param name="carrier">Optional detected carrier to append carrier-specific hints.</param>
    public static string BuildOllamaPrompt(
        string rawText,
        DocumentType hint,
        CarrierType carrier = CarrierType.Unknown)
    {
        var base_ =
            $$"""
            You are a logistics data extractor. Extract shipment fields from the
            document text below and return ONLY a JSON object. No explanation.
            No markdown. No text before or after the JSON.

            CRITICAL FIELD MAPPING RULES:
            - "trackingNumber": Look for labels like: Tracking Number, Tracking #,
              Waybill Number, AWB, AWB No, Air Waybill, Airway Bill,
              Shipment ID, Consignment Number, Consignment No,
              Reference Number, Tracking ID,
              Pro Number, Pro #, PRO No,
              Shipment Reference, Reference Number 1, Reference Number 2,
              UPS Tracking, UPS Tracking Number,
              Parcel Number, Dispatch Number, Freight Bill Number.
              Use the FIRST alphanumeric code found near any of these labels.
              A tracking number is typically 8-22 characters, may contain
              letters and numbers. Reject values that are just label text
              (e.g. reject "Tracking Number", "AWB No.", "Pro Number" as values).
            - "houseBillNumber": Look for: House AWB, HAWB, House Bill, HBL
            - "masterBillNumber": Look for: Master AWB, MAWB, Master Bill, MBL
            - "consigneeName": Look for: Consignee, Recipient, Deliver To,
              Ship To (name line), Destination Contact
            - "consigneeAddress": Look for: address lines below Consignee/Recipient label
            - "shipperName": Look for: Shipper, Sender, From, Origin Contact
            - If a field truly cannot be found, use null. Do not guess.
            - IMPORTANT: If a field value looks like a placeholder label in ALL CAPS
              (e.g. "CONSIGNEE", "SHIPPER", "EXPORTER", "IMPORTER", "RECIPIENT",
              "ADDRESS", "CITY", "COUNTRY") then it is a template label, NOT a real
              value. Set that field to null instead.
            - A real value contains actual names, numbers, or addresses —
              not generic category words in all caps.

            Document type hint: {{hint}}

            Return this exact JSON structure:
            {
              "trackingNumber": "string or null",
              "houseBillNumber": "string or null",
              "masterBillNumber": "string or null",
              "shipperName": "string or null",
              "shipperAddress": "string or null",
              "shipperCity": "string or null",
              "shipperCountry": "string or null",
              "shipperPostalCode": "string or null",
              "consigneeName": "string or null",
              "consigneeAddress": "string or null",
              "consigneeCity": "string or null",
              "consigneeCountry": "string or null",
              "consigneePostalCode": "string or null",
              "carrierName": "string or null",
              "serviceType": "string or null",
              "shipDate": "YYYY-MM-DD or null",
              "estimatedDeliveryDate": "YYYY-MM-DD or null",
              "numberOfPieces": "integer or null",
              "grossWeightKg": "number or null",
              "volumeM3": "number or null",
              "description": "string or null",
              "hsCode": "string or null",
              "declaredValue": "number or null",
              "currency": "string or null",
              "freightCost": "number or null",
              "confidenceScore": "number between 0.0 and 1.0",
              "documentType": "AirWaybill or BillOfLading or CommercialInvoice or PackingList or CustomsDeclaration or CourierLabel or Unknown"
            }

            Document text:
            {{rawText}}
            """;

        var hints = CarrierPromptBuilder.GetCarrierHints(carrier);
        return string.IsNullOrEmpty(hints) ? base_ : base_ + "\n\n" + hints;
    }
}
