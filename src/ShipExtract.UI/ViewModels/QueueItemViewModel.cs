using CommunityToolkit.Mvvm.ComponentModel;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Errors;

namespace ShipExtract.UI.ViewModels;

/// <summary>Represents a single file in the processing queue.</summary>
public sealed partial class QueueItemViewModel : ObservableObject
{
    private readonly double _confidenceThreshold;

    /// <summary>Gets the absolute path to the PDF file.</summary>
    public string FilePath { get; }

    /// <summary>Gets the file name extracted from <see cref="FilePath"/>.</summary>
    public string FileName => System.IO.Path.GetFileName(FilePath);

    [ObservableProperty] private ProcessingStatus _status = ProcessingStatus.Pending;
    [ObservableProperty] private bool _usedOcr;
    [ObservableProperty] private double _confidence;
    [ObservableProperty] private string _errorSummary = string.Empty;
    [ObservableProperty] private TimeSpan _duration;

    [ObservableProperty] private string _confidenceText = "\u2014";
    [ObservableProperty] private string _confidenceColor = "#666666";
    [ObservableProperty] private string _confidenceBadgeBackground = "Transparent";
    [ObservableProperty] private string? _confidenceTooltip;
    [ObservableProperty] private string _durationText = "\u2014";
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _rawExtractedText;
    [ObservableProperty] private bool _usedFallback;

    private ProcessingResult? _lastResult;
    private PreProcessingReport? _preProcessingReport;

    /// <summary>Gets whether this completed result is below the confidence threshold.</summary>
    public bool BelowThreshold =>
        IsCompleted && Confidence < _confidenceThreshold;

    /// <summary>Gets the tracking number from the last result, if available.</summary>
    public string? TrackingNumber => _lastResult?.Record?.TrackingNumber;

    /// <summary>Gets the consignee name from the last result, if available.</summary>
    public string? ConsigneeName => _lastResult?.Record?.ConsigneeName;

    /// <summary>Gets the carrier detected from the document text.</summary>
    public CarrierType DetectedCarrier { get; private set; } = CarrierType.Unknown;

    /// <summary>Gets the short badge text for the detected carrier, or empty string when unknown.</summary>
    public string CarrierBadgeText =>
        DetectedCarrier == CarrierType.Unknown ? string.Empty : DetectedCarrier.ToString();

    /// <summary>Gets whether a carrier badge should be shown.</summary>
    public bool ShowCarrierBadge => DetectedCarrier != CarrierType.Unknown;

    /// <summary>Gets the pre-processing report for the last result.</summary>
    public PreProcessingReport? PreProcessingReport => _preProcessingReport;

    /// <summary>Gets whether pre-processing removed any characters.</summary>
    public bool WasPreProcessed => _preProcessingReport?.CharactersRemoved > 0;

    /// <summary>Gets a human-readable summary of pre-processing work done.</summary>
    public string PreProcessingSummaryText =>
        _preProcessingReport == null ? string.Empty :
        WasPreProcessed
            ? $"Cleaned {_preProcessingReport.CharactersRemoved} chars ({_preProcessingReport.ReductionPercent:F1}% reduction)"
            : "No cleaning needed";

    /// <summary>Gets the most recent <see cref="ProcessingResult"/> for this queue item.</summary>
    public ProcessingResult? LastResult => _lastResult;

    /// <summary>Gets the human-readable status label.</summary>
    public string StatusText => Status switch
    {
        ProcessingStatus.Pending        => "Pending",
        ProcessingStatus.Running        => "Processing\u2026",
        ProcessingStatus.Succeeded      => "Done",
        ProcessingStatus.PartialSuccess => "Partial",
        ProcessingStatus.Failed         => "Failed",
        _                               => Status.ToString()
    };

    /// <summary>Initialises a new instance of <see cref="QueueItemViewModel"/>.</summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <param name="confidenceThreshold">Threshold below which the badge turns purple.</param>
    public QueueItemViewModel(string filePath, double confidenceThreshold = 0.60)
    {
        FilePath              = filePath;
        _confidenceThreshold  = confidenceThreshold;
    }

    /// <summary>Updates this view model's state from a completed <see cref="ProcessingResult"/>.</summary>
    public void UpdateFromResult(ProcessingResult result)
    {
        _lastResult          = result;
        Status               = result.Status;
        UsedOcr              = result.UsedOcrFallback;
        UsedFallback         = result.UsedFallbackExtraction;
        RawExtractedText     = result.ExtractedRawText;
        _preProcessingReport = result.PreProcessingReport;
        DetectedCarrier      = result.DetectedCarrier;
        Confidence   = result.Record?.ConfidenceScore ?? 0;
        Duration     = result.ProcessingDuration;
        ErrorSummary = result.Errors.Count > 0
            ? UserFacingMessages.GetMessage(result.Errors[0].Code)
            : string.Empty;

        IsCompleted = result.Status is ProcessingStatus.Succeeded
                      or ProcessingStatus.PartialSuccess
                      or ProcessingStatus.Failed;

        var belowThreshold = IsCompleted && result.Record is not null && Confidence < _confidenceThreshold;

        ConfidenceText = IsCompleted && result.Record is not null
            ? belowThreshold
                ? $"{result.Record.ConfidenceScore:P0} \u26A0"
                : $"{result.Record.ConfidenceScore:P0}"
            : "\u2014";

        ConfidenceColor = belowThreshold
            ? "#FFFFFF"
            : result.Record?.ConfidenceScore switch
            {
                >= 0.8 => "#217346",
                >= 0.5 => "#C55A11",
                > 0    => "#C00000",
                _      => "#666666"
            };

        ConfidenceBadgeBackground = belowThreshold ? "#7B5EA7" : "Transparent";
        ConfidenceTooltip         = belowThreshold
            ? "Below confidence threshold \u2014 will go to Review sheet"
            : null;

        DurationText = IsCompleted
            ? $"{result.ProcessingDuration.TotalSeconds:0.#}s"
            : "\u2014";

        OnPropertyChanged(nameof(BelowThreshold));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TrackingNumber));
        OnPropertyChanged(nameof(ConsigneeName));
        OnPropertyChanged(nameof(PreProcessingReport));
        OnPropertyChanged(nameof(WasPreProcessed));
        OnPropertyChanged(nameof(PreProcessingSummaryText));
        OnPropertyChanged(nameof(DetectedCarrier));
        OnPropertyChanged(nameof(CarrierBadgeText));
        OnPropertyChanged(nameof(ShowCarrierBadge));
    }
}
