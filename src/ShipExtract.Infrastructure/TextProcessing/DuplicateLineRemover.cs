using System.Text.RegularExpressions;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Removes lines that repeat three or more times in the document.
/// These are typically page headers or footers repeated by the PDF extractor.
/// Numeric-only lines are never removed (they may be legitimate repeated values).
/// </summary>
public sealed class DuplicateLineRemover : ITextPreProcessor
{
    /// <inheritdoc/>
    public string Name => "DuplicateLineRemover";

    private static readonly Regex NumericOnly = new(
        @"^\s*[\d\s\.\,]+\s*$",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public string Process(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;

        try
        {
            var lines = rawText.Split('\n');

            // Count occurrences of each non-trivial, non-numeric line.
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length >= 4 && !NumericOnly.IsMatch(trimmed))
                    counts[trimmed] = counts.GetValueOrDefault(trimmed, 0) + 1;
            }

            // Reconstruct: for lines repeated 3+ times keep only the first occurrence.
            var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                bool isFrequent = trimmed.Length >= 4
                    && !NumericOnly.IsMatch(trimmed)
                    && counts.GetValueOrDefault(trimmed, 0) >= 3;

                if (!isFrequent)
                {
                    result.Add(line);
                }
                else if (seen.Add(trimmed))
                {
                    result.Add(line); // keep first occurrence only
                }
                // subsequent occurrences of a frequent line are dropped
            }

            return string.Join('\n', result);
        }
        catch
        {
            return rawText;
        }
    }
}
