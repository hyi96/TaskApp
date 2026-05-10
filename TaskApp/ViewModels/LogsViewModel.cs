using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private readonly ITaskAppDataStore _storageService;
    private int _selectedLimit = 50;
    private DateTimeOffset _fromDate;
    private DateTimeOffset _toDate;
    private bool _isUndoMode;

    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();

    public int[] LimitOptions { get; } = [50, 100, 200, 500];

    public int SelectedLimit
    {
        get => _selectedLimit;
        set => SetProperty(ref _selectedLimit, value);
    }

    public DateTimeOffset FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTimeOffset ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public bool IsUndoMode
    {
        get => _isUndoMode;
        set => SetProperty(ref _isUndoMode, value);
    }

    public event Func<LogEntry, Task<bool>>? RequestUndo;

    public LogsViewModel(ITaskAppDataStore storageService)
    {
        _storageService = storageService;
        _fromDate = DateTimeOffset.Now.AddDays(-7);
        _toDate = DateTimeOffset.Now;
    }

    public async Task LoadAsync()
    {
        Logs.Clear();
        var fromLocal = FromDate.LocalDateTime.Date;
        var from = new DateTimeOffset(fromLocal, TimeZoneInfo.Local.GetUtcOffset(fromLocal));
        var toLocal = ToDate.LocalDateTime.Date.AddDays(1).AddTicks(-1);
        var to = new DateTimeOffset(toLocal, TimeZoneInfo.Local.GetUtcOffset(toLocal));
        var entries = await _storageService.LoadFilteredLogEntriesAsync(SelectedLimit, from, to);
        foreach (var entry in entries)
        {
            Logs.Add(LogEntryViewModel.FromEntry(entry));
        }
    }

    public async Task UndoEntryAsync(LogEntryViewModel entryVm)
    {
        if (entryVm.Entry == null || RequestUndo == null) return;

        var success = await RequestUndo(entryVm.Entry);
        if (success)
        {
            Logs.Remove(entryVm);
        }
    }
}

public class LogEntryViewModel
{
    public string Message { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
    public LogEntry? Entry { get; init; }

    public static LogEntryViewModel FromEntry(LogEntry entry)
    {
        return new LogEntryViewModel
        {
            Message = BuildMessage(entry),
            Timestamp = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Entry = entry
        };
    }

    private static string BuildMessage(LogEntry entry)
    {
        var message = entry.Type switch
        {
            LogType.HabitIncremented => $"Habit incremented by {FormatCountDelta(entry.CountDelta)}: {entry.TitleSnapshot}",
            LogType.DailyCompleted => $"Daily completed: {entry.TitleSnapshot}",
            LogType.DailyStreakProtected => $"Streak protected: {entry.TitleSnapshot}",
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

    private static string FormatCountDelta(double? delta)
    {
        return delta.HasValue ? delta.Value.ToString("0.##", CultureInfo.InvariantCulture) : "1";
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        var span = duration ?? TimeSpan.Zero;
        return span.ToString(span.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }

    private static string FormatGoldDelta(double delta)
    {
        var sign = delta >= 0 ? "+" : "-";
        var magnitude = Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture);
        return $"{sign}{magnitude} G";
    }
}
