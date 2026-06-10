using System.Globalization;
using System.Windows.Data;
using ShipExtract.Domain.Enums;
using WpfBrushes       = System.Windows.Media.Brushes;
using WpfColor         = System.Windows.Media.Color;
using WpfSolidBrush    = System.Windows.Media.SolidColorBrush;

namespace ShipExtract.UI.Converters;

/// <summary>Converts a <see cref="CarrierType"/> to the carrier's brand background colour.</summary>
public sealed class CarrierToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CarrierType carrier)
            return WpfBrushes.Transparent;

        return carrier switch
        {
            CarrierType.DHL   => new WpfSolidBrush(WpfColor.FromRgb(0xFF, 0xCC, 0x00)), // DHL yellow
            CarrierType.FedEx => new WpfSolidBrush(WpfColor.FromRgb(0x4D, 0x14, 0x8C)), // FedEx purple
            CarrierType.UPS   => new WpfSolidBrush(WpfColor.FromRgb(0x35, 0x1C, 0x15)), // UPS brown
            _                 => (WpfSolidBrush)System.Windows.Application.Current.Resources["PrimaryLightBrush"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="CarrierType"/> to the appropriate foreground text colour for the badge.</summary>
public sealed class CarrierToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CarrierType carrier)
            return WpfBrushes.White;

        return carrier switch
        {
            CarrierType.DHL => WpfBrushes.Black,                                        // dark text on yellow
            CarrierType.UPS => new WpfSolidBrush(WpfColor.FromRgb(0xFF, 0xB5, 0x00)), // UPS gold on brown
            _               => WpfBrushes.White
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
