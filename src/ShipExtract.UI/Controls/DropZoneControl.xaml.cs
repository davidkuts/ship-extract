using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ShipExtract.UI.Controls;

/// <summary>
/// A drag-and-drop zone that accepts PDF files and exposes them via
/// a <see cref="FilesSelected"/> routed event.
/// </summary>
public partial class DropZoneControl : WpfUserControl
{
    /// <summary>Identifies the <see cref="FilesDropped"/> dependency property.</summary>
    public static readonly DependencyProperty FilesDroppedProperty =
        DependencyProperty.Register(
            nameof(FilesDropped),
            typeof(IReadOnlyList<string>),
            typeof(DropZoneControl),
            new PropertyMetadata(null));

    /// <summary>Routed event raised when the user selects one or more PDF files.</summary>
    public static readonly RoutedEvent FilesSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(FilesSelected),
            RoutingStrategy.Bubble,
            typeof(FilesSelectedEventHandler),
            typeof(DropZoneControl));

    // Internal bool for drag-over visual state
    private bool _isDragOver;

    /// <summary>Gets or sets the list of files most recently dropped or selected.</summary>
    public IReadOnlyList<string>? FilesDropped
    {
        get => (IReadOnlyList<string>?)GetValue(FilesDroppedProperty);
        set => SetValue(FilesDroppedProperty, value);
    }

    /// <summary>Raised when PDF files are selected (via drop or file dialog).</summary>
    public event FilesSelectedEventHandler FilesSelected
    {
        add    => AddHandler(FilesSelectedEvent, value);
        remove => RemoveHandler(FilesSelectedEvent, value);
    }

    /// <summary>Gets whether a drag operation is currently over this control.</summary>
    public bool IsDragOver
    {
        get => _isDragOver;
        private set { _isDragOver = value; InvalidateVisual(); }
    }

    /// <summary>Initialises a new instance of <see cref="DropZoneControl"/>.</summary>
    public DropZoneControl()
    {
        InitializeComponent();
        DragOver          += OnDragOver;
        DragLeave         += OnDragLeave;
        Drop              += OnDrop;
        MouseLeftButtonUp += OnClick;
    }

    private void OnDragOver(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            e.Effects  = WpfDragDropEffects.Copy;
            IsDragOver = true;
            DropBorder.BorderBrush = (System.Windows.Media.SolidColorBrush)
                WpfApplication.Current.Resources["AccentBrush"];
        }
        else
        {
            e.Effects = WpfDragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, WpfDragEventArgs e)
    {
        IsDragOver = false;
        DropBorder.BorderBrush = (System.Windows.Media.SolidColorBrush)
            WpfApplication.Current.Resources["PrimaryLightBrush"];
        DropBorder.Background = (System.Windows.Media.SolidColorBrush)
            WpfApplication.Current.Resources["SurfaceBrush"];
    }

    private void OnDrop(object sender, WpfDragEventArgs e)
    {
        IsDragOver = false;
        DropBorder.BorderBrush = (System.Windows.Media.SolidColorBrush)
            WpfApplication.Current.Resources["PrimaryLightBrush"];
        DropBorder.Background = (System.Windows.Media.SolidColorBrush)
            WpfApplication.Current.Resources["SurfaceBrush"];

        if (e.Data.GetData(WpfDataFormats.FileDrop) is string[] paths)
        {
            var pdfs = paths
                .Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (pdfs.Count > 0)
                RaiseFilesSelected(pdfs);
        }
    }

    private void OnClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title       = "Select PDF Files",
            Filter      = "PDF Files (*.pdf)|*.pdf",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
            RaiseFilesSelected(dialog.FileNames.ToList());
    }

    private void RaiseFilesSelected(IReadOnlyList<string> files)
    {
        FilesDropped = files;
        var args = new FilesSelectedEventArgs(FilesSelectedEvent, this, files);
        RaiseEvent(args);
    }
}

/// <summary>Delegate for the <see cref="DropZoneControl.FilesSelected"/> routed event.</summary>
public delegate void FilesSelectedEventHandler(object sender, FilesSelectedEventArgs e);

/// <summary>Event arguments for the <see cref="DropZoneControl.FilesSelected"/> event.</summary>
public sealed class FilesSelectedEventArgs : RoutedEventArgs
{
    /// <summary>Gets the list of selected PDF file paths.</summary>
    public IReadOnlyList<string> FilePaths { get; }

    /// <summary>Initialises a new instance of <see cref="FilesSelectedEventArgs"/>.</summary>
    public FilesSelectedEventArgs(RoutedEvent routedEvent, object source, IReadOnlyList<string> filePaths)
        : base(routedEvent, source)
    {
        FilePaths = filePaths;
    }
}
