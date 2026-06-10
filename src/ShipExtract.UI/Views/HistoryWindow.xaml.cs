using System.IO;
using System.Windows;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.UI.ViewModels;

namespace ShipExtract.UI.Views;

/// <summary>Code-behind for the batch history window.</summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _vm;

    /// <summary>
    /// Raised when the user clicks "Load to Main Window" so the calling window
    /// can restore the batch into the queue and result panel.
    /// </summary>
    public event Action<BatchHistoryEntry>? BatchLoaded;

    /// <summary>Initialises a new instance of <see cref="HistoryWindow"/>.</summary>
    public HistoryWindow(HistoryViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        _vm.BatchLoaded += OnBatchLoaded;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    /// <inheritdoc/>
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await _vm.LoadHistoryCommand.ExecuteAsync(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OnBatchLoaded(BatchHistoryEntry entry)
    {
        BatchLoaded?.Invoke(entry);
        Close();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryViewModel.SelectedEntry))
            _ = LoadDetailGridAsync();
    }

    // Loads the per-batch detail file and populates the results DataGrid.
    private async Task LoadDetailGridAsync()
    {
        ResultsGrid.ItemsSource = null;

        var selected = _vm.SelectedEntry;
        if (selected is null) return;

        var detail = _vm.LoadHistoryCommand.IsRunning
            ? null
            : await Task.Run(() => LoadDetail(selected.BatchId));

        if (detail is not null)
            ResultsGrid.ItemsSource = detail.Results.Select(r => new ResultRow(r)).ToList();
    }

    private BatchHistoryEntry? LoadDetail(Guid batchId)
    {
        // Synchronously resolve service — history window is UI only
        var svc = DependencyInjection.ServiceLocator.Get<Domain.Interfaces.IBatchHistoryService>();
        return svc.LoadDetailAsync(batchId).GetAwaiter().GetResult();
    }

    // ── Nested row DTO for the DataGrid ──────────────────────────────────────

    private sealed class ResultRow
    {
        public string FileName       { get; }
        public string StatusText     { get; }
        public string CarrierText    { get; }
        public string TrackingNumber { get; }
        public string ShipperName    { get; }
        public string ConsigneeName  { get; }

        public ResultRow(ProcessingResult r)
        {
            FileName       = Path.GetFileName(r.SourceFilePath);
            StatusText     = r.Status.ToString();
            CarrierText    = r.DetectedCarrier == CarrierType.Unknown ? "\u2014" : r.DetectedCarrier.ToString();
            TrackingNumber = r.Record?.TrackingNumber ?? string.Empty;
            ShipperName    = r.Record?.ShipperName    ?? string.Empty;
            ConsigneeName  = r.Record?.ConsigneeName  ?? string.Empty;
        }
    }
}
