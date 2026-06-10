using System.Text.Json;

namespace ShipExtract.Infrastructure.Settings;

/// <summary>Contract for loading and persisting application settings.</summary>
public interface ISettingsService
{
    /// <summary>Gets the full path to the settings JSON file.</summary>
    string SettingsFilePath { get; }

    /// <summary>Loads settings from disk, returning defaults if the file does not exist.</summary>
    AppSettings Load();

    /// <summary>Persists settings to disk.</summary>
    void Save(AppSettings settings);
}

/// <summary>Implements <see cref="ISettingsService"/> using a JSON file in the app data directory.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly string _appDataRoot;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initialises a new instance of <see cref="SettingsService"/>.</summary>
    /// <param name="appDataRoot">Root application data directory (e.g. %APPDATA%\ShipExtract).</param>
    public SettingsService(string appDataRoot)
    {
        _appDataRoot  = appDataRoot;
        _settingsPath = Path.Combine(appDataRoot, "settings.json");
    }

    /// <inheritdoc/>
    public string SettingsFilePath => _settingsPath;

    /// <inheritdoc/>
    public AppSettings Load()
    {
        AppSettings settings;

        if (!File.Exists(_settingsPath))
        {
            settings = BuildDefaults();
        }
        else
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? BuildDefaults();
            }
            catch
            {
                settings = BuildDefaults();
            }
        }

        // Expand any remaining environment tokens
        settings.LogDirectory          = Expand(settings.LogDirectory);
        settings.TessDataDirectory     = Expand(settings.TessDataDirectory);
        settings.DefaultOutputDirectory = Expand(settings.DefaultOutputDirectory);

        return settings;
    }

    /// <inheritdoc/>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_appDataRoot);
        // Never persist the API key — it lives in Credential Manager
        var toSave = new AppSettings
        {
            ApiKey                 = string.Empty,
            Model                  = settings.Model,
            MaxTokens              = settings.MaxTokens,
            TessDataDirectory      = settings.TessDataDirectory,
            DefaultOutputDirectory = settings.DefaultOutputDirectory,
            LogDirectory           = settings.LogDirectory,
            AiProvider             = settings.AiProvider,
            OllamaBaseUrl          = settings.OllamaBaseUrl,
            OllamaModel            = settings.OllamaModel
        };
        var json = JsonSerializer.Serialize(toSave, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private AppSettings BuildDefaults() => new()
    {
        TessDataDirectory      = Path.Combine(_appDataRoot, "tessdata"),
        DefaultOutputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShipExtract"),
        LogDirectory           = Path.Combine(_appDataRoot, "logs")
    };

    private static string Expand(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty
            : Environment.ExpandEnvironmentVariables(value);
}
