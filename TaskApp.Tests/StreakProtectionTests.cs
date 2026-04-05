using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

/// <summary>
/// Tests for streak protection (new day window protect toggle) and vacation mode behavior.
/// </summary>
public class StreakProtectionTests : IDisposable
{
    private readonly string _tempDir;

    public StreakProtectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region NewDayViewModel — IsProtected mutual exclusivity

    [Fact]
    public void ChecklistItem_SetProtected_ClearsChecked()
    {
        var daily = CreateDaily();
        var item = new DailyChecklistItem { Daily = daily, IsChecked = true };

        item.IsProtected = true;

        Assert.True(item.IsProtected);
        Assert.False(item.IsChecked);
    }

    [Fact]
    public void ChecklistItem_SetChecked_ClearsProtected()
    {
        var daily = CreateDaily();
        var item = new DailyChecklistItem { Daily = daily, IsProtected = true };

        item.IsChecked = true;

        Assert.True(item.IsChecked);
        Assert.False(item.IsProtected);
    }

    [Fact]
    public void ChecklistItem_ProtectionCost_ReadsFromDaily()
    {
        var daily = CreateDailyWithGap();
        daily.SetStreakProtectionCost(3.5);
        var item = new DailyChecklistItem { Daily = daily };

        Assert.Equal(3.5, item.ProtectionCost);
    }

    [Fact]
    public void ChecklistItem_ProtectionCost_DefaultsToOne()
    {
        var daily = CreateDailyWithGap();
        var item = new DailyChecklistItem { Daily = daily };

        Assert.Equal(1.0, item.ProtectionCost);
    }

    #endregion

    #region NewDayViewModel — TotalProtectionCost and CanAffordProtections

    [Fact]
    public void TotalProtectionCost_SumsOnlyProtectedItems()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDailyWithGap(); d1.SetStreakProtectionCost(2.0);
        var d2 = CreateDailyWithGap(); d2.SetStreakProtectionCost(3.0);
        var d3 = CreateDailyWithGap(); d3.SetStreakProtectionCost(5.0);
        vm.SetUncompletedDailies(new() { d1, d2, d3 });

        vm.UncompletedDailies[0].IsProtected = true;
        vm.UncompletedDailies[1].IsChecked = true; // checked, not protected
        vm.UncompletedDailies[2].IsProtected = true;

        Assert.Equal(7.0, vm.TotalProtectionCost);
    }

    [Fact]
    public void TotalProtectionCost_ZeroWhenNoneProtected()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDaily();
        vm.SetUncompletedDailies(new() { d1 });
        vm.UncompletedDailies[0].IsChecked = true;

        Assert.Equal(0.0, vm.TotalProtectionCost);
    }

    [Fact]
    public void CanAffordProtections_TrueWhenEnoughGold()
    {
        var vm = new NewDayViewModel { UserGold = 10.0 };
        var d1 = CreateDailyWithGap(); d1.SetStreakProtectionCost(3.0);
        var d2 = CreateDailyWithGap(); d2.SetStreakProtectionCost(3.0);
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[0].IsProtected = true;
        vm.UncompletedDailies[1].IsProtected = true;

        Assert.True(vm.CanAffordProtections);
    }

    [Fact]
    public void CanAffordProtections_FalseWhenNotEnoughGold()
    {
        var vm = new NewDayViewModel { UserGold = 2.0 };
        var d1 = CreateDailyWithGap(); d1.SetStreakProtectionCost(3.0);
        vm.SetUncompletedDailies(new() { d1 });

        vm.UncompletedDailies[0].IsProtected = true;

        Assert.False(vm.CanAffordProtections);
    }

    [Fact]
    public void CanAffordProtections_TrueWhenNoneProtected()
    {
        var vm = new NewDayViewModel { UserGold = 0.0 };
        var d1 = CreateDaily();
        vm.SetUncompletedDailies(new() { d1 });

        Assert.True(vm.CanAffordProtections);
    }

    [Fact]
    public void CanAffordProtections_AccountsForProjectedGoldFromCheckedItems()
    {
        // User has 2 gold, a checked daily earns 5 gold, protection costs 3 gold
        // Projected: 2 + 5 = 7 >= 3 → can afford
        var vm = new NewDayViewModel { UserGold = 2.0 };
        var d1 = CreateDailyWithGap(); d1.SetGoldReward(5.0);
        var d2 = CreateDailyWithGap(); d2.SetStreakProtectionCost(3.0);
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[0].IsChecked = true;
        vm.UncompletedDailies[1].IsProtected = true;

        Assert.True(vm.CanAffordProtections);
    }

    [Fact]
    public void CanAffordProtections_FalseWhenProjectedGoldStillInsufficient()
    {
        // User has 0 gold, a checked daily earns 2 gold, protection costs 5 gold
        // Projected: 0 + 2 = 2 < 5 → can't afford
        var vm = new NewDayViewModel { UserGold = 0.0 };
        var d1 = CreateDailyWithGap(); d1.SetGoldReward(2.0);
        var d2 = CreateDailyWithGap(); d2.SetStreakProtectionCost(5.0);
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[0].IsChecked = true;
        vm.UncompletedDailies[1].IsProtected = true;

        Assert.False(vm.CanAffordProtections);
    }

    [Fact]
    public void ProjectedGoldEarned_SumsOnlyCheckedItems()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDailyWithGap(); d1.SetGoldReward(3.0);
        var d2 = CreateDailyWithGap(); d2.SetGoldReward(7.0);
        var d3 = CreateDailyWithGap(); d3.SetGoldReward(5.0);
        vm.SetUncompletedDailies(new() { d1, d2, d3 });

        vm.UncompletedDailies[0].IsChecked = true;
        vm.UncompletedDailies[2].IsChecked = true;
        // d2 is neither checked nor protected

        Assert.Equal(3.0 + 5.0, vm.ProjectedGoldEarned);
    }

    #endregion

    #region NewDayViewModel — ProtectAll and UncheckAll

    [Fact]
    public void ProtectAll_ProtectsUncheckedItems()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDaily();
        var d2 = CreateDaily();
        var d3 = CreateDaily();
        vm.SetUncompletedDailies(new() { d1, d2, d3 });

        vm.UncompletedDailies[0].IsChecked = true;
        vm.ProtectAll();

        Assert.True(vm.UncompletedDailies[0].IsChecked);
        Assert.False(vm.UncompletedDailies[0].IsProtected);
        Assert.True(vm.UncompletedDailies[1].IsProtected);
        Assert.True(vm.UncompletedDailies[2].IsProtected);
    }

    [Fact]
    public void UncheckAll_ClearsBothCheckedAndProtected()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDaily();
        var d2 = CreateDaily();
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[0].IsChecked = true;
        vm.UncompletedDailies[1].IsProtected = true;

        vm.UncheckAll();

        Assert.False(vm.UncompletedDailies[0].IsChecked);
        Assert.False(vm.UncompletedDailies[1].IsProtected);
    }

    [Fact]
    public void CheckAll_SetsCheckedForAll()
    {
        var vm = new NewDayViewModel();
        var d1 = CreateDaily();
        var d2 = CreateDaily();
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[1].IsProtected = true;

        vm.CheckAll();

        Assert.True(vm.UncompletedDailies[0].IsChecked);
        Assert.True(vm.UncompletedDailies[1].IsChecked);
        // IsChecked clears IsProtected
        Assert.False(vm.UncompletedDailies[1].IsProtected);
    }

    #endregion

    #region New day window — protect deducts gold and logs

    [Fact]
    public async Task NewDayProtect_DeductsGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Protect Me";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(3.0);

        // Build a streak
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-2)));
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-1)));
        Assert.Equal(2, daily.CurrentStreak);

        // Simulate protect in new day window
        var previousPeriodStart = daily.GetPreviousPeriodStart();
        daily.ProtectStreak(previousPeriodStart);
        vm.AddGold(-daily.StreakProtectionCost);

        Assert.Equal(7.0, vm.User.Gold);
        Assert.Equal(2, daily.CurrentStreak); // unchanged
    }

    [Fact]
    public async Task NewDayProtect_LogsDailyStreakProtected()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Log Me";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(2.0);

        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));

        var previousPeriodStart = daily.GetPreviousPeriodStart();
        daily.ProtectStreak(previousPeriodStart);
        vm.AddGold(-daily.StreakProtectionCost);

        var timestamp = DateTimeOffset.UtcNow;
        await vm.LogDailyStreakProtectedAsync(daily, daily.StreakProtectionCost, timestamp);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyStreakProtected);

        Assert.Equal(daily.Id, entry.TaskId);
        Assert.Equal(-2.0, entry.GoldDelta);
        Assert.Equal("Log Me", entry.TitleSnapshot);
    }

    [Fact]
    public async Task NewDayProtect_StreakSurvivesRefresh()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Survive Refresh";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 3; i >= 1; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(3, daily.CurrentStreak);

        // Protect yesterday's period
        var previousPeriodStart = daily.GetPreviousPeriodStart();
        daily.ProtectStreak(previousPeriodStart);

        // Refresh for current period — streak should survive
        vm.RefreshTasksForNewDay();

        Assert.Equal(3, daily.CurrentStreak);
    }

    [Fact]
    public async Task NewDayProtect_MixedCheckAndProtect()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 20.0;

        vm.NewDailyTitle = "Checked Daily";
        vm.AddDaily();
        vm.NewDailyTitle = "Protected Daily";
        vm.AddDaily();

        var checkedDaily = vm.Dailies.First(d => d.Title == "Checked Daily");
        var protectedDaily = vm.Dailies.First(d => d.Title == "Protected Daily");
        checkedDaily.SetGoldReward(5.0);
        protectedDaily.SetStreakProtectionCost(2.0);

        // Build streaks — complete 2 days ago (not yesterday, so yesterday is "uncompleted")
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        checkedDaily.CompleteForPeriod(checkedDaily.GetPeriodStartFor(now.AddDays(-2)));
        protectedDaily.CompleteForPeriod(protectedDaily.GetPeriodStartFor(now.AddDays(-2)));

        Assert.Equal(1, checkedDaily.CurrentStreak);
        Assert.Equal(1, protectedDaily.CurrentStreak);

        // Simulate new day window: check one for yesterday, protect the other
        var checkedPrevPeriod = checkedDaily.GetPreviousPeriodStart();
        checkedDaily.CompleteForPeriod(checkedPrevPeriod);
        vm.AddGold(checkedDaily.GetGoldRewardWithBonus());

        var protectedPrevPeriod = protectedDaily.GetPreviousPeriodStart();
        protectedDaily.ProtectStreak(protectedPrevPeriod);
        vm.AddGold(-protectedDaily.StreakProtectionCost);

        // Checked daily: streak incremented, gold earned
        Assert.Equal(2, checkedDaily.CurrentStreak);
        // Protected daily: streak preserved, gold spent
        Assert.Equal(1, protectedDaily.CurrentStreak);
        Assert.Equal(20.0 + 5.0 - 2.0, vm.User.Gold);
    }

    #endregion

    #region Vacation mode — ProtectAllStreaks

    [Fact]
    public void VacationMode_ProtectAllStreaks_PreservesAllStreaks()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Daily A";
        vm.AddDaily();
        vm.NewDailyTitle = "Daily B";
        vm.AddDaily();

        var dailyA = vm.Dailies.First(d => d.Title == "Daily A");
        var dailyB = vm.Dailies.First(d => d.Title == "Daily B");

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        dailyA.CompleteForPeriod(dailyA.GetPeriodStartFor(now.AddDays(-1)));
        dailyB.CompleteForPeriod(dailyB.GetPeriodStartFor(now.AddDays(-2)));
        dailyB.CompleteForPeriod(dailyB.GetPeriodStartFor(now.AddDays(-1)));

        Assert.Equal(1, dailyA.CurrentStreak);
        Assert.Equal(2, dailyB.CurrentStreak);

        // Protect all streaks (vacation mode)
        vm.ProtectAllStreaks();

        // Refresh — streaks should survive
        vm.RefreshTasksForNewDay();

        Assert.Equal(1, dailyA.CurrentStreak);
        Assert.Equal(2, dailyB.CurrentStreak);
    }

    [Fact]
    public void VacationMode_ProtectAllStreaks_NoGoldCost()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 5.0;

        vm.NewDailyTitle = "Vacation Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(10.0);

        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));

        vm.ProtectAllStreaks();

        // Gold should be unchanged — vacation protection is free
        Assert.Equal(5.0, vm.User.Gold);
    }

    [Fact]
    public void VacationMode_ProtectAllStreaks_SkipsDailiesWithNoStreakOrCompletion()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Never Completed";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        Assert.Equal(0, daily.CurrentStreak);
        Assert.Null(daily.LastCompletionPeriod);

        vm.ProtectAllStreaks();

        // Should not have been touched
        Assert.Equal(0, daily.CurrentStreak);
        Assert.Null(daily.LastCompletionPeriod);
    }

    [Fact]
    public void VacationMode_HandleNewDay_SkipsNewDayWindow()
    {
        // This tests the logic path: in vacation mode, HandleNewDay should
        // call ProtectAllStreaks and NOT show the new day window.
        // We verify by checking that uncompleted dailies get protected.
        var vm = CreateViewModel();
        vm.IsVacationMode = true;

        vm.NewDailyTitle = "Vacation Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-1)));
        Assert.Equal(1, daily.CurrentStreak);

        // Simulate HandleNewDay in vacation mode:
        // 1. ProtectAllStreaks (no window)
        vm.ProtectAllStreaks();
        // 2. RefreshTasksForNewDay
        vm.RefreshTasksForNewDay();

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void VacationMode_MultiDayGap_StreakSurvives()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Multi Day";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Build streak, last completed 5 days ago
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 7; i >= 5; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(3, daily.CurrentStreak);

        // ProtectAllStreaks advances to previous period (yesterday)
        vm.ProtectAllStreaks();

        // Refresh — streak survives because LastCompletionPeriod is now yesterday
        vm.RefreshTasksForNewDay();

        Assert.Equal(3, daily.CurrentStreak);
    }

    [Fact]
    public void VacationMode_ToggleOff_ProtectsStreaks()
    {
        var vm = CreateViewModel();
        vm.IsVacationMode = true;

        vm.NewDailyTitle = "Toggle Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-1)));
        Assert.Equal(1, daily.CurrentStreak);

        // Simulate toggling off vacation: protect then refresh
        vm.ProtectAllStreaks();
        vm.IsVacationMode = false;
        vm.RefreshTasksForNewDay();

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void VacationMode_ToggleOff_DoesNotUncheckCompletedDailies()
    {
        var vm = CreateViewModel();
        vm.IsVacationMode = true;

        vm.NewDailyTitle = "Completed Today";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete for today's period
        daily.Complete();
        Assert.Equal(1, daily.CurrentStreak);
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // Simulate toggling off vacation — ProtectAllStreaks should not overwrite today's completion
        vm.ProtectAllStreaks();
        vm.IsVacationMode = false;

        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void VacationMode_CanStillComplete_DuringVacation()
    {
        var vm = CreateViewModel();
        vm.IsVacationMode = true;

        vm.NewDailyTitle = "Active Vacation";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));
        Assert.Equal(1, daily.CurrentStreak);

        // User can still check off dailies during vacation
        daily.Complete();

        Assert.Equal(2, daily.CurrentStreak);
        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    #endregion

    #region Serialization round-trip

    [Fact]
    public void StreakProtectionCost_SurvivesRoundTrip()
    {
        var original = new DailyTask
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        original.UpdateTitle("Round Trip");
        original.SetStreakProtectionCost(7.5);

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(7.5, restored.StreakProtectionCost);
    }

    [Fact]
    public void StreakProtectionCost_DefaultOnOldData()
    {
        // Simulate old data that doesn't have StreakProtectionCost set
        var data = new TaskApp.Data.DailyTaskData
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Title = "Old Daily",
            GoldReward = 1.0,
            // StreakProtectionCost defaults to 1.0 in DailyTaskData
        };

        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(1.0, restored.StreakProtectionCost);
    }

    [Fact]
    public async Task StreakProtectionCost_PersistsThroughSaveLoad()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Persist Test";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(4.0);

        await vm.SaveDataAsync();

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();
        var reloaded = vm2.Dailies.First(d => d.Title == "Persist Test");

        Assert.Equal(4.0, reloaded.StreakProtectionCost);
    }

    #endregion

    #region Undo DailyStreakProtected

    [Fact]
    public async Task Undo_DailyStreakProtected_RefundsGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Undo Test";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(3.0);

        // Protect the streak — capture previous period for undo support
        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));
        var previousPeriod = daily.LastCompletionPeriod;
        daily.ProtectStreak(daily.GetPreviousPeriodStart());
        vm.AddGold(-daily.StreakProtectionCost);
        await vm.LogDailyStreakProtectedAsync(daily, daily.StreakProtectionCost, DateTimeOffset.UtcNow, previousPeriod);

        Assert.Equal(7.0, vm.User.Gold);

        // Undo
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyStreakProtected);
        var result = await vm.UndoLogEntryAsync(entry);

        Assert.True(result);
        Assert.Equal(10.0, vm.User.Gold);
        Assert.Equal(previousPeriod, daily.LastCompletionPeriod);
    }

    [Fact]
    public async Task Undo_DailyStreakProtected_RemovesLogEntry()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Undo Remove";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetStreakProtectionCost(1.0);

        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));
        var previousPeriod = daily.LastCompletionPeriod;
        daily.ProtectStreak(daily.GetPreviousPeriodStart());
        vm.AddGold(-daily.StreakProtectionCost);
        await vm.LogDailyStreakProtectedAsync(daily, daily.StreakProtectionCost, DateTimeOffset.UtcNow, previousPeriod);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyStreakProtected);
        await vm.UndoLogEntryAsync(entry);

        var logsAfter = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        Assert.DoesNotContain(logsAfter, e => e.Type == LogType.DailyStreakProtected);
    }

    [Fact]
    public async Task Undo_DailyStreakProtected_RollsBackLastCompletionPeriod()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Rollback Period";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Complete 2 days ago — gives a streak of 1
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-2)));
        var periodBeforeProtect = daily.LastCompletionPeriod;
        Assert.Equal(1, daily.CurrentStreak);

        // Protect yesterday's period
        daily.ProtectStreak(daily.GetPreviousPeriodStart());
        var periodAfterProtect = daily.LastCompletionPeriod;
        Assert.NotEqual(periodBeforeProtect, periodAfterProtect);

        vm.AddGold(-daily.StreakProtectionCost);
        await vm.LogDailyStreakProtectedAsync(daily, daily.StreakProtectionCost, DateTimeOffset.UtcNow, periodBeforeProtect);

        // Undo — should roll back LastCompletionPeriod
        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyStreakProtected);
        await vm.UndoLogEntryAsync(entry);

        Assert.Equal(periodBeforeProtect, daily.LastCompletionPeriod);
    }

    [Fact]
    public async Task Undo_DailyStreakProtected_NullPreviousPeriod_SetsNull()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10.0;

        vm.NewDailyTitle = "Null Period";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        // Create a protection log entry without PreviousLastCompletionPeriod (old data)
        daily.CompleteForPeriod(daily.GetPreviousPeriodStart());
        daily.ProtectStreak(daily.GetPreviousPeriodStart());
        vm.AddGold(-daily.StreakProtectionCost);
        await vm.LogDailyStreakProtectedAsync(daily, daily.StreakProtectionCost, DateTimeOffset.UtcNow);

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        var entry = logs.First(e => e.Type == LogType.DailyStreakProtected);

        // Undo gracefully handles null PreviousLastCompletionPeriod
        var result = await vm.UndoLogEntryAsync(entry);
        Assert.True(result);
        Assert.Null(daily.LastCompletionPeriod);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void ProtectStreak_ConsecutiveProtections_KeepsStreak()
    {
        var anchor = new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero);
        var daily = new DailyTask { CreatedAt = anchor };
        daily.UpdateTitle("Consecutive Protect");
        daily.SetCadence(RepeatCadence.Daily);

        // Complete day 8
        daily.CompleteForPeriod(new DateOnly(2026, 3, 8));
        Assert.Equal(1, daily.CurrentStreak);

        // Protect days 9, 10, 11 consecutively
        daily.ProtectStreak(new DateOnly(2026, 3, 9));
        daily.ProtectStreak(new DateOnly(2026, 3, 10));
        daily.ProtectStreak(new DateOnly(2026, 3, 11));

        Assert.Equal(1, daily.CurrentStreak); // never incremented

        // Complete day 12 — continues from protected period
        daily.CompleteForPeriod(new DateOnly(2026, 3, 12));
        Assert.Equal(2, daily.CurrentStreak);
    }

    [Fact]
    public void ProtectAllStreaks_MultipleDailies_DifferentCadences()
    {
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Daily Task";
        vm.AddDaily();
        vm.NewDailyTitle = "Weekly Task";
        vm.AddDaily();

        var dailyTask = vm.Dailies.First(d => d.Title == "Daily Task");
        var weeklyTask = vm.Dailies.First(d => d.Title == "Weekly Task");
        weeklyTask.SetCadence(RepeatCadence.Weekly);

        var now = DateTimeOffset.UtcNow.ToLocalTime();
        dailyTask.CompleteForPeriod(dailyTask.GetPeriodStartFor(now.AddDays(-1)));
        weeklyTask.CompleteForPeriod(weeklyTask.GetPeriodStartFor(now.AddDays(-7)));

        Assert.Equal(1, dailyTask.CurrentStreak);
        Assert.Equal(1, weeklyTask.CurrentStreak);

        vm.ProtectAllStreaks();
        vm.RefreshTasksForNewDay();

        Assert.Equal(1, dailyTask.CurrentStreak);
        Assert.Equal(1, weeklyTask.CurrentStreak);
    }

    [Fact]
    public async Task VacationMode_NoLogsCreated()
    {
        var vm = CreateViewModel();
        vm.IsVacationMode = true;

        vm.NewDailyTitle = "No Log";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        daily.CompleteForPeriod(daily.GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1)));

        // Simulate vacation mode HandleNewDay — just protect, no logging
        vm.ProtectAllStreaks();
        vm.RefreshTasksForNewDay();
        await vm.SaveDataAsync();

        var logs = await vm.StorageService.LoadRecentLogEntriesAsync(10);
        Assert.DoesNotContain(logs, e => e.Type == LogType.DailyStreakProtected);
    }

    [Fact]
    public async Task VacationMode_IsVacationMode_PersistsThroughSaveLoad()
    {
        var vm = CreateViewModel();
        vm.IsVacationMode = true;
        await vm.SaveDataAsync();

        var vm2 = CreateViewModel();
        await vm2.LoadDataAsync();

        Assert.True(vm2.IsVacationMode);
    }

    [Fact]
    public async Task VacationMode_FullFlow_MultiDayVacation()
    {
        // Simulate: user has streaks, goes on vacation for several days, comes back
        var vm = CreateViewModel();

        vm.NewDailyTitle = "Streak Daily";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetGoldReward(5.0);

        // Build a 5-day streak
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        for (int i = 6; i >= 2; i--)
        {
            daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-i)));
        }
        Assert.Equal(5, daily.CurrentStreak);

        // Enable vacation mode
        vm.IsVacationMode = true;
        vm.User.Gold = 100.0;
        await vm.SaveDataAsync();

        // Simulate HandleNewDay while on vacation (e.g., app opened after 3 days)
        vm.ProtectAllStreaks();
        vm.RefreshTasksForNewDay();

        // Streak is preserved, gold unchanged
        Assert.Equal(5, daily.CurrentStreak);
        Assert.Equal(100.0, vm.User.Gold);

        // Toggle off vacation
        vm.ProtectAllStreaks(); // called on toggle-off
        vm.IsVacationMode = false;
        await vm.SaveDataAsync();

        // Refresh again — streak still intact
        vm.RefreshTasksForNewDay();
        Assert.Equal(5, daily.CurrentStreak);

        // Now complete the daily normally
        daily.Complete();
        Assert.Equal(6, daily.CurrentStreak);
    }

    #endregion

    #region Multi-day gap protection cost

    [Fact]
    public void ProtectionCost_MultiDayGap_MultipliesByMissedPeriods()
    {
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var daily = new DailyTask { CreatedAt = now.AddDays(-10) };
        daily.UpdateTitle("Multi-Gap Daily");
        daily.SetCadence(RepeatCadence.Daily);
        daily.SetRepeatEvery(1);
        daily.SetStreakProtectionCost(2.0);

        // Complete for 4 days ago — 3 missed periods (3, 2, yesterday)
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-4)));

        var item = new DailyChecklistItem { Daily = daily };

        Assert.Equal(3, item.MissedPeriodCount);
        Assert.Equal(6.0, item.ProtectionCost); // 3 × 2.0
    }

    [Fact]
    public void ProtectionCost_NoStreak_IsZero()
    {
        var daily = CreateDaily();
        daily.SetStreakProtectionCost(5.0);

        var item = new DailyChecklistItem { Daily = daily };

        Assert.Equal(0, item.MissedPeriodCount);
        Assert.Equal(0.0, item.ProtectionCost);
    }

    [Fact]
    public void TotalProtectionCost_MultiDayGap_SumsMissedPeriodsTimesUnitCost()
    {
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var d1 = new DailyTask { CreatedAt = now.AddDays(-10) };
        d1.UpdateTitle("Daily 1");
        d1.SetCadence(RepeatCadence.Daily);
        d1.SetRepeatEvery(1);
        d1.SetStreakProtectionCost(1.0);
        d1.CompleteForPeriod(d1.GetPeriodStartFor(now.AddDays(-3))); // 2 missed

        var d2 = new DailyTask { CreatedAt = now.AddDays(-10) };
        d2.UpdateTitle("Daily 2");
        d2.SetCadence(RepeatCadence.Daily);
        d2.SetRepeatEvery(1);
        d2.SetStreakProtectionCost(3.0);
        d2.CompleteForPeriod(d2.GetPeriodStartFor(now.AddDays(-4))); // 3 missed

        var vm = new NewDayViewModel { UserGold = 100.0 };
        vm.SetUncompletedDailies(new() { d1, d2 });

        vm.UncompletedDailies[0].IsProtected = true;
        vm.UncompletedDailies[1].IsProtected = true;

        // d1: 2 × 1.0 = 2.0, d2: 3 × 3.0 = 9.0 → total = 11.0
        Assert.Equal(11.0, vm.TotalProtectionCost);
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

    /// <summary>
    /// Creates a daily with a 1-period gap (completed 2 days ago, yesterday is missed).
    /// GetMissedPeriodCount() returns 1 so ProtectionCost = 1 × StreakProtectionCost.
    /// </summary>
    private static DailyTask CreateDailyWithGap()
    {
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var daily = new DailyTask { CreatedAt = now.AddDays(-5) };
        daily.UpdateTitle("Test Daily");
        daily.SetCadence(RepeatCadence.Daily);
        daily.SetRepeatEvery(1);
        daily.CompleteForPeriod(daily.GetPeriodStartFor(now.AddDays(-2)));
        return daily;
    }

    #endregion
}
