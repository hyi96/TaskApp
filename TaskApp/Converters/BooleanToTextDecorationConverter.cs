using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TaskApp.Converters;

public class BooleanToTextDecorationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return TextDecorations.Strikethrough;
        }

        if (value is bool)
        {
            // Return a new empty TextDecorationCollection instead of TextDecorationCollection.Empty
            return new TextDecorationCollection();
        }

        return BindingOperations.DoNothing;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
