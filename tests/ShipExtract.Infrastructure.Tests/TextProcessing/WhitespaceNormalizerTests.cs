using Shouldly;
using ShipExtract.Infrastructure.TextProcessing;

namespace ShipExtract.Infrastructure.Tests.TextProcessing;

public sealed class WhitespaceNormalizerTests
{
    private readonly WhitespaceNormalizer _sut = new();

    [Fact]
    public void RemovesNullCharacters()
    {
        var input  = "hello\0world";
        var result = _sut.Process(input);

        result.ShouldNotContain("\0");
        result.ShouldContain("hello");
        result.ShouldContain("world");
    }

    [Fact]
    public void NormalizesNonBreakingSpaces()
    {
        var input  = "hello\u00A0world";
        var result = _sut.Process(input);

        result.ShouldNotContain("\u00A0");
        result.ShouldBe("hello world");
    }

    [Fact]
    public void CollapsesExcessiveBlankLines()
    {
        const string input    = "line1\n\n\n\n\nline2";
        const string expected = "line1\n\nline2";

        var result = _sut.Process(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void TrimsLines()
    {
        const string input = "  hello  \n  world  ";

        var result = _sut.Process(input);

        result.ShouldBe("hello\nworld");
    }

    [Fact]
    public void NormalizesWindowsLineEndings()
    {
        const string input = "line1\r\nline2\rline3";

        var result = _sut.Process(input);

        result.ShouldNotContain("\r");
        result.ShouldBe("line1\nline2\nline3");
    }

    [Fact]
    public void NeverReturnsNull()
    {
        var result = _sut.Process(string.Empty);
        result.ShouldNotBeNull();
    }
}
