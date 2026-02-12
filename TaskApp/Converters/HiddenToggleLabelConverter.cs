using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TaskApp.Converters;

public class HiddenToggleLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isHidden && isHidden ? "Unhide" : "Hide";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
