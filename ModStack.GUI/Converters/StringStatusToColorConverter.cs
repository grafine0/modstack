using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts string status to a color for UI display with dark mode support
/// </summary>
public class StringStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string status)
        {
            // Check if we're in dark mode
            bool isDarkMode = IsDarkMode();
            
            return status.ToLower() switch
            {
                "running" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)) // Lighter Green for dark mode
                    : new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green for light mode
                "stopped" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xF4, 0x7A, 0x6B)) // Lighter Red for dark mode
                    : new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // Red for light mode
                "starting" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x4D)) // Lighter Orange for dark mode
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // Orange for light mode
                "stopping" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x4D)) // Lighter Orange for dark mode
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // Orange for light mode
                "error" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xF4, 0x7A, 0x6B)) // Lighter Red for dark mode
                    : new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // Red for light mode
                "checking..." => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6)) // Lighter Blue for dark mode
                    : new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)), // Blue for light mode
                "not installed" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)) // Lighter Gray for dark mode
                    : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), // Gray for light mode
                "unknown" => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)) // Lighter Gray for dark mode
                    : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), // Gray for light mode
                _ => isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)) // Lighter Gray for dark mode
                    : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)) // Default Gray for light mode
            };
        }
        
        // Check if we're in dark mode for default return
        bool isDarkModeDefault = IsDarkMode();
        return isDarkModeDefault 
            ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)) // Lighter Gray for dark mode
            : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // Default Gray for light mode
    }
    
    private bool IsDarkMode()
    {
        try
        {
            // Check if the current theme is dark
            var currentTheme = ModernWpf.ThemeManager.Current.ApplicationTheme;
            return currentTheme == ModernWpf.ApplicationTheme.Dark;
        }
        catch
        {
            // Fallback to checking system theme
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
