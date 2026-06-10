using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Removes PDF fillable-form artifacts from extracted text.
/// These appear because PdfPig emits form field labels and UI
/// instructions alongside real document values.
/// </summary>
public sealed class FormAnnotationCleaner : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "FormAnnotationCleaner";

    // ── Exact phrase patterns ────────────────────────────────────────────────

    private static readonly string[] ExactPhraseList =
    [
        "SELECT DOWN ARROW FOR OPTIONS",
        "CLICK HERE TO SELECT",
        "CLICK TO SELECT",
        "SELECT FROM LIST",
        "DROP DOWN",
        "CHECKBOX",
        "CHECK BOX",
        "RADIO BUTTON",
        "REQUIRED FIELD",
        "OPTIONAL FIELD",
        "TYPE HERE",
        "ENTER HERE",
        "ENTER VALUE",
        "CLICK HERE",
        "FORM FIELD",
        "FILL IN",
        "FILL HERE",
        "N/A IF NOT APPLICABLE",
    ];

    // Build a single alternation regex from the phrase list (longest first to
    // prevent shorter phrases shadowing longer ones).
    private static readonly Regex ExactPhrases = new(
        string.Join("|", ExactPhraseList
            .OrderByDescending(p => p.Length)
            .Select(Regex.Escape)),
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Pattern-based removals ───────────────────────────────────────────────

    // Matches "Tax ID:" / "Tax ID #:" / "Tax ID#:" followed by an identifier.
    private static readonly Regex TaxId = new(
        @"Tax\s+ID\s*#?\s*:\s*[A-Z0-9\-]{0,20}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches entire lines that are UI instructions (select/click/enter/…).
    private static readonly Regex FormInstructionLines = new(
        @"^\s*(Please\s+)?(select|choose|pick|click|check|enter|type|fill)" +
        @"\s+(one|from|here|above|below|an?\s+option)[\s\w]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    // Matches "Company Name/Address:" with nothing but whitespace after it on the line.
    private static readonly Regex CompanyNameAddress = new(
        @"Company Name/Address:\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Collapses runs of horizontal whitespace to a single space.
    private static readonly Regex MultipleSpaces = new(
        @"[ \t]{2,}",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            var text = rawText;

            // 1. Remove entire instruction lines first (before partial-phrase removal
            //    corrupts them).
            text = FormInstructionLines.Replace(text, string.Empty);

            // 2. Remove exact annotation phrases.
            text = ExactPhrases.Replace(text, " ");

            // 3. Remove Tax ID label + value.
            text = TaxId.Replace(text, string.Empty);

            // 4. Remove "Company Name/Address:" with no content.
            text = CompanyNameAddress.Replace(text, string.Empty);

            // 5. Collapse runs of spaces/tabs introduced by the removals.
            text = MultipleSpaces.Replace(text, " ");

            return text;
        }
        catch
        {
            return rawText;
        }
    }
}
