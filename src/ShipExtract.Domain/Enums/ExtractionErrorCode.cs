namespace ShipExtract.Domain.Enums;

/// <summary>Identifies the category of error that occurred during document extraction.</summary>
public enum ExtractionErrorCode
{
    /// <summary>Failed to open or read the PDF file.</summary>
    PdfReadFailure,
    /// <summary>OCR engine failed to recognise text from the image.</summary>
    OcrFailure,
    /// <summary>The AI service call failed or returned an error response.</summary>
    AiCallFailure,
    /// <summary>The JSON returned by the AI could not be parsed.</summary>
    JsonParseFailure,
    /// <summary>Extracted data failed domain validation rules.</summary>
    ValidationFailure,
    /// <summary>The file format is not supported by the current configuration.</summary>
    UnsupportedFormat
}
