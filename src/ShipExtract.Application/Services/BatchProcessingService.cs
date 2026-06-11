using ShipExtract.Application.Pipelines;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Application.Services;

/// <summary>Defines the contract for processing a batch of documents.</summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Raised when a network connectivity problem is detected before or during processing.
    /// The string argument is a human-readable warning message.
    /// </summary>
    event Action<string>? OnNetworkWarning;

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
/// Processes a batch of PDF documents, delegating each file to
/// <see cref="ExtractionPipeline"/> with provider-aware concurrency.
/// Ollama runs sequentially (1 at a time); Anthropic allows up to 4 concurrent.
/// </summary>
public sealed class BatchProcessingService : IBatchProcessingService
{
    private readonly ExtractionPipeline _pipeline;
    private readonly ILoggingService _logger;
    private readonly int _maxConcurrency;
    private readonly IBatchHistoryService? _historyService;
    private readonly INetworkChecker? _networkChecker;

    // Progress throttle: report at most once per 500 ms to avoid UI flooding.
    private long _lastProgressTickMs;

    /// <inheritdoc/>
    public event Action<string>? OnNetworkWarning;

    /// <summary>Initialises a new instance of <see cref="BatchProcessingService"/>.</summary>
    public BatchProcessingService(
        ExtractionPipeline pipeline,
        ILoggingService logger,
        int maxConcurrency = 4,
        IBatchHistoryService? historyService = null,
        INetworkChecker? networkChecker = null)
    {
        _pipeline        = pipeline;
        _logger          = logger;
        _maxConcurrency  = maxConcurrency;
        _historyService  = historyService;
        _networkChecker  = networkChecker;
    }

    /// <inheritdoc/>
    public async Task<BatchJob> ProcessBatchAsync(
        IReadOnlyList<string> filePaths,
        IProgress<BatchJob>? progress = null,
        CancellationToken ct = default)
    {
        // Deduplicate by case-insensitive path
        var deduplicated = filePaths
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Network pre-flight (non-blocking — warn but do not abort)
        if (_networkChecker is not null && !ct.IsCancellationRequested)
        {
            try
            {
                var reachable = await _networkChecker.IsAnthropicReachableAsync(ct).ConfigureAwait(false);
                if (!reachable)
                {
                    const string msg = "Cannot reach api.anthropic.com — API calls may fail. " +
                                       "Check your internet connection.";
                    _logger.LogWarning(msg);
                    OnNetworkWarning?.Invoke(msg);
                }
            }
            catch
            {
                // Network check itself must never crash the batch
            }
        }

        var job = new BatchJob
        {
            FilePaths = deduplicated,
            CreatedAt = DateTimeOffset.UtcNow,
            Status    = BatchStatus.Running
        };

        _logger.LogInformation("Batch started — {TotalFiles} file(s) to process (max concurrency: {Concurrency}).",
            job.TotalFiles, _maxConcurrency);

        var semaphore    = new SemaphoreSlim(_maxConcurrency);
        var tasks        = new List<Task>();
        var resultsLock  = new object();
        _lastProgressTickMs = 0;

        foreach (var filePath in deduplicated)
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
                        fileResult = await ProcessWithRetryAsync(path, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
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

                    bool shouldReport;
                    lock (resultsLock)
                    {
                        job.Results.Add(fileResult);
                        job.ProcessedFiles++;

                        if (fileResult.Status is ProcessingStatus.Succeeded or ProcessingStatus.PartialSuccess)
                            job.SuccessCount++;
                        else
                            job.FailureCount++;

                        // Throttle progress reports to 500 ms
                        var nowMs = Environment.TickCount64;
                        shouldReport = (nowMs - _lastProgressTickMs) >= 500;
                        if (shouldReport)
                            _lastProgressTickMs = nowMs;
                    }

                    _logger.LogInformation("File processed: {FilePath} → {Status}", path, fileResult.Status);
                    if (shouldReport)
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

        // Always send a final progress update so the UI reflects the last file
        progress?.Report(job);

        job.CompletedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Batch finished — Status: {Status}, Success: {Success}, Failure: {Failure}",
            job.Status, job.SuccessCount, job.FailureCount);

        if (_historyService is not null && job.Status == BatchStatus.Completed)
        {
            try
            {
                await _historyService.SaveAsync(job, CancellationToken.None).ConfigureAwait(false);
                _logger.LogInformation("Batch {BatchId} saved to history.", job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to save batch to history: {Reason}", ex.Message);
            }
        }

        return job;
    }

    private async Task<ProcessingResult> ProcessWithRetryAsync(string path, CancellationToken ct)
    {
        try
        {
            return await _pipeline.ProcessAsync(path, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("File {FilePath} failed on first attempt — retrying once. Reason: {Reason}",
                path, ex.Message);
            await Task.Delay(2000, ct).ConfigureAwait(false);
            return await _pipeline.ProcessAsync(path, ct).ConfigureAwait(false);
        }
    }
}
