using System;
using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converter to display user-friendly language names in ComboBox
/// </summary>
public class LanguageDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string cultureCode)
        {
            return cultureCode switch
            {
                "en-US" => "English",
                "fr-FR" => "Français",
                "es-ES" => "Español",
                "ar-SA" => "العربية",
                _ => cultureCode
            };
        }
        
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Conversion back is handled by SelectedItem binding
        return value;
    }
}

