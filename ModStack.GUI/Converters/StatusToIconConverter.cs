using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts service status to icon
/// </summary>
public class StatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "running" => "🟢",
                "stopped" => "🔴",
                "starting" => "🟡",
                "stopping" => "🟡",
                _ => "⚪"
            };
        }
        return "⚪";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}