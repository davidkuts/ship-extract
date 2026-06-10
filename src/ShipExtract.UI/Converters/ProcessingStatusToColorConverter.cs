using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ShipExtract.Domain.Enums;
using WpfApplication = System.Windows.Application;

namespace ShipExtract.UI.Converters;

/// <summary>Converts a <see cref="ProcessingStatus"/> to its representative <see cref="SolidColorBrush"/>.</summary>
[ValueConversion(typeof(ProcessingStatus), typeof(SolidColorBrush))]
public sealed class ProcessingStatusToColorConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ProcessingStatus status)
            return GetBrush("TextSecondaryBrush");

        return status switch
        {
            ProcessingStatus.Pending        => GetBrush("TextSecondaryBrush"),
            ProcessingStatus.Running        => GetBrush("PrimaryLightBrush"),
            ProcessingStatus.Succeeded      => GetBrush("SuccessBrush"),
            ProcessingStatus.PartialSuccess => GetBrush("WarningBrush"),
            ProcessingStatus.Failed         => GetBrush("ErrorBrush"),
            _                               => GetBrush("TextSecondaryBrush")
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;

    private static SolidColorBrush GetBrush(string key) =>
        WpfApplication.Current.Resources[key] as SolidColorBrush
        ?? new SolidColorBrush(Colors.Gray);
}
