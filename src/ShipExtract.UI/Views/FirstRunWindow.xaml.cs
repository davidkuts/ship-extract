using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.ViewModels;

namespace ShipExtract.UI.Views;

/// <summary>First-run setup dialog shown when no API key has been configured.</summary>
public partial class FirstRunWindow : Window
{
    private readonly ICredentialService _credentialService;
    private readonly MainViewModel _mainViewModel;
    private readonly string _tessDataPath;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _appSettings;

    private AiProvider _selectedProvider = AiProvider.Ollama;

    /// <summary>Initialises a new instance of <see cref="FirstRunWindow"/>.</summary>
    public FirstRunWindow(
        ICredentialService credentialService,
        MainViewModel mainViewModel,
        string tessDataPath,
        ISettingsService settingsService,
        AppSettings appSettings)
    {
        InitializeComponent();
        _credentialService = credentialService;
        _mainViewModel     = mainViewModel;
        _tessDataPath      = tessDataPath;
        _settingsService   = settingsService;
        _appSettings       = appSettings;

        TessPathBox.Text = _tessDataPath;
        RefreshTessDataStatus();
        UpdateProviderUI();
    }

    private void RefreshTessDataStatus()
    {
        bool found = !string.IsNullOrWhiteSpace(_tessDataPath)
                     && Directory.Exists(_tessDataPath)
                     && Directory.GetFiles(_tessDataPath, "*.traineddata").Length > 0;

        TessStatusText.Text       = found ? "Found \u2014 OCR enabled" : "Not found yet \u2014 OCR will be unavailable";
        TessStatusIcon.Foreground = found
            ? new WpfSolidColorBrush(WpfColor.FromRgb(0x21, 0x73, 0x46))   // SuccessBrush
            : new WpfSolidColorBrush(WpfColor.FromRgb(0xC5, 0x5A, 0x11));  // WarningBrush
    }

    private void UpdateProviderUI()
    {
        var successBrush    = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["SuccessBrush"];
        var primaryBrush    = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["PrimaryBrush"];
        var surfaceDarkBrush = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["SurfaceDarkBrush"];

        OllamaProviderCard.BorderBrush     = _selectedProvider == AiProvider.Ollama ? successBrush : surfaceDarkBrush;
        OllamaProviderCard.BorderThickness = _selectedProvider == AiProvider.Ollama ? new Thickness(2) : new Thickness(1);

        AnthropicProviderCard.BorderBrush     = _selectedProvider == AiProvider.Anthropic ? primaryBrush : surfaceDarkBrush;
        AnthropicProviderCard.BorderThickness = _selectedProvider == AiProvider.Anthropic ? new Thickness(2) : new Thickness(1);

        ApiKeySection.Visibility  = _selectedProvider == AiProvider.Anthropic ? Visibility.Visible : Visibility.Collapsed;
        TessStepLabel.Text        = _selectedProvider == AiProvider.Anthropic
            ? "Step 3: OCR Support (Optional)"
            : "Step 2: OCR Support (Optional)";

        // Update validation text label for context
        ValidationText.Text = _selectedProvider == AiProvider.Anthropic
            ? "Please enter your API key"
            : string.Empty;
    }

    private void OllamaCard_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedProvider = AiProvider.Ollama;
        UpdateProviderUI();
    }

    private void AnthropicCard_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedProvider = AiProvider.Anthropic;
        UpdateProviderUI();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProvider == AiProvider.Anthropic)
        {
            var apiKey = ApiKeyBox.Password.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                ValidationText.Visibility = Visibility.Visible;
                ApiKeyBox.Focus();
                return;
            }
            ValidationText.Visibility = Visibility.Collapsed;
            _credentialService.SaveApiKey(apiKey);
        }

        _appSettings.AiProvider = _selectedProvider;
        _settingsService.Save(_appSettings);
        _mainViewModel.RefreshWarnings();
        Close();
    }

    private void GetApiKey_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://console.anthropic.com") { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    private void DownloadTessdata_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata")
                { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    private void CheckTessdata_Click(object sender, RoutedEventArgs e)
    {
        RefreshTessDataStatus();
        _mainViewModel.RefreshWarnings();
    }
}
