using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace TaskApp.Converters;

public class BooleanNegationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : BindingOperations.DoNothing;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : BindingOperations.DoNothing;
    }
}
