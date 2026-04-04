using System;
using Microsoft.Toolkit.Uwp.Notifications;

namespace TaskApp.Services;

public static class NotificationService
{
    public static void Show(string title, string body)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch
        {
            // Notification is best-effort; never crash the app
        }
    }
}
