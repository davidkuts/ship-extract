using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.Input;
using ShipExtract.Application.Services;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Export;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.DependencyInjection;

namespace ShipExtract.UI.ViewModels;

/// <summary>Primary view model for the main application window.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IBatchProcessingService _batchService;
    private readonly ExportServiceFactory _exportFactory;
    private readonly ILoggingService _logger;
    private readonly ICredentialService _credentialService;
    private readonly IOcrService _ocrService;
    private readonly AppSettings _appSettings;
    private readonly IUpdateService? _updateService;
    private readonly ISettingsService? _settingsService;

    private BatchJob? _lastBatchJob;
    private int _failureCount;
    private string? _pendingUpdateVersion;
    private string? _pendingUpdateUrl;

    [ObservableProperty] private ObservableCollection<QueueItemViewModel> _queueItems = [];
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private QueueItemViewModel? _selectedQueueItem;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatusMessage;
    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private int _processedFiles;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartProcessingCommand))]
    private string _selectedOutputDirectory = string.Empty;

    [ObservableProperty] private bool _ollamaWarningVisible;
    [ObservableProperty] private bool _networkWarningVisible;
    [ObservableProperty] private string _networkWarningText = string.Empty;
    [ObservableProperty] private string _batchSummaryText = string.Empty;
    [ObservableProperty] private string _statusBarLeftText = "Drop PDF files here or click to browse";

    // ── Update banner ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _showUpdateBanner;
    [ObservableProperty] private string _updateBannerText = string.Empty;

    /// <summary>Gets whether the queue is empty (used for empty-state panel).</summary>
    public bool ShowEmptyState => QueueItems.Count == 0;

    /// <summary>Gets the current confidence threshold (for binding in ResultPreviewPanel).</summary>
    public double ConfidenceThreshold => _appSettings.MinimumConfidenceThreshold;

    /// <summary>Gets whether an API key or Ollama provider is configured.</summary>
    public bool ApiKeyIsSet =>
        _appSettings.AiProvider == AiProvider.Ollama
        || _credentialService.GetApiKey() is { Length: > 0 };

    /// <summary>Gets the display name of the active AI provider.</summary>
    public string ActiveProviderText =>
        _appSettings.AiProvider == AiProvider.Anthropic
            ? "Claude (Anthropic)"
            : $"{_appSettings.OllamaModel} (Local)";

    /// <summary>Gets whether OCR is available (tessdata files present).</summary>
    public bool OcrIsAvailable => _ocrService.IsAvailable;

    /// <summary>Gets the dynamic window title.</summary>
    public string WindowTitle
    {
        get
        {
            if (IsProcessing)
                return _appSettings.AiProvider == AiProvider.Ollama
                    ? $"ShipExtract \u2014 Processing file {ProcessedFiles + 1} of {TotalFiles}\u2026"
                    : $"ShipExtract \u2014 Processing ({ProcessedFiles}/{TotalFiles})\u2026";
            if (HasResults)
                return _failureCount == 0
                    ? "ShipExtract \u2014 Done \u2713"
                    : $"ShipExtract \u2014 Done ({_failureCount} failed)";
            if (TotalFiles > 0)
                return $"ShipExtract \u2014 {TotalFiles} file(s) queued";
            return "ShipExtract";
        }
    }

    /// <summary>Gets the application version string.</summary>
    public string Version { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>Initialises a new instance of <see cref="MainViewModel"/>.</summary>
    public MainViewModel(
        IBatchProcessingService batchService,
        ExportServiceFactory exportFactory,
        ILoggingService logger,
        ICredentialService credentialService,
        IOcrService ocrService,
        AppSettings appSettings,
        IUpdateService? updateService = null,
        ISettingsService? settingsService = null)
    {
        _batchService      = batchService;
        _exportFactory     = exportFactory;
        _logger            = logger;
        _credentialService = credentialService;
        _ocrService        = ocrService;
        _appSettings       = appSettings;
        _updateService     = updateService;
        _settingsService   = settingsService;

        _selectedOutputDirectory = Environment.ExpandEnvironmentVariables(
            "%USERPROFILE%\\Documents\\ShipExtract");

        // Keep ShowEmptyState in sync with queue changes
        _queueItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            RefreshStatusBarLeft();
        };

        // Subscribe to network warnings from the batch service
        _batchService.OnNetworkWarning += msg =>
        {
            if (_appSettings.AiProvider == AiProvider.Anthropic)
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    NetworkWarningText    = msg;
                    NetworkWarningVisible = true;
                });
            }
        };

        // Fire-and-forget update check (3 s delay so startup UI is not blocked)
        if (_appSettings.CheckForUpdatesOnStartup && _updateService is not null)
            _ = CheckForUpdateAfterDelayAsync();
    }

    private async Task CheckForUpdateAfterDelayAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            var info = await _updateService!.CheckForUpdateAsync().ConfigureAwait(false);
            if (info is null) return;

            // Persist last-check date
            _appSettings.LastUpdateCheckDate = DateTime.UtcNow;
            _settingsService?.Save(_appSettings);

            if (!info.IsUpdateAvailable) return;

            // Respect "skip this version"
            if (!string.IsNullOrWhiteSpace(_appSettings.SkippedVersion) &&
                System.Version.TryParse(_appSettings.SkippedVersion, out var skipped) &&
                info.LatestVersion <= skipped)
                return;

            _pendingUpdateVersion = info.LatestVersion.ToString();
            _pendingUpdateUrl     = info.DownloadUrl;

            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                UpdateBannerText  = $"Update available: v{info.LatestVersion}";
                ShowUpdateBanner  = true;
            });
        }
        catch { /* never surface update errors */ }
    }

    /// <summary>Dismisses the network warning banner.</summary>
    [RelayCommand]
    private void DismissNetworkWarning() => NetworkWarningVisible = false;

    /// <summary>Opens the update download page in the default browser.</summary>
    [RelayCommand]
    private void OpenUpdatePage()
    {
        if (string.IsNullOrWhiteSpace(_pendingUpdateUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(_pendingUpdateUrl) { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    /// <summary>Marks the pending version as skipped so the banner won't reappear for it.</summary>
    [RelayCommand]
    private void SkipUpdateVersion()
    {
        if (string.IsNullOrWhiteSpace(_pendingUpdateVersion)) return;
        _appSettings.SkippedVersion = _pendingUpdateVersion;
        _settingsService?.Save(_appSettings);
        ShowUpdateBanner = false;
    }

    /// <summary>Dismisses the update banner for this session only.</summary>
    [RelayCommand]
    private void DismissUpdateBanner() => ShowUpdateBanner = false;

    /// <summary>Cancels in-progress processing (bound to Escape key).</summary>
    [RelayCommand]
    private void HandleEscape()
    {
        if (IsProcessing)
            StartProcessingCommand.Cancel();
    }

    /// <summary>Opens the containing folder for a queue item and selects the file.</summary>
    [RelayCommand]
    private void OpenFileLocation(QueueItemViewModel? item)
    {
        if (item is null) return;
        try { Process.Start("explorer.exe", $"/select,\"{item.FilePath}\""); }
        catch { /* best effort */ }
    }

    private void RefreshStatusBarLeft()
    {
        if (IsProcessing)
            StatusBarLeftText = StatusMessage;
        else if (HasResults)
            StatusBarLeftText = BatchSummaryText;
        else if (TotalFiles > 0)
            StatusBarLeftText = $"{TotalFiles} file{(TotalFiles == 1 ? "" : "s")} queued \u2014 ready to process";
        else
            StatusBarLeftText = "Drop PDF files here or click to browse";
    }

    /// <summary>Refreshes warning banner properties after settings change.</summary>
    public void RefreshWarnings()
    {
        OnPropertyChanged(nameof(ApiKeyIsSet));
        OnPropertyChanged(nameof(OcrIsAvailable));
        OnPropertyChanged(nameof(ActiveProviderText));

        if (_appSettings.AiProvider == AiProvider.Ollama)
        {
            // Fire-and-forget health check
            _ = Task.Run(async () =>
            {
                try
                {
                    var health = ServiceLocator.Get<IOllamaHealthService>();
                    var result = await health.CheckAsync(_appSettings.OllamaBaseUrl,
                        _appSettings.OllamaModel, CancellationToken.None);
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        OllamaWarningVisible = !result.IsRunning;
                    });
                }
                catch
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        OllamaWarningVisible = true;
                    });
                }
            });
        }
        else
        {
            OllamaWarningVisible = false;
        }
    }

    /// <summary>Adds valid PDF files to the processing queue.</summary>
    [RelayCommand]
    private void AddFiles(IReadOnlyList<string> filePaths)
    {
        if (IsProcessing) return;

        int skipped = 0;
        foreach (var path in filePaths)
        {
            if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;
            if (QueueItems.Any(q => q.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            QueueItems.Add(new QueueItemViewModel(path, _appSettings.MinimumConfidenceThreshold));
        }

        TotalFiles = QueueItems.Count;
        OnPropertyChanged(nameof(WindowTitle));
        StartProcessingCommand.NotifyCanExecuteChanged();

        if (skipped > 0)
            StatusMessage = $"{skipped} duplicate file(s) skipped.";

        var count = QueueItems.Count;
        if (count > 100)
        {
            WpfMessageBox.Show(
                $"{count} files queued. Processing this many files may take a long time and consume significant AI credits.",
                "ShipExtract \u2014 Large Batch",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        else if (count > 50)
        {
            StatusMessage = $"{count} files queued \u2014 large batch, processing may take a while.";
        }
    }

    /// <summary>Removes a file from the queue.</summary>
    [RelayCommand]
    private void RemoveFile(QueueItemViewModel item)
    {
        if (IsProcessing) return;
        QueueItems.Remove(item);
        TotalFiles = QueueItems.Count;
        OnPropertyChanged(nameof(WindowTitle));
        StartProcessingCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Clears all files from the queue.</summary>
    [RelayCommand]
    private void ClearQueue()
    {
        if (IsProcessing) return;
        QueueItems.Clear();
        TotalFiles = 0;
        _failureCount = 0;
        HasResults = false;
        StatusMessage = string.Empty;
        BatchSummaryText = string.Empty;
        OnPropertyChanged(nameof(WindowTitle));
        StartProcessingCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartProcessing() => !IsProcessing && QueueItems.Count > 0;

    /// <summary>Starts processing all queued PDF files.</summary>
    [RelayCommand(CanExecute = nameof(CanStartProcessing))]
    private async Task StartProcessing(CancellationToken ct)
    {
        if (!ApiKeyIsSet)
        {
            WpfMessageBox.Show(
                "No AI provider is configured. Please open Settings to configure Anthropic API key or Ollama.",
                "ShipExtract", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOutputDirectory))
        {
            WpfMessageBox.Show(
                "Please select an output directory before processing.",
                "ShipExtract", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsProcessing    = true;
        HasResults      = false;
        ProcessedFiles  = 0;
        ProgressPercent = 0;
        _failureCount   = 0;
        StatusMessage   = _appSettings.AiProvider == AiProvider.Ollama
            ? $"Processing sequentially (1 at a time) \u2014 file 1 of {QueueItems.Count}"
            : "Processing\u2026";

        // Mark all items as Running
        foreach (var item in QueueItems)
            item.Status = ProcessingStatus.Running;

        var filePaths = QueueItems.Select(q => q.FilePath).ToList();
        var queueMap  = QueueItems.ToDictionary(q => q.FilePath, StringComparer.OrdinalIgnoreCase);

        var progress = new Progress<BatchJob>(job =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                ProcessedFiles  = job.ProcessedFiles;
                ProgressPercent = job.ProgressPercent;

                if (_appSettings.AiProvider == AiProvider.Ollama && job.ProcessedFiles < job.TotalFiles)
                    StatusMessage = $"Processing sequentially (1 at a time) \u2014 file {job.ProcessedFiles + 1} of {job.TotalFiles}";

                // Update the most recently completed result
                var latest = job.Results.LastOrDefault();
                if (latest is not null && queueMap.TryGetValue(latest.SourceFilePath, out var vm))
                    vm.UpdateFromResult(latest);
            });
        });

        try
        {
            _lastBatchJob = await _batchService.ProcessBatchAsync(filePaths, progress, ct);

            // Final sync — make sure all items reflect their result
            foreach (var result in _lastBatchJob.Results)
            {
                if (queueMap.TryGetValue(result.SourceFilePath, out var vm))
                    vm.UpdateFromResult(result);
            }

            _failureCount = _lastBatchJob.FailureCount;
            HasResults    = true;
            StatusMessage = $"{_lastBatchJob.SuccessCount} succeeded, {_lastBatchJob.FailureCount} failed.";

            var totalSecs = _lastBatchJob.TotalDuration.TotalSeconds;
            var avgSecs   = _lastBatchJob.AverageSecondsPerFile;
            BatchSummaryText = _lastBatchJob.ProcessedFiles == 0
                ? string.Empty
                : $"{_lastBatchJob.SuccessCount} succeeded \u00B7 {_lastBatchJob.FailureCount} failed \u00B7 {totalSecs:F0}s total \u00B7 avg {avgSecs:F1}s/file";

            _logger.LogInformation(
                "Batch complete: {Success} succeeded, {Failure} failed.",
                _lastBatchJob.SuccessCount, _lastBatchJob.FailureCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Batch processing error", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Opens a folder browser dialog to choose the output directory.</summary>
    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description         = "Select output directory",
            SelectedPath        = SelectedOutputDirectory,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SelectedOutputDirectory = dialog.SelectedPath;
    }

    private bool CanExport() => HasResults && _lastBatchJob is not null;

    /// <summary>Exports results to CSV.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsv(CancellationToken ct)
    {
        await ExportAsync(ExportFormat.Csv, "csv", ct);
    }

    /// <summary>Exports results to Excel.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportExcel(CancellationToken ct)
    {
        await ExportAsync(ExportFormat.Excel, "xlsx", ct);
    }

    private async Task ExportAsync(ExportFormat format, string extension, CancellationToken ct)
    {
        if (_lastBatchJob is null) return;

        try
        {
            Directory.CreateDirectory(SelectedOutputDirectory);
            var timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var rawPrefix  = string.IsNullOrWhiteSpace(_appSettings.ExportFilePrefix) ? "ShipExtract" : _appSettings.ExportFilePrefix;
            var prefix     = string.Concat(rawPrefix.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var outputPath = Path.Combine(SelectedOutputDirectory, $"{prefix}_{timestamp}.{extension}");

            var threshold = _appSettings.MinimumConfidenceThreshold;
            var exporter  = _exportFactory.GetService(format);
            await exporter.ExportAsync(_lastBatchJob.Results, outputPath, threshold, ct);

            _logger.LogInformation("Exported {Format} to {Path}", format, outputPath);

            // Count above/below threshold for the status message
            var exportable = _lastBatchJob.Results
                .Where(r => r.Record is not null &&
                            r.Status is Domain.Enums.ProcessingStatus.Succeeded
                                     or Domain.Enums.ProcessingStatus.PartialSuccess)
                .ToList();
            var aboveCount = exportable.Count(r => r.MeetsConfidenceThreshold(threshold));
            var belowCount = exportable.Count - aboveCount;

            StatusMessage = belowCount > 0
                ? $"Exported {aboveCount} results \u00B7 {belowCount} sent to Review sheet"
                : $"Exported {aboveCount} results \u00B7 All above {threshold:P0} threshold";

            WpfMessageBox.Show(
                $"Export complete:\n{outputPath}",
                "ShipExtract \u2014 Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Export failed ({format})", ex);
            WpfMessageBox.Show(
                $"Export failed:\n{ex.Message}",
                "ShipExtract \u2014 Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>Opens the output directory in Windows Explorer.</summary>
    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (Directory.Exists(SelectedOutputDirectory))
            Process.Start("explorer.exe", SelectedOutputDirectory);
    }

    /// <summary>
    /// Restores a previously completed batch from history into the queue and result view.
    /// </summary>
    public void LoadFromHistory(Domain.Models.BatchHistoryEntry entry)
    {
        if (IsProcessing) return;

        QueueItems.Clear();
        _failureCount = 0;
        HasResults    = false;
        StatusMessage = string.Empty;
        BatchSummaryText = string.Empty;

        // Reconstruct a BatchJob so export commands work
        var job = new BatchJob
        {
            FilePaths   = entry.Results.Select(r => r.SourceFilePath).ToList(),
            CreatedAt   = entry.CompletedAt - TimeSpan.FromSeconds(entry.TotalDurationSeconds),
            Status      = Domain.Enums.BatchStatus.Completed,
            CompletedAt = entry.CompletedAt,
            SuccessCount = entry.SuccessCount,
            FailureCount = entry.FailureCount
        };
        job.Results.AddRange(entry.Results);
        job.ProcessedFiles = entry.TotalFiles;
        _lastBatchJob = job;

        foreach (var result in entry.Results)
        {
            var item = new QueueItemViewModel(result.SourceFilePath, _appSettings.MinimumConfidenceThreshold);
            item.UpdateFromResult(result);
            QueueItems.Add(item);
        }

        TotalFiles     = QueueItems.Count;
        ProcessedFiles = TotalFiles;
        _failureCount  = entry.FailureCount;
        HasResults     = true;
        StatusMessage  = $"Loaded from history: {entry.SuccessCount} succeeded, {entry.FailureCount} failed.";
        OnPropertyChanged(nameof(ShowEmptyState));

        var totalSecs = entry.TotalDurationSeconds;
        var avgSecs   = TotalFiles > 0 ? totalSecs / TotalFiles : 0;
        BatchSummaryText = TotalFiles == 0
            ? string.Empty
            : $"{entry.SuccessCount} succeeded \u00B7 {entry.FailureCount} failed \u00B7 {totalSecs:F0}s total \u00B7 avg {avgSecs:F1}s/file";

        OnPropertyChanged(nameof(WindowTitle));
        StartProcessingCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gets the processing result for the currently selected queue item.</summary>
    public ProcessingResult? SelectedResult => SelectedQueueItem?.LastResult;

    partial void OnSelectedQueueItemChanged(QueueItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedResult));
    }

    partial void OnStatusMessageChanged(string value)
    {
        HasStatusMessage = !string.IsNullOrEmpty(value);
        RefreshStatusBarLeft();
    }

    partial void OnIsProcessingChanged(bool value)
    {
        StartProcessingCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(WindowTitle));
        RefreshStatusBarLeft();
    }

    partial void OnHasResultsChanged(bool value)
    {
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(WindowTitle));
        RefreshStatusBarLeft();
    }

    partial void OnBatchSummaryTextChanged(string value) => RefreshStatusBarLeft();

    partial void OnProcessedFilesChanged(int value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnTotalFilesChanged(int value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }
}
