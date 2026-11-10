using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts boolean values to status colors for UI display
/// </summary>
public class BooleanToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue switch
            {
                true => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green for enabled/active
                false => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)) // Red for disabled/inactive
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)); // Default Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
