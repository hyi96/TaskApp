using System;

namespace TaskApp.Models.Logs;

public enum LogType
{
    DailyCompleted,
    HabitIncremented,
    TodoCompleted,
    RewardClaimed,
    ActivityDuration,
    DailyStreakProtected
}

public class LogEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogType Type { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? RewardId { get; set; }
    public double GoldDelta { get; set; }
    public double UserGold { get; set; } // User's total gold after GoldDelta was applied
    public double? CountDelta { get; set; } // for HabitTask
    public TimeSpan? Duration { get; set; }
    public string TitleSnapshot { get; set; } = string.Empty;
    public DateOnly? PreviousLastCompletionPeriod { get; set; } // for DailyStreakProtected undo
}
