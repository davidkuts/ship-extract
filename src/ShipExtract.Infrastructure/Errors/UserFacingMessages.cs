using ShipExtract.Domain.Enums;

namespace ShipExtract.Infrastructure.Errors;

/// <summary>
/// Maps <see cref="ExtractionErrorCode"/> values to actionable, human-readable messages
/// suitable for display in the UI without exposing internal technical detail.
/// </summary>
public static class UserFacingMessages
{
    /// <summary>Returns an actionable message for the given <paramref name="code"/>.</summary>
    public static string GetMessage(ExtractionErrorCode code) => code switch
    {
        ExtractionErrorCode.PasswordProtected =>
            "This PDF is password-protected. Remove the password and try again.",

        ExtractionErrorCode.CorruptFile =>
            "This file appears corrupt or is not a valid PDF. Try re-saving or re-downloading the document.",

        ExtractionErrorCode.EmptyFile =>
            "The file is empty (0 bytes). Check that the correct file was added.",

        ExtractionErrorCode.PdfReadFailure =>
            "The PDF could not be read. It may be damaged, truncated, or in an unsupported format.",

        ExtractionErrorCode.OcrFailure =>
            "Text recognition (OCR) failed. Ensure Tesseract language files are installed in Settings.",

        ExtractionErrorCode.AiCallFailure =>
            "The AI extraction service could not be reached. Check your API key and network connection.",

        ExtractionErrorCode.JsonParseFailure =>
            "The AI returned an unexpected response. Try again — if the problem persists, switch AI model.",

        ExtractionErrorCode.ValidationFailure =>
            "Some extracted fields failed validation. Review the result for accuracy.",

        ExtractionErrorCode.UnsupportedFormat =>
            "This file type is not supported. Only PDF files can be processed.",

        _ =>
            "An unexpected error occurred. Check the application log for details."
    };
}
