using FluentAssertions;
using ShipExtract.Domain.Enums;
using ShipExtract.Infrastructure.AI;

namespace ShipExtract.Infrastructure.Tests.AI.Prompts;

public sealed class CarrierPromptBuilderTests
{
    [Fact]
    public void DHLHints_ContainWaybillKeyword()
    {
        var hints = CarrierPromptBuilder.GetCarrierHints(CarrierType.DHL);

        hints.Should().Contain("Waybill");
    }

    [Fact]
    public void FedExHints_ContainContactNameRule()
    {
        var hints = CarrierPromptBuilder.GetCarrierHints(CarrierType.FedEx);

        hints.Should().Contain("Contact Name");
    }

    [Fact]
    public void UPSHints_Contain1ZRule()
    {
        var hints = CarrierPromptBuilder.GetCarrierHints(CarrierType.UPS);

        hints.Should().Contain("1Z");
    }

    [Fact]
    public void UnknownCarrier_ReturnsEmptyString()
    {
        var hints = CarrierPromptBuilder.GetCarrierHints(CarrierType.Unknown);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void GenericCarrier_ReturnsEmptyString()
    {
        var hints = CarrierPromptBuilder.GetCarrierHints(CarrierType.Generic);

        hints.Should().BeEmpty();
    }
}
