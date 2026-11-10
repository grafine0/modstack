using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts string status to SolidColorBrush for UI display
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "running" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                "stopped" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // Red
                "starting" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // Orange
                "stopping" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // Orange
                "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),     // Red
                "unknown" => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))          // Default Gray
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Default Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
