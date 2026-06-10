using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.TextProcessing;

/// <summary>
/// Runs a chain of <see cref="ITextPreProcessor"/> instances in registration order
/// and produces a <see cref="PreProcessingReport"/> describing the changes made.
/// </summary>
public sealed class TextPreProcessingPipeline : ITextPreProcessingPipeline
{
    private readonly IReadOnlyList<ITextPreProcessor> _processors;
    private readonly ILoggingService _logger;

    /// <summary>Initialises a new instance of <see cref="TextPreProcessingPipeline"/>.</summary>
    public TextPreProcessingPipeline(
        IEnumerable<ITextPreProcessor> processors,
        ILoggingService logger)
    {
        _processors = processors.ToList();
        _logger     = logger;
    }

    /// <inheritdoc/>
    public (string CleanedText, PreProcessingReport Report) Process(string rawText)
    {
        var steps   = new List<string>();
        var current = rawText;

        foreach (var processor in _processors)
        {
            var before = current.Length;
            try
            {
                current = processor.Process(current);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "PreProcessor {Name} threw an exception — skipping. Error: {Error}",
                    processor.Name, ex.Message);
                continue;
            }

            var removed = before - current.Length;
            if (removed > 0)
                steps.Add($"{processor.Name}: removed {removed} chars");

            _logger.LogDebug(
                "PreProcessor {Name}: {Before}→{After} chars ({Removed} removed)",
                processor.Name, before, current.Length, removed);
        }

        var report = new PreProcessingReport
        {
            OriginalCharacterCount = rawText.Length,
            CleanedCharacterCount  = current.Length,
            StepsApplied           = steps,
            CleanedText            = current.Length != rawText.Length ? current : null
        };

        if (report.ReductionPercent > 5)
            _logger.LogInformation(
                "Pre-processing reduced text by {Pct:F1}% ({Chars} chars removed)",
                report.ReductionPercent, report.CharactersRemoved);

        return (current, report);
    }
}
