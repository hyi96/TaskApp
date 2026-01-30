using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace TaskApp.Converters;

public class FilterTabStyleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filterValue && parameter is string tabValue)
        {
            return filterValue == tabValue;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FilterTabForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filterValue && parameter is string tabValue)
        {
            if (filterValue == tabValue)
            {
                // Use theme-aware primary text color for active tab
                if (Application.Current?.TryGetResource("AppTextBrush", Application.Current.ActualThemeVariant, out var brush) == true && brush is IBrush textBrush)
                {
                    return textBrush;
                }
                return new SolidColorBrush(Colors.Black);
            }
        }
        // Use theme-aware secondary text color for inactive tabs
        if (Application.Current?.TryGetResource("SecondaryTextBrush", Application.Current.ActualThemeVariant, out var secondaryBrush) == true && secondaryBrush is IBrush secondary)
        {
            return secondary;
        }
        return new SolidColorBrush(Color.Parse("#999999"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FilterTabFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filterValue && parameter is string tabValue)
        {
            if (filterValue == tabValue)
            {
                return FontWeight.SemiBold;
            }
        }
        return FontWeight.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FilterTabTextDecorationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filterValue && parameter is string tabValue)
        {
            if (filterValue == tabValue)
            {
                return TextDecorations.Underline;
            }
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
