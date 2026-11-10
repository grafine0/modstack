using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts boolean values to status text for UI display
/// </summary>
public class BooleanToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "Installed" : "Not Installed";
        }
        
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}