using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ShipExtract.UI.Converters;

/// <summary>
/// Converts <see cref="bool"/> to <see cref="Visibility"/>.
/// Pass <c>ConverterParameter="Invert"</c> to reverse the mapping.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool bv && bv;
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        var invert  = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        return visible ^ invert;
    }
}
