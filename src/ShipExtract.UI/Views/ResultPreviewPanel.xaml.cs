using System.Windows;
using System.Windows.Controls;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Errors;
using WpfApplication  = System.Windows.Application;
using WpfBrush        = System.Windows.Media.Brush;
using WpfTabControl   = System.Windows.Controls.TabControl;
using WpfTabItem      = System.Windows.Controls.TabItem;
using WpfUserControl  = System.Windows.Controls.UserControl;

namespace ShipExtract.UI.Views;

/// <summary>Displays the extracted fields of a single <see cref="ProcessingResult"/>.</summary>
public partial class ResultPreviewPanel : WpfUserControl
{
    /// <summary>Identifies the <see cref="Result"/> dependency property.</summary>
    public static readonly DependencyProperty ResultProperty =
        DependencyProperty.Register(
            nameof(Result),
            typeof(ProcessingResult),
            typeof(ResultPreviewPanel),
            new PropertyMetadata(null, OnResultChanged));

    /// <summary>Gets or sets the processing result to display.</summary>
    public ProcessingResult? Result
    {
        get => (ProcessingResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    /// <summary>Initialises a new instance of <see cref="ResultPreviewPanel"/>.</summary>
    public ResultPreviewPanel() => InitializeComponent();

    private static void OnResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResultPreviewPanel panel)
            panel.Rebuild((ProcessingResult?)e.NewValue);
    }

    private void Rebuild(ProcessingResult? result)
    {
        ContentPanel.Children.Clear();
        if (result is null) return;

        if (result.Record is not null)
        {
            var r = result.Record;

            AddSection("Shipment Identity", [
                ("Tracking Number",  r.TrackingNumber),
                ("House Bill",       r.HouseBillNumber),
                ("Master Bill",      r.MasterBillNumber),
                ("Carrier",          r.CarrierName),
                ("Service Type",     r.ServiceType),
                ("Ship Date",        r.ShipDate?.ToString("yyyy-MM-dd")),
                ("Est. Delivery",    r.EstimatedDeliveryDate?.ToString("yyyy-MM-dd")),
            ]);

            AddSection("Shipper", [
                ("Name",        r.ShipperName),
                ("Address",     r.ShipperAddress),
                ("City",        r.ShipperCity),
                ("Country",     r.ShipperCountry),
                ("Postal Code", r.ShipperPostalCode),
            ]);

            AddSection("Consignee", [
                ("Name",        r.ConsigneeName),
                ("Address",     r.ConsigneeAddress),
                ("City",        r.ConsigneeCity),
                ("Country",     r.ConsigneeCountry),
                ("Postal Code", r.ConsigneePostalCode),
            ]);

            AddSection("Cargo", [
                ("Pieces",       r.NumberOfPieces?.ToString()),
                ("Gross Weight", r.GrossWeightKg.HasValue ? $"{r.GrossWeightKg:0.##} kg" : null),
                ("Volume",       r.VolumeM3.HasValue      ? $"{r.VolumeM3:0.##} m\u00B3"  : null),
                ("Description",  r.Description),
                ("HS Code",      r.HsCode),
            ]);

            AddSection("Financial", [
                ("Declared Value", r.DeclaredValue.HasValue ? $"{r.DeclaredValue:0.##} {r.Currency}" : null),
                ("Freight Cost",   r.FreightCost.HasValue   ? $"{r.FreightCost:0.##} {r.Currency}"   : null),
            ]);

            var ppReport  = result.PreProcessingReport;
            var ppSummary = ppReport == null ? null :
                ppReport.CharactersRemoved > 0
                    ? $"Cleaned {ppReport.CharactersRemoved} chars ({ppReport.ReductionPercent:F1}% reduction)"
                    : "No cleaning needed";
            var ppSteps = ppReport?.CharactersRemoved > 0
                ? string.Join(", ", ppReport.StepsApplied)
                : null;

            var carrierText = result.DetectedCarrier != ShipExtract.Domain.Enums.CarrierType.Unknown
                ? result.DetectedCarrier.ToString()
                : null;

            AddSection("Extraction Metadata", [
                ("Document Type",     r.DocumentType.ToString()),
                ("Detected Carrier",  carrierText),
                ("Confidence",        $"{r.ConfidenceScore:P0}"),
                ("OCR Used",          result.UsedOcrFallback ? "Yes" : "No"),
                ("Duration",          $"{result.ProcessingDuration.TotalSeconds:0.#}s"),
                ("Source File",       r.SourceFileName),
                ("Pre-Processing",    ppSummary),
                ("Steps Applied",     ppSteps),
            ]);
        }

        if (result.Errors.Count > 0)
            AddErrorsSection(result);

        AddRawTextSection(result);
    }

    private void AddErrorsSection(ProcessingResult result)
    {
        var expander = new Expander
        {
            Header     = "Errors & Diagnostics",
            IsExpanded = true,
            Margin     = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.SemiBold,
            FontSize   = 12,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextPrimaryBrush"]
        };

        var panel = new StackPanel { Margin = new Thickness(4, 4, 4, 0) };

        foreach (var error in result.Errors)
        {
            // User-facing message
            var userMsg = new TextBlock
            {
                Text         = UserFacingMessages.GetMessage(error.Code),
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 12,
                Foreground   = (WpfBrush)WpfApplication.Current.Resources["TextPrimaryBrush"],
                Margin       = new Thickness(0, 0, 0, 2)
            };
            panel.Children.Add(userMsg);

            // Technical details (collapsed by default)
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                var techExpander = new Expander
                {
                    Header     = "Technical details",
                    IsExpanded = false,
                    Margin     = new Thickness(0, 0, 0, 6),
                    FontWeight = FontWeights.Normal,
                    FontSize   = 11,
                    Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                    Content    = new TextBlock
                    {
                        Text         = error.Message,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily   = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize     = 10,
                        Margin       = new Thickness(4),
                        Foreground   = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"]
                    }
                };
                panel.Children.Add(techExpander);
            }
        }

        expander.Content = panel;
        ContentPanel.Children.Add(expander);
    }

    private void AddRawTextSection(ProcessingResult result)
    {
        var rawText     = result.ExtractedRawText;
        var cleanedText = result.PreProcessingReport?.CleanedText;
        var hasRaw      = !string.IsNullOrEmpty(rawText);

        // Tab control to switch between Original and Cleaned views.
        WpfTabControl? tabControl = null;

        // Header grid: label on left, Copy button on right.
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLabel = new TextBlock
        {
            Text              = "Raw & Cleaned Text",
            FontSize          = 11,
            FontStyle         = FontStyles.Italic,
            Foreground        = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerLabel, 0);
        headerGrid.Children.Add(headerLabel);

        var copyButton = new System.Windows.Controls.Button
        {
            Content           = "Copy",
            Padding           = new Thickness(4, 2, 4, 2),
            FontSize          = 10,
            FontWeight        = FontWeights.Normal,
            Background        = System.Windows.Media.Brushes.Transparent,
            BorderThickness   = new Thickness(1),
            Cursor            = System.Windows.Input.Cursors.Hand,
            Visibility        = hasRaw ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
        copyButton.Click += (_, _) =>
        {
            if (tabControl is null) return;
            var text = tabControl.SelectedIndex == 1
                ? (string.IsNullOrEmpty(cleanedText) ? rawText : cleanedText)
                : rawText;
            if (!string.IsNullOrEmpty(text))
                System.Windows.Clipboard.SetText(text);
        };
        Grid.SetColumn(copyButton, 1);
        headerGrid.Children.Add(copyButton);

        // Build content.
        FrameworkElement content;
        if (!hasRaw)
        {
            content = new TextBlock
            {
                Text       = "No text extracted",
                FontStyle  = FontStyles.Italic,
                FontSize   = 11,
                Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                Margin     = new Thickness(4)
            };
        }
        else
        {
            tabControl = new WpfTabControl { BorderThickness = new Thickness(0) };

            tabControl.Items.Add(BuildTextTab("Original", rawText));
            tabControl.Items.Add(BuildTextTab(
                "Cleaned",
                string.IsNullOrEmpty(cleanedText) ? null : cleanedText,
                emptyMessage: "Same as original"));

            content = tabControl;
        }

        var expander = new Expander
        {
            Header     = headerGrid,
            IsExpanded = false,
            Margin     = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.Normal,
            FontSize   = 12,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextPrimaryBrush"],
            Content    = content
        };

        ContentPanel.Children.Add(expander);
    }

    private static WpfTabItem BuildTextTab(string header, string? text, string emptyMessage = "No text extracted")
    {
        FrameworkElement tabContent;
        if (string.IsNullOrEmpty(text))
        {
            tabContent = new TextBlock
            {
                Text       = emptyMessage,
                FontStyle  = FontStyles.Italic,
                FontSize   = 11,
                Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                Margin     = new Thickness(8)
            };
        }
        else
        {
            var textBox = new System.Windows.Controls.TextBox
            {
                Text            = text,
                IsReadOnly      = true,
                TextWrapping    = TextWrapping.Wrap,
                FontFamily      = new System.Windows.Media.FontFamily("Consolas"),
                FontSize        = 11,
                Background      = (WpfBrush)WpfApplication.Current.Resources["SurfaceBrush"],
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(8)
            };
            tabContent = new ScrollViewer
            {
                Content                     = textBox,
                MaxHeight                   = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        return new WpfTabItem { Header = header, Content = tabContent };
    }

    private void AddSection(string title, (string Label, string? Value)[] fields)
    {
        // Skip section if all values are null/empty
        if (fields.All(f => string.IsNullOrWhiteSpace(f.Value))) return;

        var expander = new Expander
        {
            Header     = title,
            IsExpanded = true,
            Margin     = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.SemiBold,
            FontSize   = 12,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextPrimaryBrush"]
        };

        var grid = new Grid { Margin = new Thickness(4, 4, 4, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        foreach (var (label, value) in fields)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text       = label,
                Style      = (Style)Resources["FieldLabel"],
                FontWeight = FontWeights.Normal
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            bool hasValue = !string.IsNullOrWhiteSpace(value);
            var valueBlock = new TextBlock
            {
                Text       = hasValue ? value : "\u2014",
                Style      = (Style)Resources["FieldValue"],
                FontStyle  = hasValue ? FontStyles.Normal : FontStyles.Italic,
                Foreground = hasValue
                    ? (WpfBrush)WpfApplication.Current.Resources["TextPrimaryBrush"]
                    : (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"]
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            row++;
        }

        expander.Content = grid;
        ContentPanel.Children.Add(expander);
    }
}
