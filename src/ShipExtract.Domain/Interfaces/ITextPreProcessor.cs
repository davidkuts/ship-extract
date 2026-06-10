namespace ShipExtract.Domain.Interfaces;

/// <summary>
/// Cleans raw text extracted from a PDF before AI processing.
/// Implementations may be chained via the pipeline pattern.
/// </summary>
public interface ITextPreProcessor
{
    /// <summary>
    /// Process the raw text and return cleaned text.
    /// Must never return null — return string.Empty if nothing survives.
    /// </summary>
    string Process(string rawText);

    /// <summary>Human-readable name for logging (e.g. "FormAnnotationCleaner").</summary>
    string Name { get; }
}
