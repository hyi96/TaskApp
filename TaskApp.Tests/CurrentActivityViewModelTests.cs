using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class CurrentActivityViewModelTests
{
    [AvaloniaFact]
    public void Remove_ClearsState()
    {
        var vm = new CurrentActivityViewModel();
        var taskId = Guid.NewGuid();
        var rewardId = Guid.NewGuid();

        vm.SetTitleAndReset("Focus", taskId, rewardId);
        vm.Remove();

        Assert.Equal(string.Empty, vm.Title);
        Assert.Null(vm.TaskId);
        Assert.Null(vm.RewardId);
        Assert.False(vm.IsRunning);
        Assert.Equal(TimeSpan.Zero, vm.Elapsed);
    }

    [AvaloniaFact]
    public async Task Pause_RecordsSessionDuration()
    {
        var vm = new CurrentActivityViewModel();
        var recorded = false;
        vm.ActivityDurationRecorded += (_, _, _, _) => recorded = true;

        vm.SetTitleAndReset("Focus");
        vm.Start();
        await Task.Delay(200);
        vm.Pause();

        Assert.True(recorded);
        Assert.False(vm.IsRunning);
    }

    [AvaloniaFact]
    public async Task SetTitleAndReset_WhileRunning_LogsPreviousSession()
    {
        var vm = new CurrentActivityViewModel();
        var recorded = false;
        vm.ActivityDurationRecorded += (_, title, _, _) =>
        {
            if (title == "Focus")
            {
                recorded = true;
            }
        };

        vm.SetTitleAndReset("Focus", Guid.NewGuid(), Guid.NewGuid());
        vm.Start();
        await Task.Delay(200);

        vm.SetTitleAndReset("Next", Guid.NewGuid(), null);

        Assert.True(recorded);
        Assert.Equal("Next", vm.Title);
        Assert.False(vm.IsRunning);
        Assert.Equal(TimeSpan.Zero, vm.Elapsed);
    }

    [AvaloniaFact]
    public async Task Pause_WithEmptyTitle_DoesNotRecordSession()
    {
        var vm = new CurrentActivityViewModel();
        var recorded = false;
        vm.ActivityDurationRecorded += (_, _, _, _) => recorded = true;

        vm.SetTitleAndReset(string.Empty);
        vm.Start();
        await Task.Delay(200);
        vm.Pause();

        Assert.False(recorded);
        Assert.False(vm.IsRunning);
    }

    [AvaloniaFact]
    public async Task Autocomplete_WhenThresholdReached_TriggersOnlyOnce()
    {
        var vm = new CurrentActivityViewModel();
        var taskId = Guid.NewGuid();
        var triggerCount = 0;

        vm.GetAutocompleteRemainingTime = (_, _, _) => Task.FromResult<TimeSpan?>(TimeSpan.Zero);
        vm.AutocompleteTriggered += _ => triggerCount++;

        vm.SetTitleAndReset("Focus", taskId, null);
        vm.Start();

        await InvokeCheckAutocompleteAsync(vm);
        await InvokeCheckAutocompleteAsync(vm);

        Assert.Equal(1, triggerCount);
    }

    [AvaloniaFact]
    public async Task Autocomplete_WithoutTaskId_DoesNotQueryRemainingTime()
    {
        var vm = new CurrentActivityViewModel();
        var queried = false;

        vm.GetAutocompleteRemainingTime = (_, _, _) =>
        {
            queried = true;
            return Task.FromResult<TimeSpan?>(TimeSpan.Zero);
        };

        vm.SetTitleAndReset("Focus");
        vm.Start();

        await InvokeCheckAutocompleteAsync(vm);

        Assert.False(queried);
    }

    [AvaloniaFact]
    public async Task Autocomplete_CheckPassesSessionStartAndElapsedToRemainingTimeProvider()
    {
        var vm = new CurrentActivityViewModel();
        var taskId = Guid.NewGuid();
        DateTimeOffset? capturedSessionStartedAt = null;
        var capturedSessionElapsed = TimeSpan.Zero;
        var triggered = false;

        vm.GetAutocompleteRemainingTime = (_, sessionStartedAt, currentSessionElapsed) =>
        {
            capturedSessionStartedAt = sessionStartedAt;
            capturedSessionElapsed = currentSessionElapsed;
            return Task.FromResult<TimeSpan?>(TimeSpan.FromMinutes(30) - currentSessionElapsed);
        };
        vm.AutocompleteTriggered += _ => triggered = true;

        vm.SetTitleAndReset("Focus", taskId, null);
        vm.Start();
        SetSessionStartElapsed(vm, TimeSpan.FromHours(-2));

        await InvokeCheckAutocompleteAsync(vm);

        Assert.NotNull(capturedSessionStartedAt);
        Assert.True(capturedSessionElapsed >= TimeSpan.FromHours(2));
        Assert.True(triggered);
    }

    private static async Task InvokeCheckAutocompleteAsync(CurrentActivityViewModel vm)
    {
        var method = typeof(CurrentActivityViewModel).GetMethod("CheckAutocompleteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(vm, null) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private static void SetSessionStartElapsed(CurrentActivityViewModel vm, TimeSpan value)
    {
        var field = typeof(CurrentActivityViewModel).GetField("_sessionStartElapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, value);
    }
}
