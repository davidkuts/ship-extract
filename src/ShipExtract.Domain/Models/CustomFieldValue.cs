namespace ShipExtract.Domain.Models;

/// <summary>
/// The AI-extracted value for one <see cref="CustomField"/> on a single shipment record.
/// </summary>
public sealed class CustomFieldValue
{
    /// <summary>The <see cref="CustomField.Id"/> this value belongs to.</summary>
    public Guid FieldId { get; set; }

    /// <summary>Snapshot of the field name at extraction time (for display without re-joining settings).</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Extracted value, or the field's default value when the AI could not find it.</summary>
    public string Value { get; set; } = string.Empty;
}
