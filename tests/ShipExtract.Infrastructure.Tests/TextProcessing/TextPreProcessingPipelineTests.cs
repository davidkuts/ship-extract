using Shouldly;
using Moq;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.TextProcessing;

namespace ShipExtract.Infrastructure.Tests.TextProcessing;

public sealed class TextPreProcessingPipelineTests
{
    private static ILoggingService NullLogger()
    {
        var mock = new Mock<ILoggingService>();
        return mock.Object;
    }

    [Fact]
    public void AllProcessorsRunInOrder()
    {
        // Arrange — three processors chained: original → "step1" → "step2" → "step3"
        var proc1 = new Mock<ITextPreProcessor>();
        var proc2 = new Mock<ITextPreProcessor>();
        var proc3 = new Mock<ITextPreProcessor>();

        proc1.Setup(p => p.Name).Returns("P1");
        proc2.Setup(p => p.Name).Returns("P2");
        proc3.Setup(p => p.Name).Returns("P3");

        proc1.Setup(p => p.Process("original")).Returns("step1");
        proc2.Setup(p => p.Process("step1")).Returns("step2");
        proc3.Setup(p => p.Process("step2")).Returns("step3");

        var pipeline = new TextPreProcessingPipeline(
            [proc1.Object, proc2.Object, proc3.Object],
            NullLogger());

        // Act
        var (cleaned, _) = pipeline.Process("original");

        // Assert — each processor receives the OUTPUT of the previous one
        proc1.Verify(p => p.Process("original"), Times.Once);
        proc2.Verify(p => p.Process("step1"),    Times.Once);
        proc3.Verify(p => p.Process("step2"),    Times.Once);
        cleaned.ShouldBe("step3");
    }

    [Fact]
    public void ReportReflectsActualReduction()
    {
        // Arrange — use real processors on noisy FedEx-like text
        var processors = new ITextPreProcessor[]
        {
            new FormAnnotationCleaner(),
            new WhitespaceNormalizer()
        };
        var pipeline = new TextPreProcessingPipeline(processors, NullLogger());
        const string input =
            "CONSIGNEE: Tax ID#: GB987654321 SELECT DOWN ARROW FOR OPTIONS\n" +
            "Real company data\n" +
            "CLICK HERE TO SELECT\n" +
            "Acme Corporation";

        // Act
        var (_, report) = pipeline.Process(input);

        // Assert
        report.CharactersRemoved.ShouldBeGreaterThan(0);
        report.ReductionPercent.ShouldBeGreaterThan(0);
        report.StepsApplied.ShouldNotBeEmpty();
    }

    [Fact]
    public void NeverReturnsNull()
    {
        // Arrange — null characters are valid input edge case
        var pipeline = new TextPreProcessingPipeline(
            [new WhitespaceNormalizer()],
            NullLogger());
        var input = new string('\0', 100);

        // Act
        var (cleaned, report) = pipeline.Process(input);

        // Assert
        cleaned.ShouldNotBeNull();
        report.ShouldNotBeNull();
        report.OriginalCharacterCount.ShouldBe(100);
    }

    [Fact]
    public void ReportCleanedTextIsNullWhenNothingRemoved()
    {
        var proc = new Mock<ITextPreProcessor>();
        proc.Setup(p => p.Name).Returns("Identity");
        proc.Setup(p => p.Process(It.IsAny<string>())).Returns<string>(s => s);

        var pipeline = new TextPreProcessingPipeline([proc.Object], NullLogger());

        var (_, report) = pipeline.Process("unchanged text");

        report.CleanedText.ShouldBeNull("CleanedText should be null when no chars were removed");
        report.CharactersRemoved.ShouldBe(0);
    }

    [Fact]
    public void ReportCleanedTextIsSetWhenCharsRemoved()
    {
        var pipeline = new TextPreProcessingPipeline(
            [new FormAnnotationCleaner()],
            NullLogger());

        var (cleaned, report) = pipeline.Process("data SELECT DOWN ARROW FOR OPTIONS more data");

        report.CleanedText.ShouldNotBeNull();
        report.CleanedText.ShouldBe(cleaned);
        report.CharactersRemoved.ShouldBeGreaterThan(0);
    }
}
