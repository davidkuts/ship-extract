using Shouldly;
using ShipExtract.Infrastructure.TextProcessing;

namespace ShipExtract.Infrastructure.Tests.TextProcessing;

public sealed class DuplicateLineRemoverTests
{
    private readonly DuplicateLineRemover _sut = new();

    [Fact]
    public void RemovesLineAppearing3Times()
    {
        const string input = "Header\ndata\nHeader\ndata2\nHeader\ndata3";

        var result = _sut.Process(input);

        var lines = result.Split('\n');
        lines.Count(l => l.Trim().Equals("Header", StringComparison.OrdinalIgnoreCase))
             .ShouldBe(1);
    }

    [Fact]
    public void PreservesLineAppearing2Times()
    {
        const string input = "line\nother\nline";

        var result = _sut.Process(input);

        var lines = result.Split('\n');
        lines.Count(l => l.Trim().Equals("line", StringComparison.OrdinalIgnoreCase))
             .ShouldBe(2);
    }

    [Fact]
    public void PreservesNumericLines()
    {
        const string input = "100\ndata\n100\ndata2\n100\ndata3";

        var result = _sut.Process(input);

        var lines = result.Split('\n');
        lines.Count(l => l.Trim() == "100")
             .ShouldBe(3, "numeric-only lines must never be removed");
    }

    [Fact]
    public void PreservesTrivialLines()
    {
        // Lines shorter than 4 chars are "trivial" and never counted or removed.
        const string input = "ok\ndata\nok\ndata2\nok\ndata3";

        var result = _sut.Process(input);

        var lines = result.Split('\n');
        lines.Count(l => l.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase))
             .ShouldBe(3, "trivial lines (< 4 chars) should never be removed");
    }

    [Fact]
    public void NeverReturnsNull()
    {
        var result = _sut.Process(string.Empty);
        result.ShouldNotBeNull();
    }
}
