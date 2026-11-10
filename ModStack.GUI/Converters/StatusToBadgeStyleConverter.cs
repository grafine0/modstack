using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts service status to badge style
/// </summary>
public class StatusToBadgeStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "running" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                "stopped" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                "starting" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                "stopping" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}