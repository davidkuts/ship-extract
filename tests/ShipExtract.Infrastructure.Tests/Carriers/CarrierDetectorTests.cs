using FluentAssertions;
using ShipExtract.Domain.Enums;
using ShipExtract.Infrastructure.Carriers;

namespace ShipExtract.Infrastructure.Tests.Carriers;

public sealed class CarrierDetectorTests
{
    private readonly CarrierDetector _sut = new();

    [Fact]
    public void DetectsDHL_FromWaybillText()
    {
        var result = _sut.Detect("DHL Express Waybill No. 1234567890");

        result.Should().Be(CarrierType.DHL);
    }

    [Fact]
    public void DetectsFedEx_FromAWBLabel()
    {
        var result = _sut.Detect("Air Waybill No. / Tracking No.: SHP-2024-06-A");

        result.Should().Be(CarrierType.FedEx);
    }

    [Fact]
    public void DetectsUPS_From1ZPrefix()
    {
        var result = _sut.Detect("Tracking: 1Z999AA10123456784 UPS Supply Chain");

        result.Should().Be(CarrierType.UPS);
    }

    [Fact]
    public void DetectsUPS_FromCompanyName()
    {
        var result = _sut.Detect("UPS Supply Chain Solutions\nShip From:");

        result.Should().Be(CarrierType.UPS);
    }

    [Fact]
    public void ReturnsUnknown_ForGenericText()
    {
        var result = _sut.Detect("Invoice Number: 12345\nShipper: Acme Corp");

        result.Should().Be(CarrierType.Unknown);
    }

    [Fact]
    public void CaseInsensitive_FedEx()
    {
        var result = _sut.Detect("FEDEX INTERNATIONAL PRIORITY");

        result.Should().Be(CarrierType.FedEx);
    }

    [Fact]
    public void DHL_TakesPriorityOver_Generic()
    {
        var result = _sut.Detect("DHL Waybill - shipper details below");

        result.Should().Be(CarrierType.DHL);
    }
}
