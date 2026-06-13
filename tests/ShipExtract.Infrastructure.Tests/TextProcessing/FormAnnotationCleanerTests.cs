using Shouldly;
using ShipExtract.Infrastructure.TextProcessing;

namespace ShipExtract.Infrastructure.Tests.TextProcessing;

public sealed class FormAnnotationCleanerTests
{
    private readonly FormAnnotationCleaner _sut = new();

    [Fact]
    public void RemovesSelectDownArrow()
    {
        const string input = "CONSIGNEE: Tax ID#: GB987654321 SELECT DOWN ARROW FOR OPTIONS";

        var result = _sut.Process(input);

        result.ShouldContain("CONSIGNEE:");
        result.ShouldNotContain("Tax ID");
        result.ShouldNotContain("SELECT DOWN ARROW");
        result.ShouldNotContain("GB987654321");
    }

    [Fact]
    public void RemovesTaxIdNoise()
    {
        const string input =
            "Ship Date: Tax ID#: DE123456789 SELECT DOWN ARROW FOR OPTIONS 2024-06-10";

        var result = _sut.Process(input);

        result.ShouldContain("2024-06-10");
        result.ShouldNotContain("Tax ID");
        result.ShouldNotContain("SELECT DOWN ARROW");
    }

    [Fact]
    public void PreservesRealContent()
    {
        const string input = "TechParts GmbH, Industriestr. 12, Munich";

        var result = _sut.Process(input);

        result.ShouldBe(input);
    }

    [Fact]
    public void CaseInsensitiveMatching()
    {
        const string input = "click here to select an option";

        var result = _sut.Process(input);

        result.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void RemovesCheckboxPhrase()
    {
        const string input = "CHECKBOX Please indicate your preference REQUIRED FIELD";

        var result = _sut.Process(input);

        result.ShouldNotContain("CHECKBOX");
        result.ShouldNotContain("REQUIRED FIELD");
        result.ShouldContain("Please indicate your preference");
    }

    [Fact]
    public void NeverReturnsNull()
    {
        var result = _sut.Process(string.Empty);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
