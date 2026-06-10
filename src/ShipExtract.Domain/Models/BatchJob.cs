using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Models;

/// <summary>Represents a batch of documents submitted for parallel extraction processing.</summary>
public sealed class BatchJob
{
    /// <summary>Unique identifier for this batch job.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Ordered list of file paths to be processed in this batch.</summary>
    public required IReadOnlyList<string> FilePaths { get; init; }

    /// <summary>Current overall status of the batch.</summary>
    public BatchStatus Status { get; set; } = BatchStatus.Pending;

    /// <summary>Total number of files in the batch.</summary>
    public int TotalFiles => FilePaths.Count;

    /// <summary>Number of files that have been processed (successfully or not).</summary>
    public int ProcessedFiles { get; set; }

    /// <summary>Number of files that were processed successfully.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Number of files that failed processing.</summary>
    public int FailureCount { get; set; }

    /// <summary>Individual processing results for each file in the batch.</summary>
    public List<ProcessingResult> Results { get; init; } = [];

    /// <summary>UTC timestamp when the batch was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp when all batch processing completed, or <see langword="null"/> if still in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Percentage of files processed, in the range 0–100.</summary>
    public double ProgressPercent => TotalFiles == 0 ? 0 : (double)ProcessedFiles / TotalFiles * 100;

    /// <summary>Total wall-clock duration of the batch (completed or still running).</summary>
    public TimeSpan TotalDuration =>
        CompletedAt.HasValue
            ? CompletedAt.Value - CreatedAt
            : DateTimeOffset.UtcNow - CreatedAt;

    /// <summary>Average processing time per file, in seconds. Zero when no files have been processed.</summary>
    public double AverageSecondsPerFile =>
        ProcessedFiles == 0 ? 0 :
            TotalDuration.TotalSeconds / ProcessedFiles;
}
