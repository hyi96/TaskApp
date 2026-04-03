using System;
using System.Diagnostics;
using System.Security;
using System.Text;

namespace TaskApp.Services;

public static class NotificationService
{
    public static void Show(string title, string body)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            ShowWindowsToast(title, body);
        }
        catch
        {
            // Notification is best-effort; never crash the app
        }
    }

    private static void ShowWindowsToast(string title, string body)
    {
        var escapedTitle = SecurityElement.Escape(title) ?? title;
        var escapedBody = SecurityElement.Escape(body) ?? body;

        var script =
            "$null=[Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime]\n" +
            "$null=[Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom.XmlDocument,ContentType=WindowsRuntime]\n" +
            "$x=New-Object Windows.Data.Xml.Dom.XmlDocument\n" +
            "$x.LoadXml('<toast><visual><binding template=\"ToastGeneric\">" +
            "<text>" + escapedTitle + "</text>" +
            "<text>" + escapedBody + "</text>" +
            "</binding></visual></toast>')\n" +
            "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('TaskApp').Show(" +
            "[Windows.UI.Notifications.ToastNotification]::new($x))";

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.Dispose();
    }
}
