using System.IO;
using System.Reflection;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Ocr;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.Helpers;

namespace ShipExtract.UI.ViewModels;

/// <summary>View model for the settings dialog.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ICredentialService _credentialService;
    private readonly ILoggingService _logger;
    private readonly AnthropicSettings _anthropicSettings;
    private readonly IAiExtractionService _aiService;
    private readonly IOllamaHealthService _ollamaHealthService;
    private readonly AppSettings _appSettings;
    private readonly IBatchHistoryService? _historyService;
    private readonly IUpdateService? _updateService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyIsSet))]
    [NotifyPropertyChangedFor(nameof(HasApiKey))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TessDataExists))]
    [NotifyPropertyChangedFor(nameof(TessDataStatusText))]
    private string _tessDataPath = string.Empty;

    [ObservableProperty] private string _defaultOutputDir = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedMessage))]
    private string _savedMessage = string.Empty;

    [ObservableProperty] private bool _isTestingConnection;
    [ObservableProperty] private string _testResultMessage = string.Empty;
    [ObservableProperty] private bool _testResultIsSuccess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnthropicSelected))]
    [NotifyPropertyChangedFor(nameof(IsOllamaSelected))]
    private AiProvider _selectedProvider;

    [ObservableProperty] private string _ollamaBaseUrl = "http://localhost:11434";
    [ObservableProperty] private string _ollamaModel = "llama3.1";
    [ObservableProperty] private string _ollamaStatusText = string.Empty;
    [ObservableProperty] private bool _ollamaIsRunning;
    [ObservableProperty] private List<string> _ollamaAvailableModels = [];
    [ObservableProperty] private bool _ollamaModelFound;
    [ObservableProperty] private string _ollamaModelStatusText = string.Empty;
    [ObservableProperty] private int _historyEntryCount;
    [ObservableProperty] private string _historyDirectory = string.Empty;
    [ObservableProperty] private int _customFieldCount;

    // ── Updates ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _checkForUpdates;
    [ObservableProperty] private string _currentVersionText = string.Empty;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _latestVersionText = string.Empty;
    [ObservableProperty] private bool _isCheckingForUpdates;

    // ── Export Quality ────────────────────────────────────────────────────────
    [ObservableProperty] private double _confidenceThreshold = 0.60;
    [ObservableProperty] private string _confidenceThresholdText = "60%";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportFilenamePreview))]
    private string _exportFilePrefix = "ShipExtract";

    // ── OCR Languages ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _ocrEngInstalled;
    [ObservableProperty] private bool _ocrDeuInstalled;
    [ObservableProperty] private bool _ocrDeuEnabled;
    [ObservableProperty] private bool _ocrFraInstalled;
    [ObservableProperty] private bool _ocrFraEnabled;
    [ObservableProperty] private string _ocrActiveLanguagesText = "Active: English";

    /// <summary>Gets whether a saved confirmation message is currently displayed.</summary>
    public bool HasSavedMessage => !string.IsNullOrEmpty(SavedMessage);

    /// <summary>Gets or sets whether the onboarding tour will be shown on next launch (inverse of OnboardingCompleted).</summary>
    public bool ShowOnboardingTour
    {
        get => !_appSettings.OnboardingCompleted;
        set
        {
            _appSettings.OnboardingCompleted = !value;
            OnPropertyChanged();
        }
    }

    /// <summary>Raised when the user requests an immediate tour replay; the handler should close Settings and show the tour.</summary>
    public event EventHandler? TourReplayRequested;

    /// <summary>Gets a preview of what the export filename will look like.</summary>
    public string ExportFilenamePreview
    {
        get
        {
            var prefix = SanitizeExportPrefix(ExportFilePrefix);
            return $"\u2192 {prefix}_20250611_143022.xlsx";
        }
    }

    /// <summary>Gets the model name from the current settings (read-only).</summary>
    public string ModelName => _anthropicSettings.Model;

    /// <summary>Gets whether the API key has been configured.</summary>
    public bool ApiKeyIsSet => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Gets whether the API key has been configured.</summary>
    public bool HasApiKey   => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Gets whether tessdata files are present.</summary>
    public bool TessDataExists =>
        !string.IsNullOrWhiteSpace(TessDataPath)
        && Directory.Exists(TessDataPath)
        && Directory.GetFiles(TessDataPath, "*.traineddata").Length > 0;

    /// <summary>Gets the tessdata status text.</summary>
    public string TessDataStatusText =>
        TessDataExists ? "OCR Ready" : "No language data found";

    /// <summary>Gets whether Anthropic is the selected provider.</summary>
    public bool IsAnthropicSelected => SelectedProvider == AiProvider.Anthropic;

    /// <summary>Gets whether Ollama is the selected provider.</summary>
    public bool IsOllamaSelected => SelectedProvider == AiProvider.Ollama;

    /// <summary>Initialises a new instance of <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        ICredentialService credentialService,
        ILoggingService logger,
        AnthropicSettings anthropicSettings,
        IAiExtractionService aiService,
        IOllamaHealthService ollamaHealthService,
        AppSettings appSettings,
        IBatchHistoryService? historyService = null,
        IUpdateService? updateService = null)
    {
        _settingsService      = settingsService;
        _credentialService    = credentialService;
        _logger               = logger;
        _anthropicSettings    = anthropicSettings;
        _aiService            = aiService;
        _ollamaHealthService  = ollamaHealthService;
        _appSettings          = appSettings;
        _historyService       = historyService;
        _updateService        = updateService;

        var settings = _settingsService.Load();
        TessDataPath     = settings.TessDataDirectory;
        DefaultOutputDir = settings.DefaultOutputDirectory;
        ApiKey           = _credentialService.GetApiKey() ?? string.Empty;
        SelectedProvider = _appSettings.AiProvider;
        OllamaBaseUrl    = _appSettings.OllamaBaseUrl;
        OllamaModel          = _appSettings.OllamaModel;
        HistoryDirectory     = _appSettings.HistoryDirectory;
        ConfidenceThreshold  = _appSettings.MinimumConfidenceThreshold;
        ConfidenceThresholdText = $"{_appSettings.MinimumConfidenceThreshold:P0}";
        ExportFilePrefix        = settings.ExportFilePrefix;

        // Load OCR language enabled states
        OcrDeuEnabled = _appSettings.OcrLanguages.Contains("deu", StringComparer.OrdinalIgnoreCase);
        OcrFraEnabled = _appSettings.OcrLanguages.Contains("fra", StringComparer.OrdinalIgnoreCase);

        CustomFieldCount = _appSettings.CustomFields.Count(f => f.IsEnabled);

        // Update section
        CheckForUpdates    = _appSettings.CheckForUpdatesOnStartup;
        CurrentVersionText = "v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");
        LatestVersionText  = string.Empty;
        UpdateAvailable    = false;

        RefreshOcrLanguageStatus();
        _ = LoadHistoryCountAsync();
    }

    /// <summary>Refreshes the custom field count from the live AppSettings.</summary>
    public void RefreshCustomFieldCount()
    {
        CustomFieldCount = _appSettings.CustomFields.Count(f => f.IsEnabled);
    }

    private async Task LoadHistoryCountAsync()
    {
        if (_historyService is null) return;
        try
        {
            var entries = await _historyService.GetAllAsync();
            HistoryEntryCount = entries.Count;
        }
        catch { /* best effort */ }
    }

    /// <summary>Refreshes OCR status properties.</summary>
    public void RefreshTessDataStatus()
    {
        OnPropertyChanged(nameof(TessDataExists));
        OnPropertyChanged(nameof(TessDataStatusText));
    }

    /// <summary>Saves the API key to the credential store and persists other settings.</summary>
    [RelayCommand]
    private async Task SaveSettings()
    {
        // Save API key to credential store (never log it)
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            _credentialService.SaveApiKey(ApiKey);
            _anthropicSettings.ApiKey = ApiKey; // update live singleton for current session
            _logger.LogInformation("API key updated in credential store.");
        }

        // Build OCR language list — English always first
        var langs = new List<string> { "eng" };
        if (OcrDeuEnabled && OcrDeuInstalled) langs.Add("deu");
        if (OcrFraEnabled && OcrFraInstalled) langs.Add("fra");

        // Update the live AppSettings singleton with all ViewModel values, then persist it once.
        _appSettings.TessDataDirectory          = TessDataPath;
        _appSettings.DefaultOutputDirectory     = DefaultOutputDir;
        _appSettings.AiProvider                 = SelectedProvider;
        _appSettings.OllamaBaseUrl              = OllamaBaseUrl;
        _appSettings.OllamaModel                = OllamaModel;
        _appSettings.MinimumConfidenceThreshold = ConfidenceThreshold;
        _appSettings.OcrLanguages               = langs;
        _appSettings.ExportFilePrefix           = ExportFilePrefix;
        // OnboardingCompleted is already up-to-date via ShowOnboardingTour setter
        // CheckForUpdatesOnStartup is kept in sync by OnCheckForUpdatesChanged partial

        _settingsService.Save(_appSettings);

        _logger.LogInformation("Settings saved.");
        SavedMessage = "Settings saved! OCR language changes take effect after restart.";

        await Task.Delay(3000);
        SavedMessage = string.Empty;
    }

    /// <summary>Opens a folder dialog to select the tessdata directory.</summary>
    [RelayCommand]
    private void BrowseTessData()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description  = "Select tessdata directory",
            SelectedPath = TessDataPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TessDataPath = dialog.SelectedPath;
            RefreshTessDataStatus();
            RefreshOcrLanguageStatus();
        }
    }

    /// <summary>Opens a folder dialog to select the default output directory.</summary>
    [RelayCommand]
    private void BrowseOutputDir()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description  = "Select default output directory",
            SelectedPath = DefaultOutputDir
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DefaultOutputDir = dialog.SelectedPath;
    }

    /// <summary>Checks the status of the local Ollama server.</summary>
    [RelayCommand]
    private async Task CheckOllamaStatus(CancellationToken ct)
    {
        OllamaStatusText = "Checking...";
        var result = await _ollamaHealthService.CheckAsync(OllamaBaseUrl, OllamaModel, ct);
        OllamaIsRunning       = result.IsRunning;
        OllamaAvailableModels = result.AvailableModels;
        OllamaModelFound      = result.ModelAvailable;
        OllamaStatusText      = result.IsRunning
            ? "\u2713 Ollama is running"
            : $"\u2717 Not running \u2014 {result.ErrorMessage}";
        OllamaModelStatusText = result.ModelAvailable
            ? $"\u2713 {OllamaModel} is available"
            : $"\u2717 Model not found. Run: ollama pull {OllamaModel}";
    }

    /// <summary>Tests the AI connection using the current provider settings.</summary>
    [RelayCommand]
    private async Task TestConnection(CancellationToken ct)
    {
        IsTestingConnection = true;
        TestResultMessage   = string.Empty;

        try
        {
            if (IsOllamaSelected)
            {
                var result = await _ollamaHealthService.CheckAsync(OllamaBaseUrl, OllamaModel, ct);
                TestResultIsSuccess = result.IsRunning;
                TestResultMessage   = result.IsRunning
                    ? "\u2713 Connected to Ollama successfully"
                    : $"\u2717 {result.ErrorMessage}";
            }
            else
            {
                var response = await _aiService.ExtractAsync(
                    "Test connection. Return minimal JSON.",
                    DocumentType.Unknown, ct);

                if (response.ErrorMessage?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true ||
                    response.ErrorMessage?.Contains("key", StringComparison.OrdinalIgnoreCase) == true ||
                    response.ErrorMessage?.Contains("401", StringComparison.OrdinalIgnoreCase) == true ||
                    response.ErrorMessage?.Contains("403", StringComparison.OrdinalIgnoreCase) == true)
                {
                    TestResultMessage   = $"\u2717 {ExceptionHelper.GetUserMessage(new Exception(response.ErrorMessage))}";
                    TestResultIsSuccess = false;
                }
                else
                {
                    TestResultMessage   = "\u2713 Connected successfully";
                    TestResultIsSuccess = true;
                }
            }
        }
        catch (Exception ex)
        {
            TestResultMessage   = $"\u2717 {ExceptionHelper.GetUserMessage(ex)}";
            TestResultIsSuccess = false;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    /// <summary>Clears the stored API key from Windows Credential Manager.</summary>
    [RelayCommand]
    private void ClearApiKey()
    {
        _credentialService.DeleteApiKey();
        ApiKey       = string.Empty;
        SavedMessage = "API key removed.";
        OnPropertyChanged(nameof(HasApiKey));
        OnPropertyChanged(nameof(ApiKeyIsSet));
    }

    /// <summary>Opens the tessdata folder in Windows Explorer.</summary>
    [RelayCommand]
    private void OpenTessDataFolder()
    {
        try
        {
            if (Directory.Exists(TessDataPath))
                System.Diagnostics.Process.Start("explorer.exe", TessDataPath);
        }
        catch { /* best effort */ }
    }

    /// <summary>Clears all batch history entries from disk.</summary>
    [RelayCommand]
    private async Task ClearHistory(CancellationToken ct)
    {
        if (_historyService is null) return;

        var confirm = WpfMessageBox.Show(
            "Clear all batch history? This cannot be undone.",
            "Settings \u2014 Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        await _historyService.ClearAllAsync(ct);
        HistoryEntryCount = 0;
        SavedMessage = "History cleared.";
        await Task.Delay(3000, ct);
        SavedMessage = string.Empty;
    }

    /// <summary>Updates the formatted threshold text whenever the slider value changes.</summary>
    partial void OnConfidenceThresholdChanged(double value)
    {
        ConfidenceThresholdText = $"{value:P0}";
    }

    /// <summary>Marks the tour as not completed and immediately raises <see cref="TourReplayRequested"/>.</summary>
    [RelayCommand]
    private void ReplayTour()
    {
        _appSettings.OnboardingCompleted = false;
        _settingsService.Save(_appSettings);
        OnPropertyChanged(nameof(ShowOnboardingTour));
        TourReplayRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string SanitizeExportPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return "ShipExtract";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(prefix.Select(c => invalid.Contains(c) ? '_' : c));
    }

    /// <summary>Checks which Tesseract language files are present in the current TessData directory.</summary>
    public void RefreshOcrLanguageStatus()
    {
        _logger.LogDebug(
            "Refreshing OCR status — TessDataPath: {Path}",
            TessDataPath);

        var knownLangs = new List<string> { "eng", "deu", "fra" };
        var status = OcrLanguageChecker.Check(TessDataPath ?? string.Empty, knownLangs);

        _logger.LogDebug(
            "OCR check — Available: {Avail}, Missing: {Miss}",
            string.Join(",", status.AvailableLanguages),
            string.Join(",", status.MissingLanguages));

        OcrEngInstalled = status.AvailableLanguages.Contains("eng", StringComparer.OrdinalIgnoreCase);
        OcrDeuInstalled = status.AvailableLanguages.Contains("deu", StringComparer.OrdinalIgnoreCase);
        OcrFraInstalled = status.AvailableLanguages.Contains("fra", StringComparer.OrdinalIgnoreCase);

        // If a language was enabled but is no longer installed, disable it
        if (!OcrDeuInstalled) OcrDeuEnabled = false;
        if (!OcrFraInstalled) OcrFraEnabled = false;

        var activeNames = new List<string> { "English" };
        if (OcrDeuEnabled && OcrDeuInstalled) activeNames.Add("German");
        if (OcrFraEnabled && OcrFraInstalled) activeNames.Add("French");
        OcrActiveLanguagesText = "Active: " + string.Join(" + ", activeNames);

        OnPropertyChanged(nameof(OcrEngInstalled));
        OnPropertyChanged(nameof(OcrDeuInstalled));
        OnPropertyChanged(nameof(OcrFraInstalled));
        OnPropertyChanged(nameof(OcrActiveLanguagesText));
    }

    /// <summary>Re-checks OCR language file availability.</summary>
    [RelayCommand]
    private void RefreshOcrStatus()
    {
        RefreshOcrLanguageStatus();
    }

    /// <summary>Opens the browser to download a specific language's traineddata file.</summary>
    [RelayCommand]
    private void DownloadLanguageFile(string languageCode)
    {
        var url = OcrLanguageChecker.GetDownloadUrl(languageCode);
        if (url is null) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    /// <summary>Checks for updates immediately when the user clicks the button in Settings.</summary>
    [RelayCommand]
    private async Task CheckForUpdatesNow(CancellationToken ct)
    {
        if (_updateService is null || IsCheckingForUpdates) return;
        IsCheckingForUpdates = true;
        LatestVersionText    = string.Empty;
        UpdateAvailable      = false;

        try
        {
            var info = await _updateService.CheckForUpdateAsync(ct);
            if (info is null)
            {
                LatestVersionText = "Could not reach update server.";
            }
            else if (info.IsUpdateAvailable)
            {
                LatestVersionText = $"v{info.LatestVersion} available";
                UpdateAvailable   = true;
                _appSettings.LastUpdateCheckDate = DateTime.UtcNow;
                _settingsService.Save(_appSettings);
            }
            else
            {
                LatestVersionText = "You are on the latest version.";
            }
        }
        catch
        {
            LatestVersionText = "Update check failed.";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>Keeps the AppSettings in sync when the checkbox changes before Save is clicked.</summary>
    partial void OnCheckForUpdatesChanged(bool value)
    {
        _appSettings.CheckForUpdatesOnStartup = value;
    }

    /// <summary>Opens the browser to download eng.traineddata.</summary>
    [RelayCommand]
    private void DownloadTessData()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata")
                { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }
}
