using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ModStack.Common;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converter that converts StackStatus enum to SolidColorBrush
/// </summary>
public class StackStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StackStatus status)
        {
            return status switch
            {
                StackStatus.Active => new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)), // Green
                StackStatus.Inactive => new SolidColorBrush(Color.FromRgb(0x8A, 0x88, 0x86)), // Gray
                StackStatus.Starting => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), // Orange
                StackStatus.Stopping => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), // Orange
                StackStatus.Error => new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38)), // Red
                StackStatus.PartiallyActive => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), // Orange
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x88, 0x86)) // Default Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x8A, 0x88, 0x86)); // Default Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
