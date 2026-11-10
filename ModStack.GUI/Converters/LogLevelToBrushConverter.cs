using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts log level to SolidColorBrush for UI display
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string logLevel)
        {
            return logLevel.ToLower() switch
            {
                "info" => new SolidColorBrush(Color.FromRgb(100, 181, 246)),     // Light Blue (better for dark mode)
                "warning" => new SolidColorBrush(Color.FromRgb(255, 183, 77)),   // Light Orange (better for dark mode)
                "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),      // Red
                "debug" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),    // Gray
                "trace" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),    // Gray
                "fatal" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),     // Purple
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))           // Default Gray
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Default Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
