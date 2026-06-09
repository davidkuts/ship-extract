using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Validators;

/// <summary>Holds the outcome of validating a <see cref="ShipmentRecord"/>.</summary>
public sealed class ValidationResult
{
    /// <summary>Gets a value indicating whether the record passed all validation rules.</summary>
    public bool IsValid => !Errors.Any();

    /// <summary>List of human-readable error messages describing each validation failure.</summary>
    public List<string> Errors { get; } = [];
}

/// <summary>Validates a <see cref="ShipmentRecord"/> against the domain business rules.</summary>
public static class ShipmentRecordValidator
{
    /// <summary>
    /// Validates the supplied <paramref name="record"/> and returns a <see cref="ValidationResult"/>
    /// that describes any rule violations found.
    /// </summary>
    /// <param name="record">The shipment record to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> containing all validation errors, if any.</returns>
    public static ValidationResult Validate(ShipmentRecord record)
    {
        var result = new ValidationResult();

        // At least one bill/tracking number must be present
        if (string.IsNullOrWhiteSpace(record.TrackingNumber) &&
            string.IsNullOrWhiteSpace(record.HouseBillNumber) &&
            string.IsNullOrWhiteSpace(record.MasterBillNumber))
        {
            result.Errors.Add("At least one of TrackingNumber, HouseBillNumber, or MasterBillNumber must be provided.");
        }

        // Consignee identity must be present
        if (string.IsNullOrWhiteSpace(record.ConsigneeName) &&
            string.IsNullOrWhiteSpace(record.ConsigneeAddress))
        {
            result.Errors.Add("ConsigneeName or ConsigneeAddress must be provided.");
        }

        // Weight must be positive when supplied
        if (record.GrossWeightKg.HasValue && record.GrossWeightKg.Value <= 0)
        {
            result.Errors.Add("GrossWeightKg must be greater than zero when specified.");
        }

        // Declared value must be non-negative when supplied
        if (record.DeclaredValue.HasValue && record.DeclaredValue.Value < 0)
        {
            result.Errors.Add("DeclaredValue must be greater than or equal to zero when specified.");
        }

        // Delivery date must not precede ship date
        if (record.ShipDate.HasValue && record.EstimatedDeliveryDate.HasValue &&
            record.EstimatedDeliveryDate.Value < record.ShipDate.Value)
        {
            result.Errors.Add("EstimatedDeliveryDate must be on or after ShipDate.");
        }

        // Confidence score must be in valid range
        if (record.ConfidenceScore < 0.0 || record.ConfidenceScore > 1.0)
        {
            result.Errors.Add($"ConfidenceScore must be between 0.0 and 1.0 (was {record.ConfidenceScore}).");
        }

        return result;
    }
}
