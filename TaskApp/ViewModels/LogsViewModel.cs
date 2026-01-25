using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();

    public LogsViewModel(StorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task LoadAsync()
    {
        Logs.Clear();
        var entries = await _storageService.LoadRecentLogEntriesAsync(50);
        foreach (var entry in entries)
        {
            Logs.Add(LogEntryViewModel.FromEntry(entry));
        }
    }
}

public class LogEntryViewModel
{
    public string Message { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;

    public static LogEntryViewModel FromEntry(LogEntry entry)
    {
        return new LogEntryViewModel
        {
            Message = BuildMessage(entry),
            Timestamp = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
    }

    private static string BuildMessage(LogEntry entry)
    {
        var message = entry.Type switch
        {
            LogType.HabitIncremented => $"Habit incremented: {entry.TitleSnapshot}",
            LogType.DailyCompleted => $"Daily completed: {entry.TitleSnapshot}",
            LogType.TodoCompleted => $"Todo completed: {entry.TitleSnapshot}",
            LogType.RewardClaimed => $"Reward claimed: {entry.TitleSnapshot}",
            LogType.ActivityDuration => $"Spent {FormatDuration(entry.Duration)} on activity: {entry.TitleSnapshot}",
            _ => entry.TitleSnapshot
        };

        if (Math.Abs(entry.GoldDelta) > double.Epsilon)
        {
            message += $" ({FormatGoldDelta(entry.GoldDelta)})";
        }

        return message;
    }

    private static string FormatDuration(System.TimeSpan? duration)
    {
        var span = duration ?? System.TimeSpan.Zero;
        return span.ToString(span.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }

    private static string FormatGoldDelta(double delta)
    {
        var sign = delta >= 0 ? "+" : "-";
        var magnitude = Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture);
        return $"{sign}{magnitude} G";
    }
}
