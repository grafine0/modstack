using System;
using System.Globalization;
using System.Windows.Data;
using ModStack.Common;

namespace ModStack.GUI.Converters;

/// <summary>
/// Converter that converts StackStatus enum to display text
/// </summary>
public class StackStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StackStatus status)
        {
            return status switch
            {
                StackStatus.Active => "Active",
                StackStatus.Inactive => "Inactive",
                StackStatus.Starting => "Starting...",
                StackStatus.Stopping => "Stopping...",
                StackStatus.Error => "Error",
                StackStatus.PartiallyActive => "Partially Active",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

