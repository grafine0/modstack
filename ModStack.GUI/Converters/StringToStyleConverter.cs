using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts a string resource name to a Style object
/// </summary>
public class StringToStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string styleName)
        {
            // Try to find the style in application resources
            var style = Application.Current.TryFindResource(styleName);
            return style ?? DependencyProperty.UnsetValue;
        }
        
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
