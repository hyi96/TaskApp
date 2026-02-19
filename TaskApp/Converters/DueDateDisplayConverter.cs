using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TaskApp.Converters;

public class DueDateDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto) return null;

        return dto.TimeOfDay != TimeSpan.Zero
            ? $"Due: {dto:MMM d} at {dto:h:mm tt}"
            : $"Due: {dto:MMM d}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
