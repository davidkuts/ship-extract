using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;

namespace ShipExtract.UI.ViewModels;

/// <summary>Wraps a <see cref="BatchHistoryEntry"/> for display in the history list.</summary>
public sealed class BatchHistoryEntryViewModel
{
    private readonly BatchHistoryEntry _entry;

    /// <summary>The underlying history entry.</summary>
    public BatchHistoryEntry Entry => _entry;

    /// <summary>Unique identifier of the batch.</summary>
    public Guid BatchId => _entry.BatchId;

    /// <summary>Local date/time when the batch completed.</summary>
    public DateTime CompletedAtLocal => _entry.CompletedAt.LocalDateTime;

    /// <summary>Formatted completion timestamp.</summary>
    public string CompletedAtText => CompletedAtLocal.ToString("yyyy-MM-dd  HH:mm");

    /// <summary>Total number of files in the batch.</summary>
    public int TotalFiles => _entry.TotalFiles;

    /// <summary>Number of succeeded files.</summary>
    public int SuccessCount => _entry.SuccessCount;

    /// <summary>Number of failed files.</summary>
    public int FailureCount => _entry.FailureCount;

    /// <summary>Total batch duration formatted as m:ss.</summary>
    public string DurationText
    {
        get
        {
            var ts = TimeSpan.FromSeconds(_entry.TotalDurationSeconds);
            return ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s"
                : $"{ts.Seconds}s";
        }
    }

    /// <summary>Primary carrier name, or "—" when unknown.</summary>
    public string CarrierText =>
        _entry.PrimaryCarrier == CarrierType.Unknown ? "\u2014" : _entry.PrimaryCarrier.ToString();

    /// <summary>Summary line shown below the date in the list.</summary>
    public string SummaryText =>
        $"{SuccessCount}/{TotalFiles} succeeded  \u00B7  {DurationText}  \u00B7  {CarrierText}";

    /// <summary>Initialises a new instance of <see cref="BatchHistoryEntryViewModel"/>.</summary>
    public BatchHistoryEntryViewModel(BatchHistoryEntry entry) => _entry = entry;
}
