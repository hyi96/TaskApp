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

    #region LastActiveDate stamping

    [Fact]
    public async Task SaveDataAsync_StampsLastActiveDateToToday()
    {
        var vm = CreateViewModel();
        Assert.Null(vm.User.LastActiveDate);

        await vm.SaveDataAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), vm.User.LastActiveDate);
    }

    [Fact]
    public async Task SaveDataAsync_LastActiveDate_PersistsAcrossLoad()
    {
        var vm = CreateViewModel();
        await vm.SaveDataAsync();

        // Load a fresh ViewModel from the same directory
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), vm2.User.LastActiveDate);
    }

    [Fact]
    public void NewProfile_HasNullLastActiveDate()
    {
        var vm = CreateViewModel();
        Assert.Null(vm.User.LastActiveDate);
    }

    #endregion

    #region New day window trigger conditions on user switch

    [Fact]
    public void ShouldShowNewDay_WhenLastActiveDateIsYesterday()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        var vm = CreateViewModel();
        vm.User.LastActiveDate = yesterday;

        // Replicate the condition from OnCurrentUserChanged
        var today = DateOnly.FromDateTime(DateTime.Now);
        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.True(shouldShow);
    }

    [Fact]
    public void ShouldNotShowNewDay_WhenLastActiveDateIsToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var vm = CreateViewModel();
        vm.User.LastActiveDate = today;

        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.False(shouldShow);
    }

    [Fact]
    public void ShouldNotShowNewDay_WhenLastActiveDateIsNull()
    {
        var vm = CreateViewModel();
        vm.User.LastActiveDate = null;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.False(shouldShow);
    }

    [Fact]
    public void ShouldShowNewDay_WhenLastActiveDateIsAncient()
    {
        var vm = CreateViewModel();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-30);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.True(shouldShow);
    }

    [Fact]
    public void ShouldShowNewDay_WhenLastActiveDateIsTwoDaysAgo()
    {
        var vm = CreateViewModel();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-2);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.True(shouldShow);
    }

    #endregion

    #region Streak preservation through new day window

    [Fact]
    public void NewDayCompletion_PreservesStreak_WhenCompletedBeforeRefresh()
    {
        // Simulate a daily with a 5-day streak, last completed day-before-yesterday.
        // The correct flow: CompleteForPeriod (new day window) THEN RefreshForCurrentPeriod.
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var anchor = now.AddDays(-10);
        var daily = CreateDailyWithAnchor(anchor);

        // Build up a streak by completing consecutive days up to day-before-yesterday
        for (int i = 7; i >= 2; i--)
        {
            var day = now.AddDays(-i);
            var period = daily.GetPeriodStartFor(day);
            daily.CompleteForPeriod(period);
        }

        // Streak should be 6 (days -7 through -2)
        Assert.Equal(6, daily.CurrentStreak);

        // Now simulate the new day window: complete for yesterday's period
        var yesterday = now.AddDays(-1);
        var yesterdayPeriod = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(yesterdayPeriod);

        // Streak should be 7 (continued from 6)
        Assert.Equal(7, daily.CurrentStreak);

        // THEN refresh for current period (today) — streak should NOT be reset
        daily.RefreshForCurrentPeriod(now);

        Assert.Equal(7, daily.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_StreakResetToOne_WhenRefreshRunsBeforeComplete()
    {
        // This test documents the OLD buggy behavior where RefreshForCurrentPeriod
        // runs BEFORE CompleteForPeriod, destroying the streak.
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var anchor = now.AddDays(-10);
        var daily = CreateDailyWithAnchor(anchor);

        // Build up a streak by completing consecutive days up to day-before-yesterday
        for (int i = 7; i >= 2; i--)
        {
            var day = now.AddDays(-i);
            var period = daily.GetPeriodStartFor(day);
            daily.CompleteForPeriod(period);
        }

        Assert.Equal(6, daily.CurrentStreak);

        // BUG: RefreshForCurrentPeriod runs first — resets streak to 0
        daily.RefreshForCurrentPeriod(now);
        Assert.Equal(0, daily.CurrentStreak);

        // Then CompleteForPeriod runs — streak becomes 1 instead of 7
        var yesterday = now.AddDays(-1);
        var yesterdayPeriod = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(yesterdayPeriod);

        // Streak is 1 instead of 7 — this was the bug
        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_BestStreak_NotLostByCorrectOrder()
    {
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var anchor = now.AddDays(-20);
        var daily = CreateDailyWithAnchor(anchor);

        // Build up a 10-day streak
        for (int i = 12; i >= 2; i--)
        {
            var day = now.AddDays(-i);
            var period = daily.GetPeriodStartFor(day);
            daily.CompleteForPeriod(period);
        }

        Assert.Equal(11, daily.CurrentStreak);
        Assert.Equal(11, daily.BestStreak);

        // Correct order: complete yesterday, then refresh
        var yesterday = now.AddDays(-1);
        var yesterdayPeriod = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(yesterdayPeriod);
        daily.RefreshForCurrentPeriod(now);

        Assert.Equal(12, daily.CurrentStreak);
        Assert.Equal(12, daily.BestStreak);
    }

    [Fact]
    public async Task LoadDataAsync_DoesNotCallRefreshTasksForNewDay()
    {
        // After the fix, LoadDataAsync should not reset streaks.
        // Create a daily with a streak, save, then reload — streak should be intact.
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Streak Test";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Build a streak by completing consecutive days
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 5; i >= 1; i--)
        {
            var day = now.AddDays(-i);
            var period = daily.GetPeriodStartFor(day);
            daily.CompleteForPeriod(period);
        }

        Assert.Equal(5, daily.CurrentStreak);

        await vm.SaveDataAsync();

        // Reload from disk — LoadDataAsync should NOT reset the streak
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();
        var reloaded = vm2.Dailies.First(d => d.Title == "Streak Test");

        // Streak should be preserved (LoadDataAsync no longer calls RefreshTasksForNewDay)
        Assert.Equal(5, reloaded.CurrentStreak);
    }

    [Fact]
    public void NewDayCompletion_StreakOfOne_PreservedWithCorrectOrder()
    {
        // Edge case: streak of 1 (completed day-before-yesterday only)
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var anchor = now.AddDays(-5);
        var daily = CreateDailyWithAnchor(anchor);

        var twoDaysAgo = now.AddDays(-2);
        var twoDaysAgoPeriod = daily.GetPeriodStartFor(twoDaysAgo);
        daily.CompleteForPeriod(twoDaysAgoPeriod);

        Assert.Equal(1, daily.CurrentStreak);

        // Correct order: complete yesterday, then refresh
        var yesterday = now.AddDays(-1);
        var yesterdayPeriod = daily.GetPeriodStartFor(yesterday);
        daily.CompleteForPeriod(yesterdayPeriod);
        daily.RefreshForCurrentPeriod(now);

        Assert.Equal(2, daily.CurrentStreak);
    }

    #endregion

    #region User switch integration — new day window flow

    [Fact]
    public async Task UserSwitch_LoadDataThenGetUncompleted_FindsUncompletedDaily()
    {
        // Setup: save a daily that was NOT completed yesterday
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Morning Run";
        vm.AddDaily();
        await vm.SaveDataAsync();

        // Overwrite LastActiveDate to yesterday (SaveDataAsync stamps today)
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        // Simulate user switch: fresh load from disk
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        // LastActiveDate should be yesterday
        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        Assert.Equal(yesterday, vm2.User.LastActiveDate);

        // Condition from OnCurrentUserChanged should be true
        Assert.True(vm2.User.LastActiveDate == yesterday);

        // GetUncompletedDailiesSinceLastActive should find the daily
        var uncompletedDailies = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Single(uncompletedDailies);
        Assert.Equal("Morning Run", uncompletedDailies[0].Title);
    }

    [Fact]
    public async Task UserSwitch_CompletedDailyYesterday_NotInUncompletedList()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Completed Yesterday";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete the daily for yesterday's period
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(yesterday));

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        // Reload from disk
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        // This daily was completed yesterday → should NOT appear
        var uncompletedDailies = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Empty(uncompletedDailies);
    }

    [Fact]
    public async Task UserSwitch_MixedDailies_OnlyUncompletedAppear()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Completed";
        vm.AddDaily();
        vm.NewDailyTitle = "Not Completed";
        vm.AddDaily();
        vm.NewDailyTitle = "Also Not Completed";
        vm.AddDaily();

        var completed = vm.Dailies.First(d => d.Title == "Completed");
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        completed.CompleteForPeriod(completed.GetPeriodStartFor(yesterday));

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        var uncompletedDailies = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Equal(2, uncompletedDailies.Count);
        Assert.DoesNotContain(uncompletedDailies, d => d.Title == "Completed");
        Assert.Contains(uncompletedDailies, d => d.Title == "Not Completed");
        Assert.Contains(uncompletedDailies, d => d.Title == "Also Not Completed");
    }

    [Fact]
    public async Task UserSwitch_FullHandleNewDayFlow_StreakPreserved()
    {
        // Setup: daily with a 3-day streak, last completed 2 days ago (not yesterday)
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Streak Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 4; i >= 2; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(3, daily.CurrentStreak);

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        // Simulate user switch: LoadDataAsync (no RefreshTasksForNewDay)
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();
        var reloaded = vm2.Dailies.First(d => d.Title == "Streak Daily");

        // Streak should be intact (LoadDataAsync doesn't reset)
        Assert.Equal(3, reloaded.CurrentStreak);

        // Simulate HandleNewDay: get unchecked dailies
        var uncompletedDailies = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Single(uncompletedDailies);

        // Simulate ShowNewDayWindow: complete for yesterday
        var yesterday = now.AddDays(-1);
        reloaded.CompleteForPeriod(reloaded.GetPeriodStartFor(yesterday));

        // Streak should be 4 (continued from 3)
        Assert.Equal(4, reloaded.CurrentStreak);

        // Simulate HandleNewDay calls RefreshTasksForNewDay after window
        vm2.RefreshTasksForNewDay();

        // Streak should still be 4
        Assert.Equal(4, reloaded.CurrentStreak);

        // SaveDataAsync (HandleNewDay saves at end)
        await vm2.SaveDataAsync();

        // Verify persisted correctly
        var vm3 = CreateViewModel();
        await vm3.LoadDataAsync();
        var final = vm3.Dailies.First(d => d.Title == "Streak Daily");
        Assert.Equal(4, final.CurrentStreak);
    }

    [Fact]
    public async Task UserSwitch_SaveThenLoad_PreservesLastActiveDateFromDisk()
    {
        // Simulate user A saving data (stamps LastActiveDate = today)
        var vm = CreateViewModel();
        await vm.SaveDataAsync();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), vm.User.LastActiveDate);

        // Manually set LastActiveDate to yesterday and re-save the profile
        // (simulates the JSON file having yesterday's date)
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        // Fresh load should read yesterday from disk
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(-1), vm2.User.LastActiveDate);
    }

    [Fact]
    public async Task UserSwitch_NoDailies_HandleNewDayDoesNotShowWindow()
    {
        // User with no dailies — HandleNewDay should find nothing
        var vm = CreateViewModel();
        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        // LastActiveDate matches yesterday
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(-1), vm2.User.LastActiveDate);

        // But no dailies → empty list
        var uncompletedDailies = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Empty(uncompletedDailies);
    }

    [Fact]
    public async Task UserSwitch_LastActiveDateTwoDaysAgo_ShowsHandleNewDay()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Some Daily";
        vm.AddDaily();
        await vm.SaveDataAsync();

        // LastActiveDate = 2 days ago — should still trigger HandleNewDay
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-2);
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        // Gate condition should pass (lastActive < today)
        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.True(vm2.User.LastActiveDate.HasValue && vm2.User.LastActiveDate.Value < today);

        // HandleNewDay should find the uncompleted daily
        var uncompleted = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Single(uncompleted);
        Assert.Equal("Some Daily", uncompleted[0].Title);
    }

    #endregion

    #region Two-user switch integration tests

    [Fact]
    public async Task TwoUserSwitch_SaveOldThenLoadNew_LastActiveDateCorrect()
    {
        // Setup: two users with separate data directories
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // User A: add a daily and save
        vm.NewDailyTitle = "User A Daily";
        vm.AddDaily();
        await vm.SaveDataAsync(); // stamps User A LastActiveDate = today

        // Switch to User B and set up data with LastActiveDate = yesterday
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.NewDailyTitle = "User B Daily";
        vm.AddDaily();
        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Now switch back to User A
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        await vm.SaveDataAsync(); // stamps today

        // === Simulate OnCurrentUserChanged: A → B ===
        // Step 1: SaveDataAsync (old user A) — stamps today
        await vm.SaveDataAsync();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), vm.User.LastActiveDate);

        // Step 2: RefreshDataDirectory to User B
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();

        // Step 3: LoadDataAsync (new user B)
        await vm.LoadDataAsync();

        // User B's LastActiveDate should be yesterday (from disk), NOT today
        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        Assert.Equal(yesterday, vm.User.LastActiveDate);
    }

    [Fact]
    public async Task TwoUserSwitch_OldUserDataIntact_AfterSwitch()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // User A: add dailies and save
        vm.NewDailyTitle = "A-Daily-1";
        vm.AddDaily();
        vm.NewDailyTitle = "A-Daily-2";
        vm.AddDaily();
        vm.User.Gold = 100.0;
        await vm.SaveDataAsync();
        var userAGold = vm.User.Gold;

        // Setup User B with data
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.NewDailyTitle = "B-Daily-1";
        vm.AddDaily();
        vm.User.Gold = 50.0;
        await vm.SaveDataAsync();

        // Simulate OnCurrentUserChanged: B → A
        await vm.SaveDataAsync(); // save B's data
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // User A's data should be intact
        Assert.Equal(2, vm.Dailies.Count);
        Assert.Contains(vm.Dailies, d => d.Title == "A-Daily-1");
        Assert.Contains(vm.Dailies, d => d.Title == "A-Daily-2");
        Assert.Equal(userAGold, vm.User.Gold);
    }

    [Fact]
    public async Task TwoUserSwitch_HandleNewDay_FindsNewUserUncompletedDailies()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // User A: active today
        vm.NewDailyTitle = "A-Task";
        vm.AddDaily();
        await vm.SaveDataAsync();

        // User B: active yesterday, has uncompleted dailies
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.NewDailyTitle = "B-Uncompleted";
        vm.AddDaily();
        vm.NewDailyTitle = "B-Also-Uncompleted";
        vm.AddDaily();
        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Switch back to A so we can simulate A → B switch
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // === Full OnCurrentUserChanged: A → B ===
        await vm.SaveDataAsync();                        // save A
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();            // switch dir
        await vm.LoadDataAsync();                         // load B

        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        Assert.Equal(yesterday, vm.User.LastActiveDate);

        // HandleNewDay logic
        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(vm.User.LastActiveDate!.Value);
        Assert.Equal(2, uncompleted.Count);
        Assert.Contains(uncompleted, d => d.Title == "B-Uncompleted");
        Assert.Contains(uncompleted, d => d.Title == "B-Also-Uncompleted");
    }

    [Fact]
    public async Task TwoUserSwitch_WeeklyCadence_SamePeriod_ExcludedFromUncompleted()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // Add a weekly daily — yesterday and today are in the same week period
        vm.NewDailyTitle = "Weekly Task";
        vm.AddDaily();
        var weekly = vm.Dailies[0];
        weekly.SetCadence(RepeatCadence.Weekly);
        weekly.SetRepeatEvery(1);

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Reload and check
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        var uncompleted = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);

        // Weekly tasks where today and yesterday share a period should NOT appear
        var weeklyReloaded = vm2.Dailies.First(d => d.Title == "Weekly Task");
        var todayPeriod = weeklyReloaded.GetCurrentPeriodStart();
        var yesterdayPeriod = weeklyReloaded.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1));

        if (todayPeriod == yesterdayPeriod)
        {
            Assert.DoesNotContain(uncompleted, d => d.Title == "Weekly Task");
        }
        else
        {
            // Edge case: if today is Monday, yesterday was Sunday (different week)
            Assert.Contains(uncompleted, d => d.Title == "Weekly Task");
        }
    }

    [Fact]
    public async Task TwoUserSwitch_AllDailiesCompleted_EmptyUncompletedList()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);
        await vm.SaveDataAsync();

        // User B: all dailies completed yesterday
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.NewDailyTitle = "Completed-1";
        vm.AddDaily();
        vm.NewDailyTitle = "Completed-2";
        vm.AddDaily();

        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        foreach (var daily in vm.Dailies)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(yesterday));
        }

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Switch A → B
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        await vm.SaveDataAsync();
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // LastActiveDate == yesterday, but all dailies were completed
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(-1), vm.User.LastActiveDate);
        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(vm.User.LastActiveDate!.Value);
        Assert.Empty(uncompleted); // window should NOT show
    }

    [Fact]
    public async Task TwoUserSwitch_NewUserNullLastActiveDate_SkipsHandleNewDay()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("Brand New User");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);
        await vm.SaveDataAsync();

        // Simulate A → B switch (B has never been used, no user.json or fresh profile)
        await vm.SaveDataAsync();
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);

        // New user's LastActiveDate should be null (or today from SaveDataAsync)
        // Either way, it should NOT equal yesterday
        Assert.NotEqual(yesterday, vm.User.LastActiveDate);
    }

    [Fact]
    public async Task TwoUserSwitch_StreakPreserved_ThroughFullSwitchFlow()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);
        await vm.SaveDataAsync();

        // Setup User B: daily with 5-day streak, last completed day before yesterday
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        vm.NewDailyTitle = "Streak Task";
        vm.AddDaily();
        var daily = vm.Dailies.First(d => d.Title == "Streak Task");
        daily.SetGoldReward(10.0);

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        // Complete for days -6 through -2 (5 consecutive days, NOT yesterday)
        for (int i = 6; i >= 2; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(5, daily.CurrentStreak);

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Switch back to A
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // === Full OnCurrentUserChanged: A → B ===
        // Step 1: Save A
        await vm.SaveDataAsync();

        // Step 2-3: Switch to B, load
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // Verify: streak intact after load (no RefreshTasksForNewDay yet)
        var reloaded = vm.Dailies.First(d => d.Title == "Streak Task");
        Assert.Equal(5, reloaded.CurrentStreak);

        // Step 4: HandleNewDay
        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(vm.User.LastActiveDate!.Value);
        Assert.Single(uncompleted);

        // Complete for yesterday (simulates checking in new day window)
        var yesterday = now.AddDays(-1);
        reloaded.CompleteForPeriod(reloaded.GetPeriodStartFor(yesterday));
        Assert.Equal(6, reloaded.CurrentStreak); // continued!

        // Step 5: RefreshTasksForNewDay (called after HandleNewDay)
        vm.RefreshTasksForNewDay();
        Assert.Equal(6, reloaded.CurrentStreak); // still intact!

        // Step 6: Save
        await vm.SaveDataAsync();

        // Verify persistence
        var vm2 = new MainWindowViewModel(storageService, userService);
        await vm2.LoadDataAsync();
        var final = vm2.Dailies.First(d => d.Title == "Streak Task");
        Assert.Equal(6, final.CurrentStreak);
        Assert.Equal(6, final.BestStreak);
    }

    [Fact]
    public async Task TwoUserSwitch_SaveWritesToOldDirectory_NotNewDirectory()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // User A: save with specific gold
        vm.User.Gold = 999.0;
        await vm.SaveDataAsync();

        // User B: save with different gold
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.User.Gold = 50.0;
        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        // Switch back to A
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // === Simulate OnCurrentUserChanged ordering ===
        // At this point, _dataDirectory points to A. SwitchUserAsync changes _currentUser but NOT _dataDirectory.
        await userService.SwitchUserAsync(userB.Id);

        // SaveDataAsync writes to _dataDirectory (still A's dir!)
        await vm.SaveDataAsync();

        // Now switch directory
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();

        // User B's gold should still be 50 (SaveDataAsync didn't overwrite B's data)
        Assert.Equal(50.0, vm.User.Gold);
    }

    [Fact]
    public async Task TwoUserSwitch_RepeatEvery2Days_SamePeriod_NotInUncompleted()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // Create a daily with RepeatEvery=2 anchored so today and yesterday share a period
        vm.NewDailyTitle = "Every-2-Days";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetRepeatEvery(2);

        // Determine if today and yesterday share a period for this daily
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var todayPeriod = daily.GetCurrentPeriodStart();
        var yesterdayPeriod = daily.GetPeriodStartFor(now.AddDays(-1));

        await vm.SaveDataAsync();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        await storageService.SaveUserProfileAsync(vm.User);

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        var uncompleted = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);

        if (todayPeriod == yesterdayPeriod)
        {
            // Same period — daily should NOT appear (period hasn't changed)
            Assert.DoesNotContain(uncompleted, d => d.Title == "Every-2-Days");
        }
        else
        {
            // Different period — daily should appear
            Assert.Contains(uncompleted, d => d.Title == "Every-2-Days");
        }
    }

    [Fact]
    public async Task TwoUserSwitch_MultipleRapidSwitches_DataStaysConsistent()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var userA = userService.CurrentUser!;
        var userB = await userService.CreateUserAsync("User B");

        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);

        // Setup User A
        vm.NewDailyTitle = "A-Task";
        vm.AddDaily();
        vm.User.Gold = 100.0;
        await vm.SaveDataAsync();

        // Setup User B
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        vm.NewDailyTitle = "B-Task";
        vm.AddDaily();
        vm.User.Gold = 200.0;
        await vm.SaveDataAsync();

        // Rapid switches: B → A → B → A
        for (int i = 0; i < 2; i++)
        {
            await vm.SaveDataAsync();
            await userService.SwitchUserAsync(userA.Id);
            storageService.RefreshDataDirectory();
            await vm.LoadDataAsync();

            Assert.Single(vm.Dailies);
            Assert.Equal("A-Task", vm.Dailies[0].Title);

            await vm.SaveDataAsync();
            await userService.SwitchUserAsync(userB.Id);
            storageService.RefreshDataDirectory();
            await vm.LoadDataAsync();

            Assert.Single(vm.Dailies);
            Assert.Equal("B-Task", vm.Dailies[0].Title);
        }

        // Final check: each user still has correct gold
        await vm.SaveDataAsync();
        await userService.SwitchUserAsync(userA.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        Assert.Equal(100.0, vm.User.Gold);

        await vm.SaveDataAsync();
        await userService.SwitchUserAsync(userB.Id);
        storageService.RefreshDataDirectory();
        await vm.LoadDataAsync();
        Assert.Equal(200.0, vm.User.Gold);
    }

    #endregion

    #region Multi-day gap tests

    [Fact]
    public void MultiDayGap_DailyTask_ShowsAfterTwoDayGap()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Daily Exercise";
        vm.AddDaily();

        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-2);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);
        Assert.Single(uncompleted);
        Assert.Equal("Daily Exercise", uncompleted[0].Title);
    }

    [Fact]
    public void MultiDayGap_DailyTask_ShowsAfterFiveDayGap()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Daily Workout";
        vm.AddDaily();

        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-5);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);
        Assert.Single(uncompleted);
        Assert.Equal("Daily Workout", uncompleted[0].Title);
    }

    [Fact]
    public void MultiDayGap_DailyTask_IncludedWhenCompletedInLastActivePeriod_WithGap()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Daily Exercise";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete the task for 3 days ago (the last active period)
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var threeDaysAgo = now.AddDays(-3);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(threeDaysAgo));

        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-3);

        // With gap-based filter, this daily IS included because there's a gap
        // between LastCompletionPeriod (3 days ago) and the previous period (yesterday)
        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);
        Assert.Single(uncompleted);
        Assert.Equal("Daily Exercise", uncompleted[0].Title);
    }

    [Fact]
    public void MultiDayGap_MonthlyTask_ShowsWhenMonthChanged()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Monthly Review";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Monthly);
        daily.SetRepeatEvery(1);

        // LastActiveDate = last day of the previous month (guaranteed different period from today)
        var firstOfThisMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
        var lastOfPrevMonth = firstOfThisMonth.AddDays(-1);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastOfPrevMonth);
        Assert.Single(uncompleted);
        Assert.Equal("Monthly Review", uncompleted[0].Title);
    }

    [Fact]
    public void MultiDayGap_MonthlyTask_ExcludedWhenSameMonth()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Monthly Review";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Monthly);
        daily.SetRepeatEvery(1);

        // Use first of THIS month as LastActiveDate
        var firstOfThisMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(firstOfThisMonth);
        Assert.Empty(uncompleted);
    }

    [Fact]
    public void MultiDayGap_MonthlyTask_CompletedInPreviousMonth_Excluded()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Monthly Review";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Monthly);
        daily.SetRepeatEvery(1);

        // Complete the task for last month's period
        var firstOfThisMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
        var lastOfPrevMonth = firstOfThisMonth.AddDays(-1);
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var lastOfPrevMonthTime = new DateTimeOffset(lastOfPrevMonth.ToDateTime(new TimeOnly(12, 0)), now.Offset);
        var prevMonthPeriod = daily.GetPeriodStartFor(lastOfPrevMonthTime);
        daily.CompleteForPeriod(prevMonthPeriod);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastOfPrevMonth);
        Assert.Empty(uncompleted);
    }

    [Fact]
    public void MultiDayGap_WeeklyTask_ShowsWhenWeekChanged()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Weekly Report";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetRepeatEvery(1);

        // LastActiveDate = 8 days ago (guaranteed different week)
        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-8);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);
        Assert.Single(uncompleted);
        Assert.Equal("Weekly Report", uncompleted[0].Title);
    }

    [Fact]
    public void MultiDayGap_WeeklyTask_ExcludedWhenSameWeek()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Weekly Report";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetRepeatEvery(1);

        // Use the current weekly period start as lastActiveDate
        var currentPeriod = daily.GetCurrentPeriodStart();

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(currentPeriod);
        Assert.Empty(uncompleted);
    }

    [Fact]
    public void MultiDayGap_GateCondition_PassesForFiveDayGap()
    {
        var vm = CreateViewModel();
        vm.User.LastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-5);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var shouldShow = vm.User.LastActiveDate.HasValue && vm.User.LastActiveDate.Value < today;
        Assert.True(shouldShow);
    }

    [Fact]
    public async Task MultiDayGap_FullFlow_MonthlyTaskCompletedForPreviousPeriod()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Monthly Goal";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetCadence(RepeatCadence.Monthly);
        daily.SetRepeatEvery(1);
        daily.SetGoldReward(10.0);

        // Set LastActiveDate to last day of previous month
        var firstOfThisMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
        var lastOfPrevMonth = firstOfThisMonth.AddDays(-1);
        vm.User.LastActiveDate = lastOfPrevMonth;
        await vm.SaveDataAsync();
        vm.User.LastActiveDate = lastOfPrevMonth;
        await vm.StorageService.SaveUserProfileAsync(vm.User);

        // Reload
        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        // Gate passes
        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.True(vm2.User.LastActiveDate.HasValue && vm2.User.LastActiveDate.Value < today);

        // Find uncompleted
        var uncompleted = vm2.GetUncompletedDailiesSinceLastActive(vm2.User.LastActiveDate!.Value);
        Assert.Single(uncompleted);

        // Simulate completing via new day window using GetPreviousPeriodStart
        var monthlyTask = uncompleted[0];
        var previousPeriod = monthlyTask.GetPreviousPeriodStart();
        monthlyTask.CompleteForPeriod(previousPeriod);

        // Should have completed for the previous month
        Assert.Equal(previousPeriod, monthlyTask.LastCompletionPeriod);
        Assert.NotNull(monthlyTask.LastCompletedDate);

        // The previous period should be the first of the previous month
        var firstOfPrevMonth = firstOfThisMonth.AddMonths(-1);
        Assert.Equal(firstOfPrevMonth, previousPeriod);
    }

    [Fact]
    public void MultiDayGap_MixedCadences_CorrectlyFiltered()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Daily Task";
        vm.AddDaily();
        vm.NewDailyTitle = "Monthly Task";
        vm.AddDaily();
        vm.NewDailyTitle = "Weekly Task";
        vm.AddDaily();

        var dailyTask = vm.Dailies.First(d => d.Title == "Daily Task");
        var monthlyTask = vm.Dailies.First(d => d.Title == "Monthly Task");
        var weeklyTask = vm.Dailies.First(d => d.Title == "Weekly Task");

        monthlyTask.SetCadence(RepeatCadence.Monthly);
        monthlyTask.SetRepeatEvery(1);

        weeklyTask.SetCadence(RepeatCadence.Weekly);
        weeklyTask.SetRepeatEvery(1);

        // LastActiveDate = 3 days ago
        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-3);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);

        // Daily task: 3 days ago is a different period from today → included
        Assert.Contains(uncompleted, d => d.Title == "Daily Task");

        // Monthly task: depends on whether 3 days ago is in the same month
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var lastActiveTime = new DateTimeOffset(lastActiveDate.ToDateTime(new TimeOnly(12, 0)), now.Offset);
        var monthlyCurrentPeriod = monthlyTask.GetCurrentPeriodStart();
        var monthlyLastActivePeriod = monthlyTask.GetPeriodStartFor(lastActiveTime);

        if (monthlyCurrentPeriod != monthlyLastActivePeriod)
        {
            Assert.Contains(uncompleted, d => d.Title == "Monthly Task");
        }
        else
        {
            Assert.DoesNotContain(uncompleted, d => d.Title == "Monthly Task");
        }

        // Weekly task: depends on whether 3 days ago is in the same week
        var weeklyCurrentPeriod = weeklyTask.GetCurrentPeriodStart();
        var weeklyLastActivePeriod = weeklyTask.GetPeriodStartFor(lastActiveTime);

        if (weeklyCurrentPeriod != weeklyLastActivePeriod)
        {
            Assert.Contains(uncompleted, d => d.Title == "Weekly Task");
        }
        else
        {
            Assert.DoesNotContain(uncompleted, d => d.Title == "Weekly Task");
        }
    }

    [Fact]
    public async Task MultiDayGap_DailyTask_StreakResets_WhenMultipleDaysMissed()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Streak Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        // Build a 5-day streak ending 4 days ago
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 8; i >= 4; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(5, daily.CurrentStreak);

        // App unused for 3 days (lastActive = 3 days ago)
        var lastActiveDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-3);

        var uncompleted = vm.GetUncompletedDailiesSinceLastActive(lastActiveDate);
        Assert.Single(uncompleted);

        // Complete via new day window (GetPreviousPeriodStart = yesterday)
        var previousPeriod = daily.GetPreviousPeriodStart();
        daily.CompleteForPeriod(previousPeriod);

        // Streak should reset to 1 because there's a gap between -4 and yesterday
        Assert.Equal(1, daily.CurrentStreak);
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

    private static DailyTask CreateDailyWithAnchor(DateTimeOffset anchor)
    {
        var daily = new DailyTask
        {
            CreatedAt = anchor
        };
        daily.UpdateTitle("Test Daily");
        daily.SetCadence(RepeatCadence.Daily);
        daily.SetRepeatEvery(1);
        return daily;
    }

    #endregion
}
