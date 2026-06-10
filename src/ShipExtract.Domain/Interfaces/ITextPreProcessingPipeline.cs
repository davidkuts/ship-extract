using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Interfaces;

/// <summary>
/// Orchestrates a chain of <see cref="ITextPreProcessor"/> instances and returns
/// both the cleaned text and a report of what was removed.
/// </summary>
public interface ITextPreProcessingPipeline
{
    /// <summary>
    /// Runs all registered pre-processors in order and returns the cleaned text
    /// together with a <see cref="PreProcessingReport"/> describing changes made.
    /// </summary>
    (string CleanedText, PreProcessingReport Report) Process(string rawText);
}
