using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ModStack.GUI.Converters
{
    /// <summary>
    /// Converter for module toggle button background colors
    /// </summary>
    public class ModuleToggleBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                // When IsEnabled = true, button shows "Disable" (orange background)
                // When IsEnabled = false, button shows "Enable" (green background)
                return isEnabled ? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)) : new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            }
            return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
