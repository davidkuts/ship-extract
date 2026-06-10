using System.IO;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
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

    /// <summary>Gets whether a saved confirmation message is currently displayed.</summary>
    public bool HasSavedMessage => !string.IsNullOrEmpty(SavedMessage);

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
        IBatchHistoryService? historyService = null)
    {
        _settingsService      = settingsService;
        _credentialService    = credentialService;
        _logger               = logger;
        _anthropicSettings    = anthropicSettings;
        _aiService            = aiService;
        _ollamaHealthService  = ollamaHealthService;
        _appSettings          = appSettings;
        _historyService       = historyService;

        var settings = _settingsService.Load();
        TessDataPath     = settings.TessDataDirectory;
        DefaultOutputDir = settings.DefaultOutputDirectory;
        ApiKey           = _credentialService.GetApiKey() ?? string.Empty;
        SelectedProvider = _appSettings.AiProvider;
        OllamaBaseUrl    = _appSettings.OllamaBaseUrl;
        OllamaModel      = _appSettings.OllamaModel;
        HistoryDirectory = _appSettings.HistoryDirectory;

        _ = LoadHistoryCountAsync();
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

        var settings = _settingsService.Load();
        settings.TessDataDirectory      = TessDataPath;
        settings.DefaultOutputDirectory = DefaultOutputDir;
        settings.AiProvider             = SelectedProvider;
        settings.OllamaBaseUrl          = OllamaBaseUrl;
        settings.OllamaModel            = OllamaModel;
        _settingsService.Save(settings);

        // Update the live AppSettings singleton so changes take effect immediately
        _appSettings.AiProvider    = SelectedProvider;
        _appSettings.OllamaBaseUrl = OllamaBaseUrl;
        _appSettings.OllamaModel   = OllamaModel;

        _logger.LogInformation("Settings saved.");
        SavedMessage = "Settings saved!";

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
