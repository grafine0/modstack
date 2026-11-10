using System;
using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

public class ServiceTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string serviceType)
        {
            return serviceType.ToLower() switch
            {
                "apache" => "🦬", // Buffalo (Apache mascot)
                "nginx" => "🟢",  // Green circle
                "php" => "🐘",   // Elephant (PHP mascot)
                "mysql" => "🐬", // Dolphin (MySQL mascot)
                "postgresql" => "🐘", // Elephant (PostgreSQL mascot)
                "redis" => "🔴", // Red circle
                "phpmyadmin" => "🔧", // Wrench
                _ => "⚙" // Gear
            };
        }
        return "⚙";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
