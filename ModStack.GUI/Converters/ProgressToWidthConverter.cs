using System;
using System.Globalization;
using System.Windows.Data;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converts progress percentage and total width to actual progress bar width
/// </summary>
public class ProgressToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;
        
        if (values[0] is double progress && values[1] is double totalWidth)
        {
            // Ensure progress is within valid range (0-100)
            progress = Math.Max(0, Math.Min(100, progress));
            
            // Calculate width based on percentage
            var width = Math.Max(0, (totalWidth * progress) / 100.0);
            
            return width;
        }
        
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ProgressToWidthConverter does not support ConvertBack");
    }
}
