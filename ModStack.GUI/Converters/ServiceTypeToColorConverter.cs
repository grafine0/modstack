using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

public class ServiceTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string serviceType)
        {
            return serviceType.ToLower() switch
            {
                "apache" => new SolidColorBrush(Color.FromRgb(0, 120, 212)), // Blue
                "nginx" => new SolidColorBrush(Color.FromRgb(0, 150, 57)),  // Green
                "php" => new SolidColorBrush(Color.FromRgb(119, 79, 189)),  // Purple
                "mysql" => new SolidColorBrush(Color.FromRgb(255, 140, 0)), // Orange
                "postgresql" => new SolidColorBrush(Color.FromRgb(51, 102, 153)), // Blue-Gray
                "redis" => new SolidColorBrush(Color.FromRgb(220, 20, 60)), // Red
                "phpmyadmin" => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow
                _ => new SolidColorBrush(Color.FromRgb(108, 117, 125)) // Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(108, 117, 125));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}