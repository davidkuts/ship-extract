using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.DependencyInjection;
using ShipExtract.UI.ViewModels;

namespace ShipExtract.UI.Views;

/// <summary>Code-behind for the Settings dialog.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _isApiKeyVisible = false;

    /// <summary>Initialises a new instance of <see cref="SettingsWindow"/>.</summary>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // Initialise the PasswordBox from the view model
        ApiKeyBox.Password = _vm.ApiKey;

        // Wire the tour-replay event: close Settings and ask MainWindow to show the overlay
        _vm.TourReplayRequested += (_, _) =>
        {
            Close();
            (Owner as MainWindow)?.ShowOnboardingOverlay();
        };
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.ApiKey = ApiKeyBox.Password;
    }

    private void ApiKeyRevealBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.ApiKey = ApiKeyRevealBox.Text;
    }

    private void ShowHideApiKey_Click(object sender, RoutedEventArgs e)
    {
        _isApiKeyVisible = !_isApiKeyVisible;
        if (_isApiKeyVisible)
        {
            ApiKeyRevealBox.Text       = ApiKeyBox.Password;
            ApiKeyRevealBox.Visibility = Visibility.Visible;
            ApiKeyBox.Visibility       = Visibility.Collapsed;
            ShowHideButton.Content     = "Hide";
        }
        else
        {
            ApiKeyBox.Password         = ApiKeyRevealBox.Text;
            ApiKeyBox.Visibility       = Visibility.Visible;
            ApiKeyRevealBox.Visibility = Visibility.Collapsed;
            ShowHideButton.Content     = "Show";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Execute the command (async fire-and-forget via relay command)
        _vm.SaveSettingsCommand.Execute(null);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AnthropicCard_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.SelectedProvider = AiProvider.Anthropic;
    }

    private void OllamaCard_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.SelectedProvider = AiProvider.Ollama;
    }

    private void DownloadOllama_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://ollama.com") { UseShellExecute = true });
        }
        catch { }
    }

    private void EditCustomFields_Click(object sender, RoutedEventArgs e)
    {
        var appSettings     = ServiceLocator.Get<AppSettings>();
        var settingsService = ServiceLocator.Get<ISettingsService>();
        var win = new CustomFieldsWindow(appSettings, settingsService) { Owner = this };
        if (win.ShowDialog() == true)
            _vm.RefreshCustomFieldCount();
    }
}
