namespace ShipExtract.Domain.Models;

/// <summary>
/// A user-defined extra field that the AI should attempt to extract from each document.
/// </summary>
public sealed class CustomField
{
    /// <summary>Stable identifier for this field (persisted in settings).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name and export column header (e.g. "PO Number").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hint sent to the AI describing where/how to find the value (e.g. "Purchase order number, often labelled PO# or PO Number").</summary>
    public string ExtractionHint { get; set; } = string.Empty;

    /// <summary>Value exported when the AI cannot find the field. Defaults to empty string.</summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>Whether this field is included in extraction prompts.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>0-based display order in the editor UI and export columns.</summary>
    public int SortOrder { get; set; }
}
