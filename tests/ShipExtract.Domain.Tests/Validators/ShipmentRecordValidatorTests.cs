using Shouldly;
using ShipExtract.Domain.Models;
using ShipExtract.Domain.Validators;

namespace ShipExtract.Domain.Tests.Validators;

/// <summary>Unit tests for <see cref="ShipmentRecordValidator"/>.</summary>
public sealed class ShipmentRecordValidatorTests
{
    private static ShipmentRecord CreateValidRecord() => new()
    {
        TrackingNumber  = "1Z999AA10123456784",
        ConsigneeName   = "Acme Corp",
        ConfidenceScore = 0.95
    };

    [Fact]
    public void Validate_ValidMinimalRecord_ReturnsIsValidTrue()
    {
        var record = CreateValidRecord();

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AllBillNumbersNullOrEmpty_AddsWarningButRemainsValid()
    {
        var record = CreateValidRecord();
        record.TrackingNumber   = null;
        record.HouseBillNumber  = null;
        record.MasterBillNumber = null;

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.HasWarnings.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("TrackingNumber"));
    }

    [Fact]
    public void Validate_GrossWeightKgIsZero_ReturnsValidationError()
    {
        var record = CreateValidRecord();
        record.GrossWeightKg = 0m;

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("GrossWeightKg"));
    }

    [Fact]
    public void Validate_DeclaredValueIsNegative_ReturnsValidationError()
    {
        var record = CreateValidRecord();
        record.DeclaredValue = -1m;

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("DeclaredValue"));
    }

    [Fact]
    public void Validate_EstimatedDeliveryDateBeforeShipDate_ReturnsValidationError()
    {
        var record = CreateValidRecord();
        record.ShipDate               = new DateTime(2024, 6, 10);
        record.EstimatedDeliveryDate  = new DateTime(2024, 6, 5);

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("EstimatedDeliveryDate"));
    }

    [Fact]
    public void Validate_ConfidenceScoreAboveOne_ReturnsValidationError()
    {
        var record = CreateValidRecord();
        record.ConfidenceScore = 1.5;

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("ConfidenceScore"));
    }

    [Fact]
    public void Validate_ConfidenceScoreIsZero_IsValid()
    {
        var record = CreateValidRecord();
        record.ConfidenceScore = 0.0;

        var result = ShipmentRecordValidator.Validate(record);

        result.IsValid.ShouldBeTrue();
    }
}
