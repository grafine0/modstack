using System;
using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts a boolean value to a string based on a parameter format "TrueString|FalseString"
/// </summary>
public class BooleanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramString)
            return string.Empty;

        var parts = paramString.Split('|');
        if (parts.Length != 2)
            return string.Empty;

        return boolValue ? parts[0] : parts[1];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

