using System.Windows;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.Controls;
using ShipExtract.UI.DependencyInjection;
using ShipExtract.UI.ViewModels;

namespace ShipExtract.UI.Views;

/// <summary>Code-behind for the main application window.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    /// <summary>Initialises a new instance of <see cref="MainWindow"/>.</summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // Show FirstRunWindow after this window has loaded and rendered
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var credentialService = ServiceLocator.Get<ICredentialService>();
        var appSettings       = ServiceLocator.Get<AppSettings>();

        // Show first-run when Anthropic is selected and no API key is stored,
        // or when no provider preference has been saved yet (i.e. first real launch).
        bool needsSetup = appSettings.AiProvider == ShipExtract.Infrastructure.AI.AiProvider.Anthropic
            && string.IsNullOrEmpty(credentialService.GetApiKey());

        if (needsSetup)
        {
            var settingsService = ServiceLocator.Get<ISettingsService>();
            var firstRun = new FirstRunWindow(
                credentialService,
                _vm,
                appSettings.TessDataDirectory,
                settingsService,
                appSettings)
            {
                Owner = this
            };
            firstRun.ShowDialog();
        }
    }

    private void DropZone_FilesSelected(object sender, FilesSelectedEventArgs e)
    {
        _vm.AddFilesCommand.Execute(e.FilePaths);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsVm = ServiceLocator.Get<SettingsViewModel>();
        var win = new SettingsWindow(settingsVm) { Owner = this };
        win.ShowDialog();
        _vm.RefreshWarnings();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void CheckOllamaButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshWarnings();
    }
}
