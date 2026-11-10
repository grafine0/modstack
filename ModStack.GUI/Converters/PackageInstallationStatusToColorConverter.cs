using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts package installation status strings to colors for UI display
/// </summary>
public class PackageInstallationStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Not Installed" => Application.Current.FindResource("DangerBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // Red
                "All Versions Installed" => Application.Current.FindResource("SuccessBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                var s when s.Contains("Versions Installed") => Application.Current.FindResource("WarningBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // Orange for partial
                _ => Application.Current.FindResource("TextTertiaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)) // Default Gray
            };
        }
        
        return Application.Current.FindResource("TextTertiaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
