using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Best-effort heuristic that adds a colon+space between a field label and its
/// value when the PDF extractor ran them together without separation.
/// When in doubt, the text is left unchanged to avoid corrupting real content.
/// </summary>
public sealed class LabelValueSeparator : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "LabelValueSeparator";

    // Rule 1: ALL-CAPS block (3+ chars) immediately followed (no space) by a
    // mixed-case word or digit — e.g. "SHIPPERTechParts" → "SHIPPER: TechParts".
    // The negative lookbehind ensures the caps block is not the tail of a longer word.
    private static readonly Regex AllCapsMerge = new(
        @"(?<![A-Za-z])([A-Z]{3,})([A-Z][a-z]|\d)",
        RegexOptions.Compiled);

    // Rule 2: Known shipping-document label words at the start of a line,
    // not already followed by a colon.
    private static readonly string[] KnownLabels =
    [
        "SHIPPER", "CONSIGNEE", "EXPORTER", "IMPORTER", "RECIPIENT",
        "CARRIER", "DESCRIPTION", "CURRENCY", "WEIGHT", "PIECES",
        "QUANTITY", "INVOICE", "REFERENCE", "TRACKING", "WAYBILL",
    ];

    // Matches a known label at line-start (with optional indentation),
    // not already followed (with optional spaces) by a colon.
    private static readonly Regex KnownLabelsMissingColon = new(
        @"(?m)^[ \t]*(" + string.Join("|", KnownLabels) + @")(?!\s*:)[ \t]+",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            var text = rawText;

            // Rule 2 first: add colon after known labels at line-start.
            text = KnownLabelsMissingColon.Replace(text, "$1: ");

            // Rule 1: separate merged ALL-CAPS label from mixed-case/digit value.
            text = AllCapsMerge.Replace(text, "$1: $2");

            return text;
        }
        catch
        {
            return rawText;
        }
    }
}
