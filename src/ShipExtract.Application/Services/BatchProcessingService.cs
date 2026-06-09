using ShipExtract.Application.Pipelines;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Services;

/// <summary>Defines the contract for processing a batch of documents.</summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Processes a list of file paths as a batch, returning a completed <see cref="BatchJob"/>.
    /// </summary>
    /// <param name="filePaths">Absolute paths to the PDF files to process.</param>
    /// <param name="progress">Optional progress callback invoked after each file completes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The completed <see cref="BatchJob"/> with all results.</returns>
    Task<BatchJob> ProcessBatchAsync(
        IReadOnlyList<string> filePaths,
        IProgress<BatchJob>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Processes a batch of PDF documents concurrently, delegating each file to
/// <see cref="ExtractionPipeline"/> with a maximum parallelism of 4.
/// </summary>
public sealed class BatchProcessingService : IBatchProcessingService
{
    private const int MaxDegreeOfParallelism = 4;

    private readonly ExtractionPipeline _pipeline;
    private readonly ILoggingService _logger;

    /// <summary>Initialises a new instance of <see cref="BatchProcessingService"/>.</summary>
    public BatchProcessingService(ExtractionPipeline pipeline, ILoggingService logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BatchJob> ProcessBatchAsync(
        IReadOnlyList<string> filePaths,
        IProgress<BatchJob>? progress = null,
        CancellationToken ct = default)
    {
        var job = new BatchJob
        {
            FilePaths = filePaths,
            CreatedAt = DateTimeOffset.UtcNow,
            Status    = BatchStatus.Running
        };

        _logger.LogInformation("Batch started — {TotalFiles} file(s) to process.", job.TotalFiles);

        var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);
        var tasks = new List<Task>();
        var resultsLock = new object();

        foreach (var filePath in filePaths)
        {
            if (ct.IsCancellationRequested)
                break;

            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            var path = filePath; // capture for lambda
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    ProcessingResult fileResult;
                    try
                    {
                        fileResult = await _pipeline.ProcessAsync(path, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Do not record cancelled files as failures
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Unhandled exception for file {FilePath}", ex, path);
                        fileResult = new ProcessingResult
                        {
                            SourceFilePath = path,
                            Status         = ProcessingStatus.Failed
                        };
                        fileResult.Errors.Add(new Domain.Models.ExtractionError
                        {
                            Code    = Domain.Enums.ExtractionErrorCode.AiCallFailure,
                            Message = ex.Message,
                            Exception = ex
                        });
                    }

                    lock (resultsLock)
                    {
                        job.Results.Add(fileResult);
                        job.ProcessedFiles++;

                        if (fileResult.Status is ProcessingStatus.Succeeded or ProcessingStatus.PartialSuccess)
                            job.SuccessCount++;
                        else
                            job.FailureCount++;
                    }

                    _logger.LogInformation("File processed: {FilePath} → {Status}", path, fileResult.Status);
                    progress?.Report(job);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            job.Status = ct.IsCancellationRequested ? BatchStatus.Cancelled : BatchStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = BatchStatus.Cancelled;
        }

        job.CompletedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Batch finished — Status: {Status}, Success: {Success}, Failure: {Failure}",
            job.Status, job.SuccessCount, job.FailureCount);

        return job;
    }
}
