using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Fixes the common PDF artifact where individual characters are separated
/// by single spaces, producing strings like "P K G - 0 0 1" or "I N V O I C E".
/// Only collapses ALL-CAPS/numeric sequences — mixed-case text is never touched.
/// </summary>
public sealed class SpacedCharacterNormalizer : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "SpacedCharacterNormalizer";

    // Matches sequences of single uppercase letters, digits, or hyphens alternating
    // with single spaces for at least 4 characters total (e.g. "A B C D", "1 2 3 4",
    // "P K G - 0 0 1"). The repeating group includes [-] so hyphens within a spaced
    // token are collapsed correctly. The sequence must end with an alphanumeric char
    // (not a hyphen) to avoid false matches on punctuation runs.
    // \b anchors prevent partial matches inside normal mixed-case words.
    private static readonly Regex SpacedChars = new(
        @"\b([A-Z0-9\-] ){3,}[A-Z0-9]\b",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            return SpacedChars.Replace(rawText, m => m.Value.Replace(" ", string.Empty));
        }
        catch
        {
            return rawText;
        }
    }
}
