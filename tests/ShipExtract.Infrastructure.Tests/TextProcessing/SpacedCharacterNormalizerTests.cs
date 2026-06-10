using FluentAssertions;
using ShipExtract.Infrastructure.TextProcessing;

namespace ShipExtract.Infrastructure.Tests.TextProcessing;

public sealed class SpacedCharacterNormalizerTests
{
    private readonly SpacedCharacterNormalizer _sut = new();

    [Fact]
    public void FixesSpacedUppercase()
    {
        var result = _sut.Process("P K G - 0 0 1");

        result.Should().Be("PKG-001");
    }

    [Fact]
    public void FixesSpacedNumbers()
    {
        var result = _sut.Process("1 2 3 4 5 6 7 8");

        result.Should().Be("12345678");
    }

    [Fact]
    public void PreservesMixedCase()
    {
        var result = _sut.Process("TechParts GmbH");

        result.Should().Be("TechParts GmbH");
    }

    [Fact]
    public void PreservesNormalSentences()
    {
        var result = _sut.Process("Ship to Munich");

        result.Should().Be("Ship to Munich");
    }
}
