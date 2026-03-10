using System;
using TaskApp.Models.Logs;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class LogEntryViewModelTests
{
    [Fact]
    public void FromEntry_HabitIncremented_FormatsMessage()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.HabitIncremented,
            TitleSnapshot = "Push-ups",
            CountDelta = 5,
            GoldDelta = 0.5
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Habit incremented by 5", vm.Message);
        Assert.Contains("Push-ups", vm.Message);
        Assert.Contains("+0.5 G", vm.Message);
    }

    [Fact]
    public void FromEntry_HabitIncremented_NullCountDelta_ShowsDefault1()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.HabitIncremented,
            TitleSnapshot = "Water",
            CountDelta = null,
            GoldDelta = 0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Habit incremented by 1", vm.Message);
    }

    [Fact]
    public void FromEntry_DailyCompleted_FormatsMessage()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.DailyCompleted,
            TitleSnapshot = "Meditate",
            GoldDelta = 1.0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Daily completed: Meditate", vm.Message);
        Assert.Contains("+1 G", vm.Message);
    }

    [Fact]
    public void FromEntry_TodoCompleted_FormatsMessage()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.TodoCompleted,
            TitleSnapshot = "Buy groceries",
            GoldDelta = 0.1
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Todo completed: Buy groceries", vm.Message);
        Assert.Contains("+0.1 G", vm.Message);
    }

    [Fact]
    public void FromEntry_RewardClaimed_FormatsMessage()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.RewardClaimed,
            TitleSnapshot = "Pizza Night",
            GoldDelta = -5.0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Reward claimed: Pizza Night", vm.Message);
        Assert.Contains("-5 G", vm.Message);
    }

    [Fact]
    public void FromEntry_ActivityDuration_FormatsMessage()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.ActivityDuration,
            TitleSnapshot = "Deep work",
            Duration = TimeSpan.FromMinutes(45),
            GoldDelta = 0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Spent 45:00 on activity: Deep work", vm.Message);
    }

    [Fact]
    public void FromEntry_ActivityDuration_OverOneHour_FormatsWithHours()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.ActivityDuration,
            TitleSnapshot = "Study",
            Duration = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)),
            GoldDelta = 0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("02:30:00", vm.Message);
    }

    [Fact]
    public void FromEntry_ZeroGoldDelta_OmitsGoldSuffix()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.DailyCompleted,
            TitleSnapshot = "Free task",
            GoldDelta = 0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.DoesNotContain("G", vm.Message);
    }

    [Fact]
    public void FromEntry_Timestamp_FormattedAsLocal()
    {
        var utcTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = utcTime,
            Type = LogType.DailyCompleted,
            TitleSnapshot = "Test"
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        var expected = utcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, vm.Timestamp);
    }

    [Fact]
    public void FromEntry_FractionalCountDelta_FormatsCleanly()
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.HabitIncremented,
            TitleSnapshot = "Water",
            CountDelta = 0.5,
            GoldDelta = 0
        };

        var vm = LogEntryViewModel.FromEntry(entry);

        Assert.Contains("Habit incremented by 0.5", vm.Message);
    }
}
