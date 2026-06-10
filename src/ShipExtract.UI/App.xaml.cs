using System.IO;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipExtract.Application.DependencyInjection;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.DependencyInjection;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.DependencyInjection;
using ShipExtract.UI.Helpers;
using ShipExtract.UI.ViewModels;
using ShipExtract.UI.Views;

namespace ShipExtract.UI;

/// <summary>Application entry point and host bootstrapper.</summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <inheritdoc/>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Settings-first bootstrap (appSettings must be in scope for the error handler closure)
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShipExtract");

        var settingsService = new SettingsService(appDataRoot);
        bool isFirstLaunch = !File.Exists(settingsService.SettingsFilePath);
        var appSettings = settingsService.Load();

        if (isFirstLaunch)
            settingsService.Save(appSettings);

        DirectoryBootstrapper.EnsureDirectories(appSettings);

        // Global error handlers (appSettings captured in closure)
        DispatcherUnhandledException += (_, args) =>
        {
            var userMsg = ExceptionHelper.GetUserMessage(args.Exception);
            try
            {
                var logger = ServiceLocator.Get<ShipExtract.Domain.Interfaces.ILoggingService>();
                logger.LogError("Unhandled UI exception", args.Exception);
            }
            catch { /* logger not yet available */ }

            var logDir = appSettings?.LogDirectory ?? "the log directory";
            WpfMessageBox.Show(
                $"An unexpected error occurred:\n\n{userMsg}\n\n" +
                $"Details have been logged to:\n{logDir}\n\n" +
                "The application will continue running.",
                "ShipExtract \u2014 Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                var logger = ServiceLocator.Get<ShipExtract.Domain.Interfaces.ILoggingService>();
                logger.LogError("Unobserved task exception", args.Exception);
            }
            catch { /* best effort */ }

            args.SetObserved();
        };

        var anthropicSettings = new AnthropicSettings
        {
            ApiKey    = string.Empty, // loaded from Credential Manager at call time
            Model     = appSettings.Model,
            MaxTokens = appSettings.MaxTokens
        };

        // Build host
        int maxConcurrency = appSettings.AiProvider == AiProvider.Ollama ? 1 : 4;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddApplicationServices(maxConcurrency);
                services.AddInfrastructureServices(
                    appSettings.LogDirectory,
                    appSettings.TessDataDirectory,
                    anthropicSettings,
                    appDataRoot,
                    appSettings);

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<StatusBarViewModel>();

                // Views
                services.AddTransient<MainWindow>();
                services.AddTransient<SettingsWindow>();
                services.AddTransient<AboutWindow>();
            })
            .Build();

        await _host.StartAsync();
        ServiceLocator.Initialize(_host.Services);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <inheritdoc/>
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
