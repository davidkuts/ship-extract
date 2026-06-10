using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Normalizes whitespace in PDF-extracted text: removes null characters,
/// unifies line endings, collapses excessive blank lines and spaces,
/// and trims each line.
/// </summary>
public sealed class WhitespaceNormalizer : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "WhitespaceNormalizer";

    // Non-breaking space and other Unicode space variants.
    private static readonly Regex UnicodeSpaces = new(
        @"[\u00A0\u2000-\u200B\u202F\u3000]",
        RegexOptions.Compiled);

    // Three or more consecutive blank lines → two blank lines.
    private static readonly Regex ExcessiveBlankLines = new(
        @"\n{3,}",
        RegexOptions.Compiled);

    // Three or more consecutive spaces on a single line → two spaces.
    private static readonly Regex ExcessiveSpaces = new(
        @"[ \t]{3,}",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            var text = rawText;

            // 1. Replace null characters with space.
            text = text.Replace('\0', ' ');

            // 2. Replace non-breaking spaces and Unicode space variants.
            text = UnicodeSpaces.Replace(text, " ");

            // 3. Normalize line endings to \n.
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            // 4. Collapse 3+ consecutive blank lines to 2.
            text = ExcessiveBlankLines.Replace(text, "\n\n");

            // 5. Collapse 3+ consecutive spaces/tabs to 2.
            text = ExcessiveSpaces.Replace(text, "  ");

            // 6. Trim each line.
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                lines[i] = lines[i].Trim();
            text = string.Join('\n', lines);

            // 7. Trim the whole result.
            return text.Trim();
        }
        catch
        {
            return rawText;
        }
    }
}
