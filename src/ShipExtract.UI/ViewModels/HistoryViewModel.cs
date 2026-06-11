using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox  = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Export;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.UI.ViewModels;

/// <summary>View model for the batch history window.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IBatchHistoryService _historyService;
    private readonly ExportServiceFactory _exportFactory;
    private readonly ILoggingService _logger;
    private readonly AppSettings _appSettings;

    /// <summary>
    /// Raised when the user clicks "Load" on a history entry so the main window can
    /// restore the batch results into the queue.
    /// </summary>
    public event Action<BatchHistoryEntry>? BatchLoaded;

    [ObservableProperty] private ObservableCollection<BatchHistoryEntryViewModel> _entries = [];
    [ObservableProperty] private BatchHistoryEntryViewModel? _selectedEntry;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    /// <summary>Gets whether a status message is currently set.</summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>Gets whether there is a selected entry with results that can be exported.</summary>
    public bool CanExport => SelectedEntry is not null;

    /// <summary>Gets whether there are any entries in the history.</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>Initialises a new instance of <see cref="HistoryViewModel"/>.</summary>
    public HistoryViewModel(
        IBatchHistoryService historyService,
        ExportServiceFactory exportFactory,
        ILoggingService logger,
        AppSettings appSettings)
    {
        _historyService = historyService;
        _exportFactory  = exportFactory;
        _logger         = logger;
        _appSettings    = appSettings;
    }

    /// <summary>Loads all history entries from disk.</summary>
    [RelayCommand]
    public async Task LoadHistoryAsync(CancellationToken ct)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var all = await _historyService.GetAllAsync(ct);
            Entries = new ObservableCollection<BatchHistoryEntryViewModel>(
                all.Select(e => new BatchHistoryEntryViewModel(e)));
            OnPropertyChanged(nameof(HasEntries));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load history: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads the full results for the selected entry and raises <see cref="BatchLoaded"/>.
    /// </summary>
    [RelayCommand]
    private async Task LoadBatchAsync(CancellationToken ct)
    {
        if (SelectedEntry is null) return;

        try
        {
            var detail = await _historyService.LoadDetailAsync(SelectedEntry.BatchId, ct);
            if (detail is null)
            {
                WpfMessageBox.Show(
                    "This history entry's detail file could not be found.",
                    "History \u2014 Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            BatchLoaded?.Invoke(detail);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to load batch: {ex.Message}",
                "History \u2014 Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>Exports the selected history entry to CSV.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync(CancellationToken ct) =>
        await ExportSelectedAsync(Domain.Enums.ExportFormat.Csv, "csv", ct);

    /// <summary>Exports the selected history entry to Excel.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportExcelAsync(CancellationToken ct) =>
        await ExportSelectedAsync(Domain.Enums.ExportFormat.Excel, "xlsx", ct);

    /// <summary>Deletes the selected history entry.</summary>
    [RelayCommand]
    private async Task DeleteEntryAsync(CancellationToken ct)
    {
        if (SelectedEntry is null) return;

        var confirm = WpfMessageBox.Show(
            $"Delete the history entry from {SelectedEntry.CompletedAtText}?",
            "History \u2014 Delete Entry",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _historyService.DeleteAsync(SelectedEntry.BatchId, ct);
            Entries.Remove(SelectedEntry);
            SelectedEntry = null;
            OnPropertyChanged(nameof(HasEntries));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    /// <summary>Clears the entire history store.</summary>
    [RelayCommand]
    public async Task ClearHistoryAsync(CancellationToken ct)
    {
        if (Entries.Count == 0) return;

        var confirm = WpfMessageBox.Show(
            "Clear all batch history? This cannot be undone.",
            "History \u2014 Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _historyService.ClearAllAsync(ct);
            Entries.Clear();
            SelectedEntry = null;
            OnPropertyChanged(nameof(HasEntries));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear failed: {ex.Message}";
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task ExportSelectedAsync(
        Domain.Enums.ExportFormat format,
        string extension,
        CancellationToken ct)
    {
        if (SelectedEntry is null) return;

        try
        {
            var detail = await _historyService.LoadDetailAsync(SelectedEntry.BatchId, ct);
            if (detail is null || detail.Results.Count == 0)
            {
                WpfMessageBox.Show(
                    "No results available to export for this entry.",
                    "History \u2014 Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var outputDir = _appSettings.DefaultOutputDirectory;
            Directory.CreateDirectory(outputDir);

            var timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDir, $"ShipExtract_History_{timestamp}.{extension}");

            var exporter = _exportFactory.GetService(format);
            await exporter.ExportAsync(detail.Results, outputPath, _appSettings.MinimumConfidenceThreshold, ct);

            _logger.LogInformation("History export ({Format}) written to {Path}", format, outputPath);

            WpfMessageBox.Show(
                $"Export complete:\n{outputPath}",
                "History \u2014 Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"History export failed ({format})", ex);
            WpfMessageBox.Show(
                $"Export failed:\n{ex.Message}",
                "History \u2014 Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    partial void OnSelectedEntryChanged(BatchHistoryEntryViewModel? value)
    {
        OnPropertyChanged(nameof(CanExport));
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }
}
