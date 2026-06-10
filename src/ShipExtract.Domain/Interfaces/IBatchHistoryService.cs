using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Interfaces;

/// <summary>Persists completed batches to disk and exposes them for the history view.</summary>
public interface IBatchHistoryService
{
    /// <summary>
    /// Saves a completed <see cref="BatchJob"/> to the history store.
    /// Trims the store to the most recent 30 entries after saving.
    /// </summary>
    Task SaveAsync(BatchJob job, CancellationToken ct = default);

    /// <summary>
    /// Returns all history entries (summary data only — no Results).
    /// Ordered newest-first.
    /// </summary>
    Task<IReadOnlyList<BatchHistoryEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads the full <see cref="BatchHistoryEntry"/> including <see cref="BatchHistoryEntry.Results"/>
    /// for the given batch identifier.
    /// Returns <see langword="null"/> when the entry does not exist.
    /// </summary>
    Task<BatchHistoryEntry?> LoadDetailAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Removes a single entry and its detail file from the store.</summary>
    Task DeleteAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Removes all entries and detail files from the store.</summary>
    Task ClearAllAsync(CancellationToken ct = default);
}
