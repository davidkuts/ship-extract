using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Removes PDF operator sequences and rendering artifacts that PdfPig
/// occasionally leaks into extracted text for certain PDF types.
/// </summary>
public sealed class PdfControlSequenceRemover : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "PdfControlSequenceRemover";

    // PDF text operators: only match when not adjacent to other letters
    // (prevents removing "BT" from "DEBT", "Td" from "standard", etc.).
    private static readonly Regex PdfOperators = new(
        @"(?<![A-Za-z])(BT|ET|Tf|Td|TD|Tm|T\*|Tj|TJ|Tw|Tc|Ts)(?![A-Za-z])",
        RegexOptions.Compiled);

    // Hex-color / coordinate lines: e.g. "0.5 0.5 0.5 rg" or "1 0 0 1 72 720 cm".
    private static readonly Regex CoordinateLines = new(
        @"^\s*[\d\.\s]+(rg|RG|cm|re|w|q|Q)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Font name references: "/F1 12 Tf" or "/ABCDEF+Arial-Bold 10 Tf".
    private static readonly Regex FontNames = new(
        @"\/(F\d+|[A-Z]{2,6}\+[A-Za-z\-]+)\s+\d+\s+Tf",
        RegexOptions.Compiled);

    // Standalone PDF path/painting operators on their own line (q Q f F n S s B b W).
    private static readonly Regex StandaloneOperators = new(
        @"^\s*[qQfFnSsBbW]{1,2}\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            var text = rawText;

            text = StandaloneOperators.Replace(text, string.Empty);
            text = CoordinateLines.Replace(text, string.Empty);
            text = FontNames.Replace(text, string.Empty);
            text = PdfOperators.Replace(text, string.Empty);

            return text;
        }
        catch
        {
            return rawText;
        }
    }
}
