using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class UndoLogEntryTests : IDisposable
{
    private readonly string _tempDir;

    public UndoLogEntryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region Habit Undo

    [Fact]
    public async Task UndoHabit_DecrementsCount()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetGoldReward(1.0);
        habit.Increment(); // Count = 1
        vm.AddGold(1.0);
        await vm.LogHabitIncrementAsync(habit, 1.0);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.True(result);
        Assert.Equal(0, habit.Count);
    }

    [Fact]
    public async Task UndoHabit_ReversesGold()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetGoldReward(2.5);

        var goldBefore = vm.User.Gold;
        habit.Increment();
        vm.AddGold(2.5);
        await vm.LogHabitIncrementAsync(habit, 2.5);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(goldBefore, vm.User.Gold, 2);
    }

    [Fact]
    public async Task UndoHabit_RollsBackLastCompletedDate_ToNull_WhenNoPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        Assert.NotNull(habit.LastCompletedDate);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(entry);

        Assert.Null(habit.LastCompletedDate);
    }

    [Fact]
    public async Task UndoHabit_RollsBackLastCompletedDate_ToPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        // First increment
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);
        var firstDate = habit.LastCompletedDate;

        // Small delay so timestamps differ
        await Task.Delay(50);

        // Second increment
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(latestEntry);

        Assert.NotNull(habit.LastCompletedDate);
        Assert.NotNull(firstDate);
    }

    [Fact]
    public async Task UndoHabit_CountClampsToZero_WhenDeltaExceedsCount()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetIncrementAmount(10);
        habit.SetIncrementEnabled(true);
        habit.SetDecrementEnabled(true);

        // Increment once: count = 10
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        // Decrement back to 0 (DecrementEnabled = true)
        habit.Decrement(); // 10 - 10 = 0
        Assert.Equal(0, habit.Count);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(entry);

        // 0 - 10 = -10, clamped to 0
        Assert.Equal(0, habit.Count);
    }

    [Fact]
    public async Task UndoHabit_ReturnsFalse_WhenHabitNotFound()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.HabitIncremented,
            TaskId = Guid.NewGuid(), // non-existent
            GoldDelta = 1.0,
            CountDelta = 1.0,
            TitleSnapshot = "Ghost"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    [Fact]
    public async Task UndoHabit_ReturnsFalse_WhenTaskIdNull()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.HabitIncremented,
            TaskId = null,
            GoldDelta = 1.0,
            TitleSnapshot = "Null"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    [Fact]
    public async Task UndoHabit_UsesCountDelta_FromLogEntry()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetIncrementAmount(3);
        habit.Increment(); // Count = 3
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        // Change increment amount after logging
        habit.SetIncrementAmount(10);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        // CountDelta in the log should be 3 (the original), not 10
        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(0, habit.Count);
    }

    #endregion

    #region Daily Undo

    [Fact]
    public async Task UndoDaily_ClearsCompletion()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(0.5);
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());

        Assert.True(daily.IsCompleteForCurrentPeriod);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.False(daily.IsCompleteForCurrentPeriod);
        Assert.Null(daily.LastCompletionPeriod);
        Assert.Null(daily.LastCompletedDate);
    }

    [Fact]
    public async Task UndoDaily_DecrementsStreak()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());

        Assert.Equal(1, daily.CurrentStreak);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(0, daily.CurrentStreak);
    }

    [Fact]
    public async Task UndoDaily_ThenReComplete_PreservesStreak()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete for yesterday's period (streak 0 → 1)
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        daily.Complete(yesterday);
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus(), yesterday);
        Assert.Equal(1, daily.CurrentStreak);

        // Complete for today's period (streak 1 → 2)
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.Equal(2, daily.CurrentStreak);

        // Undo today's completion → streak 2 → 1
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.DailyCompleted);
        await vm.UndoLogEntryAsync(latestEntry);
        Assert.Equal(1, daily.CurrentStreak);

        // Re-complete today → streak should go back to 2, not reset to 1
        daily.Complete();
        Assert.Equal(2, daily.CurrentStreak);
    }

    [Fact]
    public async Task UndoDaily_ReversesGold()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(3.0);

        var goldBefore = vm.User.Gold;
        daily.Complete();
        var reward = daily.GetGoldRewardWithBonus();
        vm.AddGold(reward);
        await vm.LogDailyCompletedAsync(daily, reward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(goldBefore, vm.User.Gold, 2);
    }

    [Fact]
    public async Task UndoDaily_NoOp_WhenNewerCompletionExists()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);

        // First completion (last week)
        var lastWeek = DateTimeOffset.UtcNow.AddDays(-7);
        daily.Complete(lastWeek);
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus(), lastWeek);

        // Second completion (today)
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());

        Assert.Equal(2, daily.CurrentStreak);

        // Undo the OLDER entry — should not change completion state since a newer entry exists
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var olderEntry = logs.Where(e => e.Type == LogType.DailyCompleted)
            .OrderBy(e => e.Timestamp).First();

        await vm.UndoLogEntryAsync(olderEntry);

        // Completion state preserved — newer entry still exists
        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public async Task UndoDaily_RollsBackLastCompletedDate_ToNull_WhenNoPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());

        Assert.NotNull(daily.LastCompletedDate);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.Null(daily.LastCompletedDate);
    }

    [Fact]
    public async Task UndoDaily_WorksAfterPeriodChange()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete with daily cadence
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Change period to weekly — completion should be preserved
        daily.SetCadence(RepeatCadence.Weekly);
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Undo the completion — should uncomplete even after period change
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);
        await vm.UndoLogEntryAsync(entry);

        Assert.False(daily.IsCompleteForCurrentPeriod);
        Assert.Null(daily.LastCompletionPeriod);
        Assert.Null(daily.LastCompletedDate);
    }

    [Fact]
    public async Task UndoDaily_WorksAfterRepeatEveryChange()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);

        // Complete with weekly cadence
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Change to every 2 weeks — completion should be preserved
        daily.SetRepeatEvery(2);
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Undo
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);
        await vm.UndoLogEntryAsync(entry);

        Assert.False(daily.IsCompleteForCurrentPeriod);
        Assert.Null(daily.LastCompletedDate);
    }

    [Fact]
    public async Task UndoDaily_WorksAfterFormSave_ChangeCadence()
    {
        // Exact app flow: complete → open edit form → change cadence → save form → save data → undo
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Step 1: Complete (daily cadence, every 1)
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Step 2: Open edit form and change cadence (simulating DailyFormViewModel)
        var formVm = new DailyFormViewModel(Enumerable.Empty<SelectableTag>(), daily);
        formVm.Cadence = RepeatCadence.Weekly;
        formVm.Save();
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Step 3: Save data (like the app does after form closes)
        await vm.SaveDataAsync();

        // Step 4: Undo the completion
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);
        await vm.UndoLogEntryAsync(entry);

        Assert.False(daily.IsCompleteForCurrentPeriod);
        Assert.Null(daily.LastCompletedDate);
        Assert.Null(daily.LastCompletionPeriod);
    }

    [Fact]
    public async Task UndoDaily_WorksAfterFormSave_ChangeRepeatEvery()
    {
        // Exact app flow: complete → open edit form → change repeat every → save form → save data → undo
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);

        // Step 1: Complete
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Step 2: Open edit form and change repeat every
        var formVm = new DailyFormViewModel(Enumerable.Empty<SelectableTag>(), daily);
        formVm.RepeatEvery = 2;
        formVm.Save();
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Step 3: Save data
        await vm.SaveDataAsync();

        // Step 4: Undo
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);
        await vm.UndoLogEntryAsync(entry);

        Assert.False(daily.IsCompleteForCurrentPeriod);
        Assert.Null(daily.LastCompletedDate);
    }

    [Fact]
    public async Task UndoDaily_WorksAfterPeriodChange_WithOlderEntriesInSameNewPeriod()
    {
        // Bug: daily cadence, completed Mon + Tue. Change to weekly. Undo Tue.
        // Mon's entry maps to current week → task stays completed. Should be uncompleted.
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete yesterday (daily cadence, different period)
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        daily.Complete(yesterday);
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus(), yesterday);

        // Complete today (daily cadence, current period)
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Change to weekly — both entries now fall in the same week
        daily.SetCadence(RepeatCadence.Weekly);
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Undo today's completion
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.Where(e => e.Type == LogType.DailyCompleted)
            .OrderByDescending(e => e.Timestamp).First();
        await vm.UndoLogEntryAsync(latestEntry);

        // Must be uncompleted even though yesterday's entry is in the same week
        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public async Task UndoDaily_RollsBackLastCompletedDate_ToPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);

        // First completion
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());
        var firstDate = daily.LastCompletedDate;

        await Task.Delay(50);

        // Second completion (log a second entry for the same task)
        // We need to clear period to allow re-complete in the same period for testing
        daily.Complete();
        vm.AddGold(daily.GetGoldRewardWithBonus());
        await vm.LogDailyCompletedAsync(daily, daily.GetGoldRewardWithBonus());

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.DailyCompleted);

        await vm.UndoLogEntryAsync(latestEntry);

        // Should roll back to the first entry's timestamp, not null
        Assert.NotNull(daily.LastCompletedDate);
        Assert.NotNull(firstDate);
    }

    [Fact]
    public async Task UndoDaily_ReturnsFalse_WhenDailyNotFound()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.DailyCompleted,
            TaskId = Guid.NewGuid(),
            GoldDelta = 1.0,
            TitleSnapshot = "Ghost"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    [Fact]
    public async Task UndoDaily_ReturnsFalse_WhenTaskIdNull()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.DailyCompleted,
            TaskId = null,
            GoldDelta = 0.5,
            TitleSnapshot = "Null"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    #endregion

    #region Todo Undo

    [Fact]
    public async Task UndoTodo_ClearsLastCompletedDate()
    {
        var vm = CreateViewModel();
        vm.NewTodoTitle = "Buy milk";
        vm.AddTodo();
        var todo = vm.Todos[0];
        todo.SetGoldReward(1.0);
        todo.Complete();
        vm.AddGold(1.0);
        await vm.LogTodoCompletedAsync(todo, 1.0);

        Assert.NotNull(todo.LastCompletedDate);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.TodoCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.Null(todo.LastCompletedDate);
    }

    [Fact]
    public async Task UndoTodo_ReversesGold()
    {
        var vm = CreateViewModel();
        vm.NewTodoTitle = "Buy milk";
        vm.AddTodo();
        var todo = vm.Todos[0];
        todo.SetGoldReward(5.0);

        var goldBefore = vm.User.Gold;
        todo.Complete();
        vm.AddGold(5.0);
        await vm.LogTodoCompletedAsync(todo, 5.0);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.TodoCompleted);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(goldBefore, vm.User.Gold, 2);
    }

    [Fact]
    public async Task UndoTodo_AlwaysNullsLastCompletedDate_EvenWithPreviousEntries()
    {
        var vm = CreateViewModel();
        vm.NewTodoTitle = "Buy milk";
        vm.AddTodo();
        var todo = vm.Todos[0];
        todo.SetGoldReward(1.0);

        // First completion
        todo.Complete();
        vm.AddGold(1.0);
        await vm.LogTodoCompletedAsync(todo, 1.0);

        await Task.Delay(50);

        // Second completion
        todo.Complete();
        vm.AddGold(1.0);
        await vm.LogTodoCompletedAsync(todo, 1.0);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.TodoCompleted);

        await vm.UndoLogEntryAsync(latestEntry);

        // Todo is one-time, always nulls out regardless of previous entries
        Assert.Null(todo.LastCompletedDate);
    }

    [Fact]
    public async Task UndoTodo_ReturnsFalse_WhenTodoNotFound()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.TodoCompleted,
            TaskId = Guid.NewGuid(),
            GoldDelta = 1.0,
            TitleSnapshot = "Ghost"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    #endregion

    #region Reward Undo

    [Fact]
    public async Task UndoReward_DecrementsClaimCount()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Movie night";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(2.0);
        reward.SetRepeatable(true);
        vm.User.Gold = 10;
        vm.ClaimReward(reward);

        Assert.Equal(1, reward.ClaimCount);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(0, reward.ClaimCount);
    }

    [Fact]
    public async Task UndoReward_AddsGoldBack()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Movie night";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(5.0);
        reward.SetRepeatable(true);
        vm.User.Gold = 20;

        var goldBefore = vm.User.Gold;
        vm.ClaimReward(reward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(goldBefore, vm.User.Gold, 2);
    }

    [Fact]
    public async Task UndoReward_NonRepeatable_UnclaimsWhenCountReachesZero()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Treat";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(1.0);
        reward.SetRepeatable(false);
        vm.User.Gold = 10;
        vm.ClaimReward(reward);

        Assert.True(reward.IsClaimed);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(entry);

        Assert.False(reward.IsClaimed);
        Assert.Equal(0, reward.ClaimCount);
    }

    [Fact]
    public async Task UndoReward_Repeatable_RollsBackClaimedAt_ToPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Snack";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(1.0);
        reward.SetRepeatable(true);
        vm.User.Gold = 100;

        // First claim
        vm.ClaimReward(reward);
        await Task.Delay(50);

        // Second claim
        vm.ClaimReward(reward);

        Assert.Equal(2, reward.ClaimCount);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(latestEntry);

        Assert.Equal(1, reward.ClaimCount);
        // ClaimedAt should roll back to first claim's timestamp, not null
        Assert.NotNull(reward.ClaimedAt);
    }

    [Fact]
    public async Task UndoReward_SetsClaimedAtNull_WhenNoPreviousEntry()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Treat";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(1.0);
        reward.SetRepeatable(true);
        vm.User.Gold = 10;
        vm.ClaimReward(reward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(entry);

        Assert.Null(reward.ClaimedAt);
    }

    [Fact]
    public async Task UndoReward_ReturnsFalse_WhenRewardNotFound()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.RewardClaimed,
            RewardId = Guid.NewGuid(),
            GoldDelta = -1.0,
            TitleSnapshot = "Ghost"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    [Fact]
    public async Task UndoReward_ReturnsFalse_WhenRewardIdNull()
    {
        var vm = CreateViewModel();
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.RewardClaimed,
            RewardId = null,
            GoldDelta = -1.0,
            TitleSnapshot = "Null"
        };

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.False(result);
    }

    #endregion

    #region ActivityDuration Undo

    [Fact]
    public async Task UndoActivityDuration_DeletesLogEntry()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Study";
        vm.AddHabit();
        var habit = vm.Habits[0];

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "Study", habit.Id);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.ActivityDuration);

        var result = await vm.UndoLogEntryAsync(entry);

        Assert.True(result);

        var remaining = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        Assert.DoesNotContain(remaining, e => e.Id == entry.Id);
    }

    [Fact]
    public async Task UndoActivityDuration_DoesNotChangeGold()
    {
        var vm = CreateViewModel();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "Study");

        var goldBefore = vm.User.Gold;

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.ActivityDuration);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(goldBefore, vm.User.Gold, 2);
    }

    #endregion

    #region Gold Reversal Edge Cases

    [Fact]
    public async Task UndoReward_GoldDeltaNegative_RestoresGold()
    {
        // Reward claims have negative GoldDelta. User.Gold -= (-5) => User.Gold += 5
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Big treat";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(10.0);
        reward.SetRepeatable(true);
        vm.User.Gold = 50;
        vm.ClaimReward(reward); // Gold: 50 - 10 = 40

        Assert.Equal(40, vm.User.Gold, 2);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.RewardClaimed);

        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(50, vm.User.Gold, 2);
    }

    [Fact]
    public async Task UndoHabit_GoldCanGoBelowZero_AfterUndo()
    {
        // If user spent all gold and then undoes an earn, gold goes negative
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetGoldReward(10.0);
        habit.Increment();
        vm.AddGold(10.0);
        await vm.LogHabitIncrementAsync(habit, 10.0);

        // Spend all gold
        vm.User.Gold = 0;

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(entry);

        // Gold should go negative (correct reversal even when broke)
        Assert.Equal(-10.0, vm.User.Gold, 2);
    }

    #endregion

    #region Log Entry Deletion

    [Fact]
    public async Task Undo_DeletesLogEntry_FromDatabase()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);
        var entryId = entry.Id;

        await vm.UndoLogEntryAsync(entry);

        var remaining = await vm.StorageService.LoadRecentLogEntriesAsync(100);
        Assert.DoesNotContain(remaining, e => e.Id == entryId);
    }

    [Fact]
    public async Task Undo_SavesData_AfterUndo()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.SetGoldReward(5.0);
        habit.Increment(); // Count = 1
        vm.AddGold(5.0);
        await vm.LogHabitIncrementAsync(habit, 5.0);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        await vm.UndoLogEntryAsync(entry);

        // Reload data from disk to verify it was persisted
        var reloadedVm = CreateViewModel();
        await reloadedVm.LoadDataAsync();
        var reloadedHabit = reloadedVm.Habits.FirstOrDefault(h => h.Title == "Exercise");
        Assert.NotNull(reloadedHabit);
        Assert.Equal(0, reloadedHabit.Count);
    }

    #endregion

    #region LogsViewModel.UndoEntryAsync

    [Fact]
    public async Task LogsViewModel_UndoEntry_RemovesFromList()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        var logsVm = new LogsViewModel(vm.StorageService);
        logsVm.FromDate = DateTimeOffset.Now.AddDays(-1);
        logsVm.ToDate = DateTimeOffset.Now.AddDays(1);
        logsVm.RequestUndo += entry => vm.UndoLogEntryAsync(entry);
        await logsVm.LoadAsync();

        Assert.Single(logsVm.Logs);

        var entryVm = logsVm.Logs[0];
        await logsVm.UndoEntryAsync(entryVm);

        Assert.Empty(logsVm.Logs);
    }

    [Fact]
    public async Task LogsViewModel_UndoEntry_DoesNotRemove_WhenUndoFails()
    {
        var vm = CreateViewModel();
        var logsVm = new LogsViewModel(vm.StorageService);
        // RequestUndo returns false
        logsVm.RequestUndo += _ => Task.FromResult(false);

        var fakeEntryVm = new LogEntryViewModel
        {
            Message = "Test",
            Timestamp = "2026-01-01 00:00:00",
            Entry = new LogEntry
            {
                Id = Guid.NewGuid(),
                Type = LogType.HabitIncremented,
                TaskId = Guid.NewGuid(), // non-existent
                Timestamp = DateTimeOffset.UtcNow,
                TitleSnapshot = "Ghost"
            }
        };
        logsVm.Logs.Add(fakeEntryVm);

        await logsVm.UndoEntryAsync(fakeEntryVm);

        Assert.Single(logsVm.Logs);
    }

    [Fact]
    public async Task LogsViewModel_UndoEntry_NoOp_WhenNoHandler()
    {
        var vm = CreateViewModel();
        var logsVm = new LogsViewModel(vm.StorageService);
        // No RequestUndo handler

        var fakeEntryVm = new LogEntryViewModel
        {
            Message = "Test",
            Timestamp = "2026-01-01 00:00:00",
            Entry = new LogEntry
            {
                Id = Guid.NewGuid(),
                Type = LogType.HabitIncremented,
                Timestamp = DateTimeOffset.UtcNow,
                TitleSnapshot = "Test"
            }
        };
        logsVm.Logs.Add(fakeEntryVm);

        await logsVm.UndoEntryAsync(fakeEntryVm);

        Assert.Single(logsVm.Logs); // unchanged
    }

    [Fact]
    public async Task LogsViewModel_UndoEntry_NoOp_WhenEntryNull()
    {
        var vm = CreateViewModel();
        var logsVm = new LogsViewModel(vm.StorageService);
        logsVm.RequestUndo += _ => Task.FromResult(true);

        var fakeEntryVm = new LogEntryViewModel
        {
            Message = "Test",
            Timestamp = "2026-01-01 00:00:00",
            Entry = null
        };
        logsVm.Logs.Add(fakeEntryVm);

        await logsVm.UndoEntryAsync(fakeEntryVm);

        Assert.Single(logsVm.Logs); // unchanged
    }

    #endregion

    #region StorageService.FindPreviousLogEntryAsync

    [Fact]
    public async Task FindPreviousLogEntry_ReturnsNull_WhenNoPreviousExists()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.HabitIncremented);

        var previous = await vm.StorageService.FindPreviousLogEntryAsync(
            LogType.HabitIncremented, habit.Id, null, entry.Id);

        Assert.Null(previous);
    }

    [Fact]
    public async Task FindPreviousLogEntry_ReturnsPreviousEntry_WhenExists()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        await Task.Delay(50);

        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var latestEntry = logs.First(e => e.Type == LogType.HabitIncremented);

        var previous = await vm.StorageService.FindPreviousLogEntryAsync(
            LogType.HabitIncremented, habit.Id, null, latestEntry.Id);

        Assert.NotNull(previous);
        Assert.NotEqual(latestEntry.Id, previous.Id);
    }

    [Fact]
    public async Task FindPreviousLogEntry_DoesNotReturnDifferentType()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];
        habit.Increment();
        vm.AddGold(habit.GoldReward);
        await vm.LogHabitIncrementAsync(habit, habit.GoldReward);

        // Log a different type for the same task
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "Exercise", habit.Id);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var habitEntry = logs.First(e => e.Type == LogType.HabitIncremented);

        var previous = await vm.StorageService.FindPreviousLogEntryAsync(
            LogType.HabitIncremented, habit.Id, null, habitEntry.Id);

        // Should not find the ActivityDuration entry
        Assert.Null(previous);
    }

    [Fact]
    public async Task FindPreviousLogEntry_DoesNotReturnDifferentTask()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        vm.NewHabitTitle = "Read";
        vm.AddHabit();
        var habit1 = vm.Habits.First(h => h.Title == "Exercise");
        var habit2 = vm.Habits.First(h => h.Title == "Read");

        habit1.Increment();
        vm.AddGold(habit1.GoldReward);
        await vm.LogHabitIncrementAsync(habit1, habit1.GoldReward);

        habit2.Increment();
        vm.AddGold(habit2.GoldReward);
        await vm.LogHabitIncrementAsync(habit2, habit2.GoldReward);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var habit1Entry = logs.First(e => e.Type == LogType.HabitIncremented && e.TaskId == habit1.Id);

        var previous = await vm.StorageService.FindPreviousLogEntryAsync(
            LogType.HabitIncremented, habit1.Id, null, habit1Entry.Id);

        // Should not find habit2's entry
        Assert.Null(previous);
    }

    #endregion

    private MainWindowViewModel CreateViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }
}
