using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.UI.Views;

/// <summary>Editor window for managing user-defined custom extraction fields.</summary>
public partial class CustomFieldsWindow : Window
{
    private readonly AppSettings _appSettings;
    private readonly ISettingsService _settingsService;
    private readonly ObservableCollection<CustomField> _fields;

    private static readonly Dictionary<string, (string Hint, string Default)> BuiltInExamples = new()
    {
        ["PO Number"]       = ("Purchase order number, often labelled PO#, PO Number, or Purchase Order", string.Empty),
        ["Invoice Number"]  = ("Commercial invoice number, often labelled Invoice No, Invoice #, or Inv. No", string.Empty),
        ["Incoterms"]       = ("International commercial terms, e.g. EXW, FOB, CIF, DAP, DDP", string.Empty),
    };

    /// <summary>Initialises a new instance of <see cref="CustomFieldsWindow"/>.</summary>
    public CustomFieldsWindow(AppSettings appSettings, ISettingsService settingsService)
    {
        InitializeComponent();
        _appSettings    = appSettings;
        _settingsService = settingsService;

        // Deep-copy so Cancel truly cancels
        _fields = new ObservableCollection<CustomField>(
            appSettings.CustomFields.Select(f => new CustomField
            {
                Id             = f.Id,
                Name           = f.Name,
                ExtractionHint = f.ExtractionHint,
                DefaultValue   = f.DefaultValue,
                IsEnabled      = f.IsEnabled,
                SortOrder      = f.SortOrder
            }));

        FieldsGrid.ItemsSource = _fields;
        _fields.CollectionChanged += (_, _) => RefreshState();
        RefreshState();
    }

    private void RefreshState()
    {
        ManyFieldsWarning.Visibility = _fields.Count > 10
            ? Visibility.Visible
            : Visibility.Collapsed;

        var activeFields = _fields.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Name)).ToList();
        PromptPreview.Text = activeFields.Count == 0
            ? "(no enabled custom fields)"
            : ExtractionPromptBuilder.BuildCustomFieldsSection(activeFields);
    }

    private void AddField_Click(object sender, RoutedEventArgs e)
    {
        var field = new CustomField { SortOrder = _fields.Count };
        _fields.Add(field);
        FieldsGrid.ScrollIntoView(field);
        FieldsGrid.SelectedItem = field;
        RefreshState();
    }

    private void ExampleField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string name
            && BuiltInExamples.TryGetValue(name, out var example))
        {
            // Only add if not already present
            if (_fields.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                return;

            var field = new CustomField
            {
                Name           = name,
                ExtractionHint = example.Hint,
                DefaultValue   = example.Default,
                IsEnabled      = true,
                SortOrder      = _fields.Count
            };
            _fields.Add(field);
            RefreshState();
        }
    }

    private void DeleteField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is CustomField field)
        {
            _fields.Remove(field);
            // Re-assign sort orders
            for (int i = 0; i < _fields.Count; i++)
                _fields[i].SortOrder = i;
            RefreshState();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        FieldsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        // Re-assign sort orders before saving
        for (int i = 0; i < _fields.Count; i++)
            _fields[i].SortOrder = i;

        _appSettings.CustomFields = _fields.ToList();
        _settingsService.Save(_appSettings);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
