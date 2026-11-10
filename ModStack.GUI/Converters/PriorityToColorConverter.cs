using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int priority)
        {
            return priority switch
            {
                1 => new SolidColorBrush(Color.FromRgb(220, 53, 69)), // Red - High priority
                2 => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow - Medium priority
                3 => new SolidColorBrush(Color.FromRgb(40, 167, 69)), // Green - Normal priority
                4 => new SolidColorBrush(Color.FromRgb(108, 117, 125)), // Gray - Low priority
                _ => new SolidColorBrush(Color.FromRgb(108, 117, 125)) // Gray - Default
            };
        }
        return new SolidColorBrush(Color.FromRgb(108, 117, 125));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
