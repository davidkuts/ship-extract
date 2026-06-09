using FluentAssertions;
using Moq;
using ShipExtract.Application.Pipelines;
using ShipExtract.Application.Services;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Tests.Services;

/// <summary>Unit tests for <see cref="BatchProcessingService"/>.</summary>
public sealed class BatchProcessingServiceTests
{
    private readonly Mock<IPdfParser> _pdfParser = new();
    private readonly Mock<IOcrService> _ocrService = new();
    private readonly Mock<IAiExtractionService> _aiService = new();
    private readonly Mock<ILoggingService> _logger = new();

    private ExtractionPipeline CreatePipeline() =>
        new(_pdfParser.Object, _ocrService.Object, _aiService.Object, _logger.Object);

    private BatchProcessingService CreateService() =>
        new(CreatePipeline(), _logger.Object);

    [Fact]
    public async Task EmptyFileList_ReturnsCompletedJobWithZeroResults()
    {
        var service = CreateService();
        var job = await service.ProcessBatchAsync([]);

        job.Status.Should().Be(BatchStatus.Completed);
        job.TotalFiles.Should().Be(0);
        job.Results.Should().BeEmpty();
        job.SuccessCount.Should().Be(0);
        job.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task SingleFile_Success_CorrectCounts()
    {
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            SetupSuccessfulPipeline(pdfFile);

            var service = CreateService();
            var job = await service.ProcessBatchAsync([pdfFile]);

            job.Status.Should().Be(BatchStatus.Completed);
            job.ProcessedFiles.Should().Be(1);
            job.SuccessCount.Should().Be(1);
            job.FailureCount.Should().Be(0);
            job.Results.Should().HaveCount(1);
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }

    [Fact]
    public async Task OneFileFails_OtherSucceeds_BothRecorded()
    {
        var tempFile = Path.GetTempFileName();
        var pdfFile = Path.ChangeExtension(tempFile, ".pdf");
        File.Move(tempFile, pdfFile);

        try
        {
            SetupSuccessfulPipeline(pdfFile);

            var service = CreateService();
            // "bad.pdf" does not exist → will fail
            var job = await service.ProcessBatchAsync([pdfFile, @"C:\nonexistent\bad.pdf"]);

            job.Status.Should().Be(BatchStatus.Completed);
            job.ProcessedFiles.Should().Be(2);
            job.SuccessCount.Should().Be(1);
            job.FailureCount.Should().Be(1);
            job.Results.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(pdfFile);
        }
    }

    [Fact]
    public async Task CancellationToken_StopsProcessing_StatusIsCancelled()
    {
        using var cts = new CancellationTokenSource();

        // Use a file that doesn't exist — pipeline returns Failed quickly
        // We cancel immediately
        await cts.CancelAsync();

        var service = CreateService();
        var job = await service.ProcessBatchAsync(
            [@"C:\nonexistent\file1.pdf", @"C:\nonexistent\file2.pdf"],
            ct: cts.Token);

        job.Status.Should().Be(BatchStatus.Cancelled);
    }

    private void SetupSuccessfulPipeline(string pdfFile)
    {
        _pdfParser.Setup(p => p.HasSelectableTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);
        _pdfParser.Setup(p => p.ExtractTextAsync(pdfFile, It.IsAny<CancellationToken>()))
                  .ReturnsAsync("valid text content");
        _aiService.Setup(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new AiExtractionResponse(
                      new ShipmentRecord { TrackingNumber = "T123", ConsigneeName = "Corp", ConfidenceScore = 0.9 },
                      0.9, "{}", true, null));
    }
}
