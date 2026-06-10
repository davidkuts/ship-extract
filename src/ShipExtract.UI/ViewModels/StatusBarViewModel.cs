using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShipExtract.UI.ViewModels;

/// <summary>View model for the application status bar.</summary>
public sealed partial class StatusBarViewModel : ObservableObject
{
    /// <summary>Gets the application version string derived from the assembly version.</summary>
    public string Version { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");
}
