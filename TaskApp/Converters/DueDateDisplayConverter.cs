using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TaskApp.Converters;

public class DueDateDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto) return null;

        var time = dto.TimeOfDay;
        // 23:59:xx is the default end-of-day sentinel — treat as date-only
        var isDefault = time.Hours == 23 && time.Minutes == 59;
        var showYear = dto.Year != DateTimeOffset.Now.Year;
        var dateFmt = showYear ? "MMM d, yyyy" : "MMM d";
        var dateStr = dto.ToString(dateFmt);

        return time != TimeSpan.Zero && !isDefault
            ? $"Due: {dateStr} at {dto:h:mm tt}"
            : $"Due: {dateStr}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
