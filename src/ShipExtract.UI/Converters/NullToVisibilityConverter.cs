using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ShipExtract.UI.Converters;

/// <summary>Converts <see langword="null"/> to <see cref="Visibility.Collapsed"/> and non-null to <see cref="Visibility.Visible"/>.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        return (isNull ^ invert) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
