using System.Text.Json;
using System.Text.Json.Serialization;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;

namespace ShipExtract.Infrastructure.History;

/// <summary>
/// File-based implementation of <see cref="IBatchHistoryService"/>.
///
/// Storage layout inside <c>historyDirectory</c>:
/// <code>
///   index.json          — array of BatchHistoryEntry (Results omitted)
///   {batchId}.json      — full BatchHistoryEntry including Results
/// </code>
///
/// The store is capped at 30 entries; the oldest are removed when the cap is exceeded.
/// </summary>
public sealed class BatchHistoryService : IBatchHistoryService
{
    private const int MaxEntries = 30;

    private readonly string _historyDir;
    private readonly string _indexPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        PropertyNameCaseInsensitive = true,
        Converters             = { new JsonStringEnumConverter() }
    };

    /// <summary>Initialises a new instance of <see cref="BatchHistoryService"/>.</summary>
    /// <param name="historyDirectory">Directory where history files are stored.</param>
    public BatchHistoryService(string historyDirectory)
    {
        _historyDir = historyDirectory;
        _indexPath  = Path.Combine(historyDirectory, "index.json");
    }

    /// <inheritdoc/>
    public async Task SaveAsync(BatchJob job, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_historyDir);

            var entry = BuildEntry(job);

            // Write detail file (with Results)
            var detailPath = DetailPath(entry.BatchId);
            var detailJson = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(detailPath, detailJson, ct).ConfigureAwait(false);

            // Update index (without Results — summary only for fast listing)
            var index = await LoadIndexAsync(ct).ConfigureAwait(false);
            var summary = new BatchHistoryEntry
            {
                BatchId              = entry.BatchId,
                CompletedAt          = entry.CompletedAt,
                TotalFiles           = entry.TotalFiles,
                SuccessCount         = entry.SuccessCount,
                FailureCount         = entry.FailureCount,
                TotalDurationSeconds = entry.TotalDurationSeconds,
                PrimaryCarrier       = entry.PrimaryCarrier
            };
            index.Insert(0, summary);

            // Trim to cap — remove entries beyond MaxEntries and delete their detail files
            if (index.Count > MaxEntries)
            {
                var toRemove = index.Skip(MaxEntries).ToList();
                foreach (var old in toRemove)
                {
                    var oldDetail = DetailPath(old.BatchId);
                    if (File.Exists(oldDetail))
                        File.Delete(oldDetail);
                }
                index = index.Take(MaxEntries).ToList();
            }

            await WriteIndexAsync(index, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BatchHistoryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoadIndexAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<BatchHistoryEntry?> LoadDetailAsync(Guid batchId, CancellationToken ct = default)
    {
        var path = DetailPath(batchId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<BatchHistoryEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid batchId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(ct).ConfigureAwait(false);
            index.RemoveAll(e => e.BatchId == batchId);
            await WriteIndexAsync(index, ct).ConfigureAwait(false);

            var detail = DetailPath(batchId);
            if (File.Exists(detail))
                File.Delete(detail);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(ct).ConfigureAwait(false);
            foreach (var entry in index)
            {
                var detail = DetailPath(entry.BatchId);
                if (File.Exists(detail))
                    File.Delete(detail);
            }

            await WriteIndexAsync([], ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BatchHistoryEntry BuildEntry(BatchJob job)
    {
        var primaryCarrier = ComputePrimaryCarrier(job.Results);

        return new BatchHistoryEntry
        {
            BatchId              = job.Id,
            CompletedAt          = job.CompletedAt ?? DateTimeOffset.UtcNow,
            TotalFiles           = job.TotalFiles,
            SuccessCount         = job.SuccessCount,
            FailureCount         = job.FailureCount,
            TotalDurationSeconds = job.TotalDuration.TotalSeconds,
            PrimaryCarrier       = primaryCarrier,
            Results              = [..job.Results]
        };
    }

    /// <summary>
    /// Returns the most frequently detected carrier across all results,
    /// excluding <see cref="CarrierType.Unknown"/>.
    /// Returns <see cref="CarrierType.Unknown"/> when no carrier was detected.
    /// </summary>
    public static CarrierType ComputePrimaryCarrier(IEnumerable<ProcessingResult> results)
    {
        var best = results
            .Where(r => r.DetectedCarrier != CarrierType.Unknown)
            .GroupBy(r => r.DetectedCarrier)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return best?.Key ?? CarrierType.Unknown;
    }

    private async Task<List<BatchHistoryEntry>> LoadIndexAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_indexPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<BatchHistoryEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            // Corrupt index — return empty and let the next save overwrite it
            return [];
        }
    }

    private async Task WriteIndexAsync(List<BatchHistoryEntry> index, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(_indexPath, json, ct).ConfigureAwait(false);
    }

    private string DetailPath(Guid batchId) =>
        Path.Combine(_historyDir, $"{batchId:N}.json");
}
