using FluentAssertions;
using Moq;
using ShipExtract.Application.Pipelines;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Tests.Pipelines;

/// <summary>Unit tests for <see cref="ExtractionPipeline"/>.</summary>
public sealed class ExtractionPipelineTests
{
    private readonly Mock<IPdfParser> _pdfParser = new();
    private readonly Mock<IOcrService> _ocrService = new();
    private readonly Mock<IAiExtractionService> _aiService = new();
    private readonly Mock<ILoggingService> _logger = new();

    private ExtractionPipeline CreatePipeline() =>
        new(_pdfParser.Object, _ocrService.Object, _aiService.Object, _logger.Object);

    private static AiExtractionResponse SuccessResponse(ShipmentRecord? record = null) =>
        new(
            Record: record ?? new ShipmentRecord
            {
                TrackingNumber  = "TRACK123",
                ConsigneeName   = "Test Corp",
                ConfidenceScore = 0.9
            },
            ConfidenceScore: 0.9,
            RawJson: "{}",
            Success: true,
            ErrorMessage: null
        );

    [Fact]
    public async Task FileNotFound_ReturnsFailed()
    {
        var pipeline = CreatePipeline();
        var result = await pipeline.ProcessAsync(@"C:\nonexistent\file.pdf");

        result.Status.Should().Be(ProcessingStatus.Failed);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.UnsupportedFormat);
    }

    [Fact]
    public async Task NonPdfExtension_ReturnsFailed()
    {
        var pipeline = CreatePipeline();
        var result = await pipeline.ProcessAsync("document.txt");

        result.Status.Should().Be(ProcessingStatus.Failed);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.UnsupportedFormat);
    }

    [Fact]
    public async Task SelectableText_SkipsOcr()
    {
        // Create a real temp PDF-named file so File.Exists passes
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            _pdfParser.Setup(p => p.HasSelectableTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true);
            _pdfParser.Setup(p => p.ExtractTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync("some text content");
            _aiService.Setup(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(SuccessResponse());

            var pipeline = CreatePipeline();
            var result = await pipeline.ProcessAsync(pdfFile);

            result.UsedOcrFallback.Should().BeFalse();
            _ocrService.Verify(o => o.RecognizeTextFromPagesAsync(
                It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }

    [Fact]
    public async Task NoSelectableText_UsesOcrFallback()
    {
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            var fakeImage = new byte[] { 1, 2, 3 };
            _pdfParser.Setup(p => p.HasSelectableTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(false);
            _pdfParser.Setup(p => p.RenderPagesToImagesAsync(pdfFile, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<byte[]> { fakeImage });
            _ocrService.Setup(o => o.RecognizeTextFromPagesAsync(It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync("ocr extracted text");
            _aiService.Setup(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(SuccessResponse());

            var pipeline = CreatePipeline();
            var result = await pipeline.ProcessAsync(pdfFile);

            result.UsedOcrFallback.Should().BeTrue();
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }

    [Fact]
    public async Task AiFailure_ReturnsFailedResult()
    {
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            _pdfParser.Setup(p => p.HasSelectableTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true);
            _pdfParser.Setup(p => p.ExtractTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync("some text");
            _aiService.Setup(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new AiExtractionResponse(null, 0, "{}", false, "API error"));

            var pipeline = CreatePipeline();
            var result = await pipeline.ProcessAsync(pdfFile);

            result.Status.Should().Be(ProcessingStatus.Failed);
            result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.AiCallFailure);
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }

    [Fact]
    public async Task ValidationFailure_ReturnsPartialSuccess()
    {
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            // Record with no bill numbers — will fail validation
            var invalidRecord = new ShipmentRecord
            {
                TrackingNumber  = null,
                HouseBillNumber  = null,
                MasterBillNumber = null,
                ConsigneeName   = "Test Corp",
                ConfidenceScore = 0.8
            };

            _pdfParser.Setup(p => p.HasSelectableTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true);
            _pdfParser.Setup(p => p.ExtractTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                      .ReturnsAsync("some text");
            _aiService.Setup(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(SuccessResponse(invalidRecord));

            var pipeline = CreatePipeline();
            var result = await pipeline.ProcessAsync(pdfFile);

            result.Status.Should().Be(ProcessingStatus.PartialSuccess);
            result.Errors.Should().Contain(e => e.Code == ExtractionErrorCode.ValidationFailure);
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }
}
