using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TaskApp.Services;

internal static class WindowsWindowFrame
{
    private const uint DefaultColor = 0xFFFFFFFF;

    private enum DwmAttribute
    {
        BorderColor = 34,
        CaptionColor = 35,
        TextColor = 36
    }

    internal static void Register()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        // The routed opening event runs before the window finishes applying its styles.
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => ScheduleUpdate(window));
        TopLevel.ActualThemeVariantProperty.Changed.AddClassHandler<Window>((window, _) => ScheduleUpdate(window));
        Window.IsActiveProperty.Changed.AddClassHandler<Window>((window, _) => ScheduleUpdate(window));
    }

    private static void ScheduleUpdate(Window window)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!window.IsVisible || window.TryGetPlatformHandle() is not { HandleDescriptor: "HWND" } handle ||
                handle.Handle == IntPtr.Zero)
                return;

            var colors = GetColors(window);
            DwmSetWindowAttribute(handle.Handle, DwmAttribute.CaptionColor, colors.Caption, sizeof(uint));
            DwmSetWindowAttribute(handle.Handle, DwmAttribute.TextColor, colors.Text, sizeof(uint));
            DwmSetWindowAttribute(handle.Handle, DwmAttribute.BorderColor, colors.Border, sizeof(uint));
        });
    }

    internal static (uint Caption, uint Text, uint Border) GetColors(Window window)
    {
        if (window.ActualThemeVariant != App.ForestTheme)
            return (DefaultColor, DefaultColor, DefaultColor);

        var border = window.IsActive ? "WindowActiveBorderBrush" : "WindowInactiveBorderBrush";
        return (GetColorRef(window, "WindowCaptionBrush"), GetColorRef(window, "AppTextBrush"), GetColorRef(window, border));
    }

    private static uint GetColorRef(Window window, string resourceKey)
    {
        if (!window.TryFindResource(resourceKey, window.ActualThemeVariant, out var value) || value is not ISolidColorBrush brush)
            return DefaultColor;

        var color = brush.Color;
        return (uint)color.R | ((uint)color.G << 8) | ((uint)color.B << 16);
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, DwmAttribute attribute, in uint value, int size);
}
