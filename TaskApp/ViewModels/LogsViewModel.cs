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
        return entry.Type switch
        {
            LogType.HabitIncremented => $"Habit incremented: {entry.TitleSnapshot}",
            LogType.DailyCompleted => $"Daily completed: {entry.TitleSnapshot}",
            LogType.TodoCompleted => $"Todo completed: {entry.TitleSnapshot}",
            LogType.RewardClaimed => $"Reward claimed: {entry.TitleSnapshot}",
            LogType.ActivityDuration => $"Spent {FormatDuration(entry.Duration)} on activity: {entry.TitleSnapshot}",
            _ => entry.TitleSnapshot
        };
    }

    private static string FormatDuration(System.TimeSpan? duration)
    {
        var span = duration ?? System.TimeSpan.Zero;
        return span.ToString(span.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }
}
