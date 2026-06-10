namespace ShipExtract.Domain.Models;

/// <summary>
/// Records what was removed during pre-processing — used for
/// debug logging and the Raw Text preview panel.
/// </summary>
public class PreProcessingReport
{
    /// <summary>Character count of the original text before any cleaning.</summary>
    public int OriginalCharacterCount { get; init; }

    /// <summary>Character count of the text after all pre-processors ran.</summary>
    public int CleanedCharacterCount { get; init; }

    /// <summary>Total characters removed across all pre-processing steps.</summary>
    public int CharactersRemoved => OriginalCharacterCount - CleanedCharacterCount;

    /// <summary>Fraction of the original text that was removed, expressed as a percentage.</summary>
    public double ReductionPercent =>
        OriginalCharacterCount == 0 ? 0 :
        (double)CharactersRemoved / OriginalCharacterCount * 100;

    /// <summary>
    /// Human-readable description of each step that removed at least one character.
    /// </summary>
    public List<string> StepsApplied { get; init; } = new();

    /// <summary>
    /// The cleaned text after all pre-processors ran.
    /// <see langword="null"/> when no characters were removed (identical to the original).
    /// </summary>
    public string? CleanedText { get; init; }
}
