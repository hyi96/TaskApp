using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

/// <summary>
/// Tests that verify daily tasks checked in the new day window are completed
/// with a recorded time of the last minute of the previous period, and that
/// the completion is properly logged.
/// </summary>
public class NewDayCompletionTests : IDisposable
{
    private readonly string _tempDir;

    public NewDayCompletionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region Completion for previous period

    [Fact]
    public async Task NewDayCompletion_MarksTaskCompleteForPreviousPeriod()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Morning Run";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        // Simulate the new day window logic from App.axaml.cs ShowNewDayWindow
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        // The task should be marked as completed for the previous period
        Assert.Equal(previousPeriodStart, daily.LastCompletionPeriod);
        Assert.NotNull(daily.LastCompletedDate);
    }

    [Fact]
    public async Task NewDayCompletion_DoesNotMarkCompleteForCurrentPeriod()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Evening Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        var currentPeriodStart = daily.GetCurrentPeriodStart();

        // Only complete for previous period if it's actually a different period
        if (previousPeriodStart != currentPeriodStart)
        {
            daily.CompleteForPeriod(previousPeriodStart);
            Assert.False(daily.IsCompleteForCurrentPeriod);
        }
    }

    #endregion

    #region Log entry with end-of-previous-period timestamp

    [Fact]
    public async Task NewDayCompletion_LogsWithEndOfPreviousPeriodTimestamp()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Read Book";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(2.0);

        // Replicate ShowNewDayWindow logic
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        // Verify the log entry exists and has the correct timestamp
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.FirstOrDefault(e => e.Type == LogType.DailyCompleted);

        Assert.NotNull(entry);
        Assert.Equal(endOfPreviousPeriodOffset, entry.Timestamp);
    }

    [Fact]
    public async Task NewDayCompletion_LogTimestamp_IsLastMinuteOfPreviousDay()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Meditate";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(1.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        // The timestamp should be 23:59 of the day before the current period
        Assert.Equal(23, entry.Timestamp.Hour);
        Assert.Equal(59, entry.Timestamp.Minute);
    }

    [Fact]
    public async Task NewDayCompletion_LogEntry_HasCorrectTaskId()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Stretch";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(1.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        Assert.Equal(daily.Id, entry.TaskId);
        Assert.Equal("Stretch", entry.TitleSnapshot);
    }

    [Fact]
    public async Task NewDayCompletion_LogEntry_HasCorrectType()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Journal";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(1.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        Assert.Equal(LogType.DailyCompleted, entry.Type);
    }

    #endregion

    #region Gold reward with streak bonus

    [Fact]
    public async Task NewDayCompletion_AwardsGoldWithStreakBonus()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Push-ups";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(10.0);

        // Establish a streak foundation: complete for 2 days ago, then set streak to 6
        var twoDaysAgo = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-2);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(twoDaysAgo));
        daily.SetCurrentStreak(6);

        var goldBefore = vm.User.Gold;

        // Simulate new day window completion
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        // After CompleteForPeriod, streak is now 7 (was 6, incremented by 1)
        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);

        // Streak 7 with default rules → 10% bonus → 10 * 1.10 = 11.0
        Assert.Equal(goldBefore + 11.0, vm.User.Gold);
    }

    [Fact]
    public async Task NewDayCompletion_LogsGoldDelta_WithStreakBonus()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Squats";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(10.0);

        // Establish a streak foundation: complete for 2 days ago, then set streak to 6
        var twoDaysAgo = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-2);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(twoDaysAgo));
        daily.SetCurrentStreak(6);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);

        // Streak 7 → 10% bonus → 10 * 1.10 = 11.0
        Assert.Equal(11.0, entry.GoldDelta);
    }

    [Fact]
    public async Task NewDayCompletion_NoStreakBonus_WhenStreakBelowThreshold()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Walk";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        // No previous streak → CompleteForPeriod sets streak to 1 (below 7 threshold)
        var goldBefore = vm.User.Gold;

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);

        // Streak 1, no bonus → 5.0
        Assert.Equal(goldBefore + 5.0, vm.User.Gold);
    }

    #endregion

    #region Streak update

    [Fact]
    public void NewDayCompletion_IncrementsStreak_FromZero()
    {
        var daily = CreateDaily();
        daily.SetGoldReward(1.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_IncrementsStreak_WhenConsecutive()
    {
        var daily = CreateDaily();
        daily.SetGoldReward(1.0);

        // Complete the period before yesterday to build a streak foundation
        var twoDaysAgo = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-2);
        var twoDaysAgoPeriod = daily.GetPeriodStartFor(twoDaysAgo);
        daily.CompleteForPeriod(twoDaysAgoPeriod);
        Assert.Equal(1, daily.CurrentStreak);

        // Now complete yesterday's period via new day window
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        Assert.Equal(2, daily.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_ResetsStreak_WhenNotConsecutive()
    {
        var daily = CreateDaily();
        daily.SetGoldReward(1.0);

        // Complete 5 days ago to have a gap
        var fiveDaysAgo = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-5);
        var fiveDaysAgoPeriod = daily.GetPeriodStartFor(fiveDaysAgo);
        daily.CompleteForPeriod(fiveDaysAgoPeriod);
        daily.SetCurrentStreak(3); // Pretend there was a streak

        // Now complete yesterday — there's a gap, so streak resets to 1
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_UpdatesBestStreak()
    {
        var daily = CreateDaily();
        daily.SetGoldReward(1.0);

        // Build a streak by completing consecutive periods
        var twoDaysAgo = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-2);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(twoDaysAgo));

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(yesterday));

        Assert.Equal(2, daily.CurrentStreak);
        Assert.Equal(2, daily.BestStreak);
    }

    #endregion

    #region Multiple dailies in new day window

    [Fact]
    public async Task NewDayCompletion_MultipleCheckedDailies_AllCompletedAndLogged()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Exercise";
        vm.AddDaily();
        vm.NewDailyTitle = "Read";
        vm.AddDaily();
        vm.NewDailyTitle = "Meditate";
        vm.AddDaily();

        var dailies = vm.Dailies.ToList();
        foreach (var d in dailies)
            d.SetGoldReward(2.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);

        // Simulate checking all three in the new day window
        foreach (var daily in dailies)
        {
            var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
            daily.CompleteForPeriod(previousPeriodStart);

            var currentPeriodStart = daily.GetCurrentPeriodStart();
            var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
            var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

            var goldReward = daily.GetGoldRewardWithBonus();
            vm.AddGold(goldReward);
            await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);
        }

        // All three should be logged
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var dailyLogs = logs.Where(e => e.Type == LogType.DailyCompleted).ToList();

        Assert.Equal(3, dailyLogs.Count);

        // Each log should reference one of the dailies
        foreach (var daily in dailies)
        {
            Assert.Contains(dailyLogs, e => e.TaskId == daily.Id);
        }
    }

    [Fact]
    public async Task NewDayCompletion_UncheckedDailies_AreNotCompletedOrLogged()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Checked Task";
        vm.AddDaily();
        vm.NewDailyTitle = "Unchecked Task";
        vm.AddDaily();

        var checkedDaily = vm.Dailies[0];
        var uncheckedDaily = vm.Dailies[1];
        checkedDaily.SetGoldReward(1.0);
        uncheckedDaily.SetGoldReward(1.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);

        // Only complete the checked one (simulating user only checking one item)
        var previousPeriodStart = checkedDaily.GetPeriodStartFor(yesterday);
        checkedDaily.CompleteForPeriod(previousPeriodStart);

        var currentPeriodStart = checkedDaily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        var goldReward = checkedDaily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);
        await vm.LogDailyCompletedAsync(checkedDaily, goldReward, endOfPreviousPeriodOffset);

        // Unchecked daily should NOT be completed
        Assert.Null(uncheckedDaily.LastCompletionPeriod);
        Assert.Null(uncheckedDaily.LastCompletedDate);

        // Only one log entry
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var dailyLogs = logs.Where(e => e.Type == LogType.DailyCompleted).ToList();
        Assert.Single(dailyLogs);
        Assert.Equal(checkedDaily.Id, dailyLogs[0].TaskId);
    }

    #endregion

    #region Weekly cadence new day completion

    [Fact]
    public async Task NewDayCompletion_WeeklyCadence_LogsAtEndOfPreviousWeek()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Weekly Review";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetGoldReward(3.0);

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        var currentPeriodStart = daily.GetCurrentPeriodStart();

        // Only test if yesterday is actually in a different period
        if (previousPeriodStart != currentPeriodStart)
        {
            daily.CompleteForPeriod(previousPeriodStart);

            var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
            var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

            var goldReward = daily.GetGoldRewardWithBonus();
            vm.AddGold(goldReward);
            await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

            var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
            var entry = logs.First(e => e.Type == LogType.DailyCompleted);

            // Timestamp should be 23:59 of the day before the current period starts
            Assert.Equal(23, entry.Timestamp.Hour);
            Assert.Equal(59, entry.Timestamp.Minute);
            Assert.Equal(previousPeriodStart, daily.LastCompletionPeriod);
        }
    }

    #endregion

    #region End-to-end new day window flow

    [Fact]
    public async Task NewDayCompletion_EndToEnd_CompletesLogsAndSaves()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Daily Standup";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        var goldBefore = vm.User.Gold;
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);

        // === Replicate full ShowNewDayWindow flow ===

        // 1. CompleteForPeriod
        var previousPeriodStart = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(previousPeriodStart);

        // 2. Calculate end-of-previous-period timestamp
        var currentPeriodStart = daily.GetCurrentPeriodStart();
        var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
        var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, yesterday.Offset);

        // 3. Award gold with bonus
        var goldReward = daily.GetGoldRewardWithBonus();
        vm.AddGold(goldReward);

        // 4. Log
        await vm.LogDailyCompletedAsync(daily, goldReward, endOfPreviousPeriodOffset);

        // 5. Save
        await vm.SaveDataAsync();

        // === Verify everything ===

        // Task is completed for previous period
        Assert.Equal(previousPeriodStart, daily.LastCompletionPeriod);
        Assert.NotNull(daily.LastCompletedDate);

        // Gold was awarded
        Assert.Equal(goldBefore + goldReward, vm.User.Gold);

        // Log entry exists with correct timestamp and data
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyCompleted);
        Assert.Equal(daily.Id, entry.TaskId);
        Assert.Equal(endOfPreviousPeriodOffset, entry.Timestamp);
        Assert.Equal(goldReward, entry.GoldDelta);
        Assert.Equal("Daily Standup", entry.TitleSnapshot);
    }

    #endregion

    #region Test helpers

    private MainWindowViewModel CreateViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }

    private static DailyTask CreateDaily()
    {
        var daily = new DailyTask();
        daily.UpdateTitle("Test Daily");
        daily.SetCadence(RepeatCadence.Daily);
        daily.SetRepeatEvery(1);
        return daily;
    }

    #endregion
}
