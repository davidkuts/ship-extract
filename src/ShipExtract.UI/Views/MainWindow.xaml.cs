using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.Settings;
using ShipExtract.UI.Controls;
using ShipExtract.UI.DependencyInjection;
using ShipExtract.UI.ViewModels;

namespace ShipExtract.UI.Views;

/// <summary>Code-behind for the main application window.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AppSettings   _appSettings;
    private readonly ISettingsService _settingsService;

    // Window geometry save throttle
    private DispatcherTimer? _geometrySaveTimer;
    private bool _geometryReady; // prevent spurious saves during startup

    // Onboarding step state
    private int  _onboardingStep;
    private bool _onboardingDontShowAgain;
    private static readonly (string Icon, string Title, string Desc)[] OnboardingSteps =
    [
        (
            "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z",
            "Welcome to ShipExtract",
            "ShipExtract uses AI to extract shipment data from PDF documents — tracking numbers, addresses, weights and more — automatically."
        ),
        (
            "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z",
            "Add your PDFs",
            "Drag and drop PDF files into the drop zone on the left, or click it to open a file browser. You can add as many files as you need."
        ),
        (
            "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z",
            "Process & Extract",
            "Click 'Process Files' to send your PDFs to the AI. Each file is analysed and results appear in the queue. Hover or click an item to see extracted fields."
        ),
        (
            "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
            "Export your results",
            "Once processing is done, export to Excel or CSV with one click. Results below your confidence threshold go to a separate Review sheet for manual checking."
        ),
    ];

    /// <summary>Initialises a new instance of <see cref="MainWindow"/>.</summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _vm              = viewModel;
        _appSettings     = ServiceLocator.Get<AppSettings>();
        _settingsService = ServiceLocator.Get<ISettingsService>();
        DataContext      = _vm;

        Loaded           += MainWindow_Loaded;
        SizeChanged      += OnWindowGeometryChanged;
        LocationChanged  += OnWindowGeometryChanged;
        StateChanged     += OnWindowStateChanged;
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Part I — Restore window position/size
        RestoreWindowGeometry();
        _geometryReady = true;

        var credentialService = ServiceLocator.Get<ICredentialService>();

        // Show first-run when Anthropic is selected and no API key is stored
        bool needsSetup = _appSettings.AiProvider == ShipExtract.Infrastructure.AI.AiProvider.Anthropic
            && string.IsNullOrEmpty(credentialService.GetApiKey());

        if (needsSetup)
        {
            var firstRun = new FirstRunWindow(
                credentialService,
                _vm,
                _appSettings.TessDataDirectory,
                _settingsService,
                _appSettings)
            {
                Owner = this
            };
            firstRun.ShowDialog();
        }

        // Part A — Show onboarding tour once (after FirstRunWindow if applicable)
        if (!_appSettings.OnboardingCompleted)
        {
            _onboardingStep = 0;
            ApplyOnboardingStep(0);
            OnboardingOverlay.Visibility = Visibility.Visible;
        }
    }

    // ── Onboarding (Part A) ───────────────────────────────────────────────────

    /// <summary>Shows the onboarding overlay from the beginning (called from SettingsWindow).</summary>
    public void ShowOnboardingOverlay()
    {
        _onboardingStep             = 0;
        _onboardingDontShowAgain    = false;
        OnboardingDontShowChk.IsChecked = false;
        ApplyOnboardingStep(0);
        OnboardingOverlay.Visibility = Visibility.Visible;
    }

    private void ApplyOnboardingStep(int step)
    {
        if (step < 0 || step >= OnboardingSteps.Length) return;
        var (icon, title, desc) = OnboardingSteps[step];

        OnboardingIconPath.Data = System.Windows.Media.Geometry.Parse(icon);
        OnboardingTitle.Text    = title;
        OnboardingDesc.Text     = desc;

        // Update dot indicators
        var dots = new[] { StepDot0, StepDot1, StepDot2, StepDot3 };
        for (int i = 0; i < dots.Length; i++)
            dots[i].Fill = i == step
                ? (System.Windows.Media.Brush)FindResource("PrimaryBrush")
                : (System.Windows.Media.Brush)FindResource("SurfaceDarkBrush");

        bool isLast = step == OnboardingSteps.Length - 1;
        OnboardingNextBtn.Content    = isLast ? "Get started \u2713" : "Next \u2192";
        OnboardingSkipBtn.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnboardingNext_Click(object sender, RoutedEventArgs e)
    {
        if (_onboardingStep < OnboardingSteps.Length - 1)
        {
            _onboardingStep++;
            ApplyOnboardingStep(_onboardingStep);
        }
        else
        {
            // Completed all steps → always mark as done
            DismissOnboarding(persist: true);
        }
    }

    private void OnboardingSkip_Click(object sender, RoutedEventArgs e)
    {
        // Skip → persist only if "Don't show again" is checked
        DismissOnboarding(persist: _onboardingDontShowAgain);
    }

    private void OnboardingDontShowChk_Changed(object sender, RoutedEventArgs e)
    {
        _onboardingDontShowAgain = OnboardingDontShowChk.IsChecked == true;
    }

    private void DismissOnboarding(bool persist)
    {
        OnboardingOverlay.Visibility = Visibility.Collapsed;
        if (persist)
        {
            _appSettings.OnboardingCompleted = true;
            _settingsService.Save(_appSettings);
        }
    }

    // ── Window geometry (Part I) ──────────────────────────────────────────────

    private void RestoreWindowGeometry()
    {
        if (_appSettings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        // Clamp to screen bounds; fallback to CenterScreen when -1
        var screen = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                     ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

        var w = Math.Max(900, Math.Min(_appSettings.WindowWidth,  screen.Width));
        var h = Math.Max(600, Math.Min(_appSettings.WindowHeight, screen.Height));

        Width  = w;
        Height = h;

        if (_appSettings.WindowLeft < 0 || _appSettings.WindowTop < 0)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            // Clamp so window is at least partially on screen
            Left = Math.Max(screen.Left, Math.Min(_appSettings.WindowLeft, screen.Right  - 100));
            Top  = Math.Max(screen.Top,  Math.Min(_appSettings.WindowTop,  screen.Bottom - 100));
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
    }

    private void OnWindowGeometryChanged(object? sender, EventArgs e)
    {
        if (!_geometryReady || WindowState != WindowState.Normal) return;

        _geometrySaveTimer?.Stop();
        _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _geometrySaveTimer.Tick += (_, _) =>
        {
            _geometrySaveTimer.Stop();
            SaveWindowGeometry();
        };
        _geometrySaveTimer.Start();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_geometryReady) return;
        _appSettings.WindowMaximized = WindowState == WindowState.Maximized;
        var settings = _settingsService.Load();
        settings.WindowMaximized = _appSettings.WindowMaximized;
        _settingsService.Save(settings);
    }

    private void SaveWindowGeometry()
    {
        _appSettings.WindowLeft   = Left;
        _appSettings.WindowTop    = Top;
        _appSettings.WindowWidth  = Width;
        _appSettings.WindowHeight = Height;

        var settings = _settingsService.Load();
        settings.WindowLeft   = Left;
        settings.WindowTop    = Top;
        settings.WindowWidth  = Width;
        settings.WindowHeight = Height;
        _settingsService.Save(settings);
    }

    // ── Keyboard shortcuts (Part B) ───────────────────────────────────────────

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;

        if (e.Key == Key.H && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            HistoryButton_Click(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            SettingsButton_Click(sender, new RoutedEventArgs());
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

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

    private void CustomFieldsButton_Click(object sender, RoutedEventArgs e)
    {
        var appSettings = ServiceLocator.Get<AppSettings>();
        var settingsService = ServiceLocator.Get<ISettingsService>();
        var win = new CustomFieldsWindow(appSettings, settingsService) { Owner = this };
        win.ShowDialog();
        _vm.RefreshWarnings();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var historyVm = ServiceLocator.Get<HistoryViewModel>();
        var win = new HistoryWindow(historyVm) { Owner = this };
        win.BatchLoaded += OnHistoryBatchLoaded;
        win.ShowDialog();
    }

    private void OnHistoryBatchLoaded(BatchHistoryEntry entry)
    {
        _vm.LoadFromHistory(entry);
    }

    private void CheckOllamaButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshWarnings();
    }
}
