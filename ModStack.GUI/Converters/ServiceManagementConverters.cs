using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ModStack.Core.Common;
using ModStack.Common.Models;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts count values to inverse Visibility (0 = Visible, >0 = Collapsed)
/// </summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// Converts service type to visibility for conditional UI elements
/// </summary>
[ValueConversion(typeof(ModStack.Common.ServiceType), typeof(Visibility))]
public class ServiceTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ModStack.Common.ServiceType serviceType && parameter is string targetTypeName)
        {
            if (Enum.TryParse<ModStack.Common.ServiceType>(targetTypeName, out var parsedTargetType))
            {
                return serviceType == parsedTargetType ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// Converts port number to display string with "N/A" for 0 or negative values
/// </summary>
[ValueConversion(typeof(int), typeof(string))]
public class PortToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int port && port > 0)
        {
            return port.ToString();
        }
        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && int.TryParse(str, out int port))
        {
            return port;
        }
        return 0;
    }
}

/// <summary>
/// Converts service path to display name (shows only directory name)
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public class PathToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                return System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            }
            catch
            {
                return path;
            }
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
