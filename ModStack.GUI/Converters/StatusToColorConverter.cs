using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ModStack.Common;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts ServiceStatus to a color for UI display
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Running => Application.Current.FindResource("SuccessBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                ServiceStatus.Stopped => Application.Current.FindResource("DangerBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                ServiceStatus.Starting => Application.Current.FindResource("WarningBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                ServiceStatus.Stopping => Application.Current.FindResource("WarningBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                ServiceStatus.Error => Application.Current.FindResource("DangerBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                ServiceStatus.Unknown => Application.Current.FindResource("TextTertiaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
                _ => Application.Current.FindResource("TextTertiaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
            };
        }
        
        return Application.Current.FindResource("TextTertiaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
