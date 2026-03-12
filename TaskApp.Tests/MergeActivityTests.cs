using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class MergeActivityTests : IDisposable
{
    private readonly string _tempDir;

    public MergeActivityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region EvaluateMergeEligibility (via SearchQuery → CanMerge)

    [Fact]
    public async Task CanMerge_True_WhenActivityAndOneHabitShareName()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        // Log an activity with the same name but no TaskId
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";

        Assert.True(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_True_WhenActivityAndOneDailyShareName()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewDailyTitle = "read";
        vm.AddDaily();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "read");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "read";

        Assert.True(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_True_WhenActivityAndOneRewardShareName()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewRewardTitle = "snack";
        vm.AddReward();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "snack");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "snack";

        Assert.True(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenOnlyActivityResults()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "orphan");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "orphan";

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenOnlyTaskResults()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "unique";
        vm.AddHabit();

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "unique";

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenNoActivityType()
    {
        // Two non-Activity types with same name → no merge
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "overlap";
        vm.AddHabit();
        vm.NewDailyTitle = "overlap";
        vm.AddDaily();

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "overlap";

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenTwoNonActivityInstancesWithSameName()
    {
        // Two habits with same name + activity → ambiguous, no merge
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "duped";
        vm.AddHabit();
        vm.NewHabitTitle = "duped";
        vm.AddHabit();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "duped");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "duped";

        // 3 results: habit, habit, activity. 2 non-Activity → ambiguous
        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenResultsHaveDifferentNames()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "reading";
        vm.AddHabit();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "read");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        // "read" matches both "reading" (Habit) and "read" (Activity) but names differ
        graphVm.SearchQuery = "read";

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenThreeDistinctTypes()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "multi";
        vm.AddHabit();
        vm.NewDailyTitle = "multi";
        vm.AddDaily();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "multi");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "multi";

        // 3 types: Habit, Daily, Activity → not exactly 2
        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_False_WhenSearchQueryEmpty()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "test";
        vm.AddHabit();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(5), "test");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "";

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_CaseInsensitive()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";

        Assert.True(graphVm.CanMerge);
    }

    #endregion

    #region MergeAsync

    [Fact]
    public async Task Merge_AssignsTaskId_ToOrphanedActivityEntries()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(15), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";
        Assert.True(graphVm.CanMerge);

        await graphVm.MergeAsync();

        // Verify entries now have the habit's TaskId
        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var activityEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration &&
            e.TitleSnapshot.Equals("exercise", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Equal(2, activityEntries.Count);
        Assert.All(activityEntries, e => Assert.Equal(habit.Id, e.TaskId));
    }

    [Fact]
    public async Task Merge_AssignsRewardId_ToOrphanedActivityEntries()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewRewardTitle = "snack";
        vm.AddReward();
        var reward = vm.Rewards[0];

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "snack");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "snack";
        Assert.True(graphVm.CanMerge);

        await graphVm.MergeAsync();

        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var activityEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration &&
            e.TitleSnapshot.Equals("snack", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Single(activityEntries);
        Assert.Equal(reward.Id, activityEntries[0].RewardId);
        Assert.Null(activityEntries[0].TaskId);
    }

    [Fact]
    public async Task Merge_DoesNotAffect_AlreadyAssignedEntries()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        // Log one with TaskId (as if started from the task) and one orphaned
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise", habit.Id);
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(15), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";

        await graphVm.MergeAsync();

        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var activityEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration &&
            e.TitleSnapshot.Equals("exercise", StringComparison.OrdinalIgnoreCase)).ToList();

        // Both should now have the habit's TaskId
        Assert.Equal(2, activityEntries.Count);
        Assert.All(activityEntries, e => Assert.Equal(habit.Id, e.TaskId));
    }

    [Fact]
    public async Task Merge_DoesNotAffect_DifferentTitleEntries()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "other");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";

        await graphVm.MergeAsync();

        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var otherEntry = entries.First(e => e.TitleSnapshot == "other");
        Assert.Null(otherEntry.TaskId);
        Assert.Null(otherEntry.RewardId);
    }

    [Fact]
    public async Task Merge_ClearsCanMerge()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";
        Assert.True(graphVm.CanMerge);

        await graphVm.MergeAsync();

        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task Merge_SearchResults_RemoveActivityEntry()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";

        // Before merge: should have 2 results (Habit + Activity)
        Assert.Equal(2, graphVm.SearchResults.Count);
        Assert.Contains(graphVm.SearchResults, r => r.TargetType == TargetType.Activity);
        Assert.Contains(graphVm.SearchResults, r => r.TargetType == TargetType.Habit);

        await graphVm.MergeAsync();

        // After merge: search cleared, dropdown gone
        Assert.Empty(graphVm.SearchResults);
        Assert.Equal(string.Empty, graphVm.SearchQuery);
        Assert.False(graphVm.CanMerge);
    }

    [Fact]
    public async Task Merge_EntriesVisibleUnderTask_InDatabase()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habitId = vm.Habits[0].Id;

        // Create 3 orphaned activity entries
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";
        await graphVm.MergeAsync();

        // All 3 entries should now point to the habit
        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var habitEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration && e.TaskId == habitId).ToList();
        Assert.Equal(3, habitEntries.Count);

        // No orphaned activity entries should remain
        var orphanedEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration &&
            e.TaskId == null && e.RewardId == null &&
            e.TitleSnapshot.Equals("exercise", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(orphanedEntries);
    }

    [Fact]
    public async Task Merge_NoOp_WhenCanMergeIsFalse()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "orphan");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "orphan";
        Assert.False(graphVm.CanMerge);

        // Should not throw
        await graphVm.MergeAsync();
    }

    #endregion

    #region Full scenario diagnostics

    [Fact]
    public async Task Merge_FullScenario_MixedEntries_ActivityDisappearsFromSearch()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        // Entries logged FROM the task (has TaskId) — like "Set as Current Activity"
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "exercise", habit.Id);
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "exercise", habit.Id);

        // Orphaned entries (no TaskId) — like manually typing in activity bar
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(15), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();

        // Verify: before merge, search shows both Habit and Activity
        graphVm.SearchQuery = "exercise";
        Assert.Contains(graphVm.SearchResults, r => r.TargetType == TargetType.Activity);
        Assert.Contains(graphVm.SearchResults, r => r.TargetType == TargetType.Habit);
        Assert.True(graphVm.CanMerge);

        await graphVm.MergeAsync();

        // After merge: search cleared entirely
        Assert.Empty(graphVm.SearchResults);
        Assert.Equal(string.Empty, graphVm.SearchQuery);
        Assert.False(graphVm.CanMerge);

        // Verify DB: all entries should now have the habit's TaskId
        var entries = await vm.StorageService.LoadAllLogEntriesAsync();
        var exerciseEntries = entries.Where(e =>
            e.Type == LogType.ActivityDuration &&
            e.TitleSnapshot.Equals("exercise", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(4, exerciseEntries.Count);
        Assert.All(exerciseEntries, e => Assert.Equal(habit.Id, e.TaskId));

        // Zero orphaned entries
        var orphaned = exerciseEntries.Where(e => e.TaskId == null).ToList();
        Assert.Empty(orphaned);
    }

    [Fact]
    public async Task Merge_Survives_Reload_ActivityGoneAfterRestart()
    {
        var (vm, graphVm) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "exercise";
        vm.AddHabit();
        var habit = vm.Habits[0];

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "exercise");

        await vm.SaveDataAsync();
        await graphVm.LoadAsync();
        graphVm.SearchQuery = "exercise";
        Assert.True(graphVm.CanMerge);

        await graphVm.MergeAsync();

        // Simulate app restart: create a fresh GraphViewModel
        var graphVm2 = new GraphViewModel(vm.StorageService);
        await graphVm2.LoadAsync();
        graphVm2.SearchQuery = "exercise";

        // After restart and re-search, only the task should appear (no Activity)
        Assert.DoesNotContain(graphVm2.SearchResults, r => r.TargetType == TargetType.Activity);
        Assert.False(graphVm2.CanMerge);
    }

    #endregion

    #region StorageService.MergeActivityLogEntriesAsync

    [Fact]
    public async Task StorageMerge_ReturnsAffectedCount()
    {
        var (vm, _) = await CreateLoadedViewModels();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "test");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "test");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "other");

        var targetId = Guid.NewGuid();
        var count = await vm.StorageService.MergeActivityLogEntriesAsync("test", targetId, null);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task StorageMerge_ReturnsZero_WhenNoMatch()
    {
        var (vm, _) = await CreateLoadedViewModels();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "something");

        var count = await vm.StorageService.MergeActivityLogEntriesAsync("nonexistent", Guid.NewGuid(), null);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task StorageMerge_SkipsAlreadyAssigned()
    {
        var (vm, _) = await CreateLoadedViewModels();
        vm.NewHabitTitle = "test";
        vm.AddHabit();
        var habitId = vm.Habits[0].Id;

        // One with TaskId already set, one orphaned
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "test", habitId);
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "test");

        var newId = Guid.NewGuid();
        var count = await vm.StorageService.MergeActivityLogEntriesAsync("test", newId, null);

        // Only the orphaned one should be affected
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StorageMerge_IsCaseInsensitive()
    {
        var (vm, _) = await CreateLoadedViewModels();

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(10), "Exercise");
        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(20), "EXERCISE");

        var targetId = Guid.NewGuid();
        var count = await vm.StorageService.MergeActivityLogEntriesAsync("exercise", targetId, null);

        Assert.Equal(2, count);
    }

    #endregion

    private async Task<(MainWindowViewModel vm, GraphViewModel graphVm)> CreateLoadedViewModels()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        var vm = new MainWindowViewModel(storageService, userService);
        var graphVm = new GraphViewModel(storageService);
        return (vm, graphVm);
    }
}
