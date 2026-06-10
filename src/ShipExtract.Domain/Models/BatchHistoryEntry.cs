using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Models;

/// <summary>
/// A lightweight snapshot of a completed <see cref="BatchJob"/> persisted to disk.
/// The <see cref="Results"/> list is only populated when loading the full detail file;
/// the index stores summary fields only for fast listing.
/// </summary>
public sealed class BatchHistoryEntry
{
    /// <summary>Unique identifier matching the original <see cref="BatchJob.Id"/>.</summary>
    public Guid BatchId { get; init; } = Guid.NewGuid();

    /// <summary>UTC timestamp when the batch completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Total number of files in the batch.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Number of files that succeeded (Succeeded or PartialSuccess).</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of files that failed.</summary>
    public int FailureCount { get; init; }

    /// <summary>Total wall-clock duration of the batch in seconds.</summary>
    public double TotalDurationSeconds { get; init; }

    /// <summary>
    /// The most frequently detected carrier across all results (excluding Unknown).
    /// <see cref="CarrierType.Unknown"/> when no carrier was detected.
    /// </summary>
    public CarrierType PrimaryCarrier { get; init; } = CarrierType.Unknown;

    /// <summary>
    /// Full processing results for this batch.
    /// Empty when loaded from the index; populated when loading the per-batch detail file.
    /// </summary>
    public List<ProcessingResult> Results { get; init; } = [];
}
