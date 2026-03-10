using System;
using System.IO;
using System.Threading.Tasks;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class ManualDurationLoggingTests : IDisposable
{
    private readonly string _tempDir;

    public ManualDurationLoggingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region TaskFormViewModel.LogManualDuration

    [Fact]
    public void LogManualDuration_FiresEvent_WithCorrectDuration()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test Habit");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);
        TimeSpan? captured = null;
        string? capturedTitle = null;
        Guid? capturedTaskId = null;
        vm.RequestLogManualDuration += (duration, title, taskId, rewardId) =>
        {
            captured = duration;
            capturedTitle = title;
            capturedTaskId = taskId;
        };

        vm.ManualHours = 1;
        vm.ManualMinutes = 30;
        vm.ManualSeconds = 15;
        vm.LogManualDuration();

        Assert.NotNull(captured);
        Assert.Equal(new TimeSpan(1, 30, 15), captured.Value);
        Assert.Equal("Test Habit", capturedTitle);
        Assert.Equal(habit.Id, capturedTaskId);
    }

    [Fact]
    public void LogManualDuration_ResetsInputs_AfterSuccess()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);
        vm.RequestLogManualDuration += (_, _, _, _) => { };

        vm.ManualHours = 2;
        vm.ManualMinutes = 45;
        vm.ManualSeconds = 30;
        vm.LogManualDuration();

        Assert.Equal(0, vm.ManualHours);
        Assert.Equal(0, vm.ManualMinutes);
        Assert.Equal(0, vm.ManualSeconds);
    }

    [Fact]
    public void LogManualDuration_ShowsSuccessStatus()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);
        vm.RequestLogManualDuration += (_, _, _, _) => { };

        vm.ManualHours = 0;
        vm.ManualMinutes = 45;
        vm.ManualSeconds = 0;
        vm.LogManualDuration();

        Assert.Contains("00:45:00", vm.ManualDurationStatus);
        Assert.Contains("successfully", vm.ManualDurationStatus);
    }

    [Fact]
    public void LogManualDuration_ZeroDuration_ShowsError_DoesNotFireEvent()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);
        var fired = false;
        vm.RequestLogManualDuration += (_, _, _, _) => fired = true;

        vm.ManualHours = 0;
        vm.ManualMinutes = 0;
        vm.ManualSeconds = 0;
        vm.LogManualDuration();

        Assert.False(fired);
        Assert.Contains("greater than zero", vm.ManualDurationStatus);
    }

    [Fact]
    public void LogManualDuration_AllZero_DoesNotResetStatus()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);

        vm.LogManualDuration();

        // Status should show validation error, not success
        Assert.DoesNotContain("successfully", vm.ManualDurationStatus);
    }

    [Fact]
    public void LogManualDuration_NoEventHandler_StillResetsAndShowsStatus()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test");
        using var vm = new HabitFormViewModel(Array.Empty<SelectableTag>(), habit);
        // No event handler subscribed

        vm.ManualHours = 0;
        vm.ManualMinutes = 10;
        vm.ManualSeconds = 0;
        vm.LogManualDuration();

        // Should still show success status and reset inputs
        Assert.Contains("00:10:00", vm.ManualDurationStatus);
        Assert.Equal(0, vm.ManualMinutes);
    }

    [Fact]
    public void LogManualDuration_RewardForm_PassesRewardId()
    {
        var reward = new TaskApp.Models.Rewards.Reward("Test Reward");
        using var vm = new RewardFormViewModel(Array.Empty<SelectableTag>(), reward);
        Guid? capturedTaskId = null;
        Guid? capturedRewardId = null;
        vm.RequestLogManualDuration += (_, _, taskId, rewardId) =>
        {
            capturedTaskId = taskId;
            capturedRewardId = rewardId;
        };

        vm.ManualMinutes = 5;
        vm.LogManualDuration();

        Assert.Null(capturedTaskId);
        Assert.Equal(reward.Id, capturedRewardId);
    }

    #endregion

    #region MainWindowViewModel.TryAutocompleteFromLoggedDurationAsync

    [Fact]
    public async Task TryAutocomplete_CompletesDaily_WhenThresholdMet()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var initialGold = vm.User.Gold;

        // Log enough duration to meet the threshold
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(35), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);

        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.True(vm.User.Gold > initialGold);
    }

    [Fact]
    public async Task TryAutocomplete_DoesNotComplete_WhenBelowThreshold()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(60));

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);

        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public async Task TryAutocomplete_DoesNotComplete_WhenAlreadyComplete()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        daily.Complete(); // Already complete
        var goldAfterComplete = vm.User.Gold;

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(60), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);

        // Gold should not increase again
        Assert.Equal(goldAfterComplete, vm.User.Gold);
    }

    [Fact]
    public async Task TryAutocomplete_DoesNotComplete_WhenNoThresholdSet()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        // No autocomplete threshold set

        await vm.LogActivityDurationAsync(TimeSpan.FromHours(10), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);

        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public async Task TryAutocomplete_NoOp_WhenTaskIdNotADaily()
    {
        var vm = CreateMainViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        // Should not throw even though it's a habit, not a daily
        await vm.TryAutocompleteFromLoggedDurationAsync(habit.Id);
    }

    [Fact]
    public async Task TryAutocomplete_NoOp_WhenTaskIdNotFound()
    {
        var vm = CreateMainViewModel();

        // Should not throw for a non-existent ID
        await vm.TryAutocompleteFromLoggedDurationAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task TryAutocomplete_CumulativeLogs_TriggerCompletion()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(60));

        // Two separate logs that together exceed the threshold
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(35), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);
        Assert.False(daily.IsCompleteForCurrentPeriod);

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);
        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public async Task TryAutocomplete_ExactThreshold_TriggersCompletion()
    {
        var vm = CreateMainViewModel();
        vm.NewDailyTitle = "Study";
        vm.AddDaily();
        var daily = vm.Dailies[0];
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "Study", daily.Id);
        await vm.TryAutocompleteFromLoggedDurationAsync(daily.Id);

        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    #endregion

    private MainWindowViewModel CreateMainViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }
}
