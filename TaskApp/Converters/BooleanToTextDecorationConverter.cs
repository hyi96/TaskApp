using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace TaskApp.Converters;

public class BooleanToTextDecorationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TextDecorations.Strikethrough : new TextDecorationCollection();
        }

        return BindingOperations.DoNothing;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BooleanToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? 0.5 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class NotNullAndBooleanMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Count >= 2 && values[0] != null && values[1] is bool b && b;
    }
}

public class TodoDeadlineForegroundMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        var theme = (values.Count >= 4 ? values[3] as ThemeVariant : null) ?? app?.ActualThemeVariant;
        var deadlineBrush = values.Count >= 3 && values[1] is bool completed && values[2] is DateTimeOffset now
            ? GetDeadlineBrushKey(values[0] as DateTimeOffset?, completed, now)
            : null;

        if (app?.TryGetResource(deadlineBrush ?? "AppTextBrush", theme, out var brush) == true && brush is IBrush)
            return brush;

        return deadlineBrush switch
        {
            "TodoOverdueBrush" => Brushes.Firebrick,
            "TodoDueSoonBrush" => Brushes.DarkGoldenrod,
            _ => theme == ThemeVariant.Dark || theme?.InheritVariant == ThemeVariant.Dark
                ? Brushes.WhiteSmoke : Brushes.Black
        };
    }

    internal static string? GetDeadlineBrushKey(DateTimeOffset? dueDate, bool completed, DateTimeOffset now)
    {
        if (completed || dueDate is not DateTimeOffset deadline)
            return null;

        var remaining = deadline - now;
        if (remaining < TimeSpan.Zero)
            return "TodoOverdueBrush";

        return remaining <= TimeSpan.FromDays(3) ? "TodoDueSoonBrush" : null;
    }
}

public class LoggedDurationDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan duration || duration <= TimeSpan.Zero)
            return null;

        var hours = (long)duration.TotalHours;
        var time = hours > 0 ? $"{hours}h {duration.Minutes}m"
            : duration.Minutes > 0 ? $"{duration.Minutes}m"
            : $"{duration.Seconds}s";
        return $"Logged this period: {time}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
