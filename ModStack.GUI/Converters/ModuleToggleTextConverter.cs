using System;
using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters
{
    /// <summary>
    /// Converter for module toggle button text
    /// </summary>
    public class ModuleToggleTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isEnabled) ? "Disable" : "Enable";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
