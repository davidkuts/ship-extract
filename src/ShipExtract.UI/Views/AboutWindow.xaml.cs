using System.Reflection;
using System.Windows;

namespace ShipExtract.UI.Views;

/// <summary>About dialog showing application version and acknowledgements.</summary>
public partial class AboutWindow : Window
{
    /// <summary>Initialises a new instance of <see cref="AboutWindow"/>.</summary>
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"Version {version}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
