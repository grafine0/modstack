using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts project status strings to colors for UI display
/// </summary>
public class ProjectStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "active" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                "inactive" => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), // Gray
                "running" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                "stopped" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // Red
                "error" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // Red
                _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)) // Default Gray
            };
        }

        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // Default Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
