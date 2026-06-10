using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ShipExtract.Domain.Enums;

namespace ShipExtract.UI.Converters;

/// <summary>Converts a <see cref="ProcessingStatus"/> to a user-friendly display string.</summary>
[ValueConversion(typeof(ProcessingStatus), typeof(string))]
public sealed class ProcessingStatusToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ProcessingStatus status
            ? status switch
            {
                ProcessingStatus.Pending        => "Pending",
                ProcessingStatus.Running        => "Processing\u2026",
                ProcessingStatus.Succeeded      => "Done",
                ProcessingStatus.PartialSuccess => "Partial",
                ProcessingStatus.Failed         => "Failed",
                _                               => status.ToString()
            }
            : string.Empty;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
