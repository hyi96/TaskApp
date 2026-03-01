using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.Tests;

/// <summary>
/// Tests that verify data survives an export → import round-trip intact.
///
/// ISOLATION: A dedicated throwaway "source" user is created for each test
/// run. All writes go there — the real user's data is never touched.
/// Both the source and every imported user are deleted in DisposeAsync.
/// </summary>
public class ImportExportTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly UserService _userService;
    private readonly List<Guid> _testUserIds = new();
    private Guid _originalUserId;

    public ImportExportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _userService = new UserService();
        _userService.LoadSync();
        _originalUserId = _userService.CurrentUser!.Id;
    }

    public async Task InitializeAsync()
    {
        // Create a throwaway user so tests never write to the real profile
        var sourceUser = await _userService.CreateUserAsync($"_Test_{Guid.NewGuid():N}");
        _testUserIds.Add(sourceUser.Id);
        await _userService.SwitchUserAsync(sourceUser.Id);
    }

    public async Task DisposeAsync()
    {
        // Switch back to the original user first
        await _userService.SwitchUserAsync(_originalUserId);

        // Delete every user that was created during the test
        foreach (var userId in _testUserIds)
        {
            try { await _userService.DeleteUserAsync(userId); } catch { }
        }

        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region Tasks round-trip

    [Fact]
    public async Task ExportImport_Tasks_PreservesAllTaskTypes()
    {
        // Arrange – create one of each task type with rich data
        var storage = CreateStorage();

        var tag = new Tag("TestTag");

        var habit = new HabitTask();
        habit.UpdateTitle("Read books");
        habit.UpdateNotes("At least 30 min");
        habit.SetGoldReward(1.5);
        habit.SetIncrementAmount(2.0);
        habit.SetDecrementEnabled(true);
        habit.SetResetCadence(HabitResetCadence.Weekly);
        habit.UpdateTags(new[] { tag });
        habit.SetHidden(true);
        habit.Increment();
        habit.Increment();

        var daily = new DailyTask();
        daily.UpdateTitle("Exercise");
        daily.UpdateNotes("Gym or walk");
        daily.SetGoldReward(2.0);
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetRepeatEvery(2);
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(45));
        daily.UpdateTags(new[] { tag });
        daily.Complete();

        var todo = new TodoTask();
        todo.UpdateTitle("Buy groceries");
        todo.UpdateNotes("Milk, eggs");
        todo.SetGoldReward(0.5);
        todo.SetDueDate(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero));
        todo.UpdateTags(new[] { tag });
        todo.Checklist.Add(new ChecklistItem("Milk") { IsCompleted = true });
        todo.Checklist.Add(new ChecklistItem("Eggs"));

        var tasks = new List<TaskBase> { habit, daily, todo };
        await storage.SaveTasksAsync(tasks);

        // Act – export & import
        var (importedStorage, _) = await ExportAndImportAsync();

        // Assert
        var loaded = await importedStorage.LoadTasksAsync();
        Assert.Equal(3, loaded.Count);

        // Habit
        var h = loaded.OfType<HabitTask>().Single();
        Assert.Equal("Read books", h.Title);
        Assert.Equal("At least 30 min", h.Notes);
        Assert.Equal(1.5, h.GoldReward, 3);
        Assert.Equal(2.0, h.IncrementAmount, 3);
        Assert.True(h.DecrementEnabled);
        Assert.Equal(HabitResetCadence.Weekly, h.ResetCadence);
        Assert.True(h.IsHidden);
        Assert.Equal(4.0, h.Count, 3); // 2 increments × 2.0
        Assert.Single(h.Tags);
        Assert.Equal(tag.Id, h.Tags[0].Id);

        // Daily
        var d = loaded.OfType<DailyTask>().Single();
        Assert.Equal("Exercise", d.Title);
        Assert.Equal("Gym or walk", d.Notes);
        Assert.Equal(2.0, d.GoldReward, 3);
        Assert.Equal(RepeatCadence.Weekly, d.Cadence);
        Assert.Equal(2, d.RepeatEvery);
        Assert.Equal(TimeSpan.FromMinutes(45), d.AutocompleteTimeThreshold);
        Assert.Equal(1, d.CurrentStreak);
        Assert.Equal(1, d.BestStreak);
        Assert.NotNull(d.LastCompletedDate);

        // Todo
        var t = loaded.OfType<TodoTask>().Single();
        Assert.Equal("Buy groceries", t.Title);
        Assert.Equal("Milk, eggs", t.Notes);
        Assert.Equal(0.5, t.GoldReward, 3);
        Assert.NotNull(t.DueDate);
        Assert.Equal(2, t.Checklist.Count);
        Assert.True(t.Checklist[0].IsCompleted);
        Assert.False(t.Checklist[1].IsCompleted);
        Assert.Equal("Milk", t.Checklist[0].Text);
    }

    #endregion

    #region Rewards round-trip

    [Fact]
    public async Task ExportImport_Rewards_PreservesAllFields()
    {
        var storage = CreateStorage();

        var tag = new Tag("Treat");
        var reward = new Reward("Movie night", "Pick a movie", isRepeatable: true, goldCost: 5.0);
        reward.UpdateTags(new[] { tag });
        reward.TryClaim(100);
        reward.TryClaim(100);

        await storage.SaveRewardsAsync(new[] { reward });

        var (importedStorage, _) = await ExportAndImportAsync();

        var loaded = await importedStorage.LoadRewardsAsync();
        Assert.Single(loaded);
        var r = loaded[0];
        Assert.Equal("Movie night", r.Title);
        Assert.Equal("Pick a movie", r.Notes);
        Assert.True(r.IsRepeatable);
        Assert.Equal(5.0, r.GoldCost, 3);
        Assert.Equal(2, r.ClaimCount);
        Assert.Single(r.Tags);
        Assert.Equal(tag.Id, r.Tags[0].Id);
    }

    #endregion

    #region Tags round-trip

    [Fact]
    public async Task ExportImport_Tags_PreservesIdAndName()
    {
        var storage = CreateStorage();

        var tags = new List<Tag>
        {
            new("Health"),
            new("Work"),
            new("Personal")
        };
        await storage.SaveTagsAsync(tags);

        var (importedStorage, _) = await ExportAndImportAsync();

        var loaded = await importedStorage.LoadTagsAsync();
        Assert.Equal(3, loaded.Count);
        foreach (var original in tags)
        {
            var match = loaded.SingleOrDefault(t => t.Id == original.Id);
            Assert.NotNull(match);
            Assert.Equal(original.Name, match!.Name);
        }
    }

    #endregion

    #region UserProfile round-trip

    [Fact]
    public async Task ExportImport_UserProfile_PreservesGoldAndSortPreferences()
    {
        var storage = CreateStorage();

        var profile = new UserProfile
        {
            Gold = 42.5,
            HabitsSortMode = "Name (Z-A)",
            DailiesSortMode = "Created time (new to old)",
            TodosSortMode = "Due date (earliest to latest)",
            RewardsSortMode = "Gold value (high to low)"
        };
        await storage.SaveUserProfileAsync(profile);

        var (importedStorage, _) = await ExportAndImportAsync();

        var loaded = await importedStorage.LoadUserProfileAsync();
        Assert.Equal(42.5, loaded.Gold, 3);
        Assert.Equal("Name (Z-A)", loaded.HabitsSortMode);
        Assert.Equal("Created time (new to old)", loaded.DailiesSortMode);
        Assert.Equal("Due date (earliest to latest)", loaded.TodosSortMode);
        Assert.Equal("Gold value (high to low)", loaded.RewardsSortMode);
    }

    #endregion

    #region Logs round-trip

    [Fact]
    public async Task ExportImport_Logs_PreservesLogEntries()
    {
        var storage = CreateStorage();

        var taskId = Guid.NewGuid();
        var entry1 = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-10),
            Type = LogType.DailyCompleted,
            TaskId = taskId,
            GoldDelta = 2.0,
            UserGold = 10.0,
            TitleSnapshot = "Exercise"
        };
        var entry2 = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            Type = LogType.ActivityDuration,
            TaskId = taskId,
            GoldDelta = 0,
            UserGold = 10.0,
            Duration = TimeSpan.FromMinutes(25),
            TitleSnapshot = "Exercise"
        };

        await storage.AddLogEntryAsync(entry1);
        await storage.AddLogEntryAsync(entry2);

        var (importedStorage, _) = await ExportAndImportAsync();

        var loaded = await importedStorage.LoadAllLogEntriesAsync();
        Assert.Equal(2, loaded.Count);

        var l1 = loaded.Single(e => e.Id == entry1.Id);
        Assert.Equal(LogType.DailyCompleted, l1.Type);
        Assert.Equal(taskId, l1.TaskId);
        Assert.Equal(2.0, l1.GoldDelta, 3);
        Assert.Equal(10.0, l1.UserGold, 3);
        Assert.Equal("Exercise", l1.TitleSnapshot);

        var l2 = loaded.Single(e => e.Id == entry2.Id);
        Assert.Equal(LogType.ActivityDuration, l2.Type);
        Assert.NotNull(l2.Duration);
        Assert.Equal(TimeSpan.FromMinutes(25), l2.Duration!.Value);
    }

    #endregion

    #region Import metadata

    [Fact]
    public async Task ImportedUser_GetsNewIdAndPreservesName()
    {
        var storage = CreateStorage();
        await storage.SaveUserProfileAsync(new UserProfile { Gold = 1 });

        var sourceUserId = _userService.CurrentUser!.Id;
        var sourceUserName = _userService.CurrentUser!.Name;

        var (_, importedUser) = await ExportAndImportAsync();

        Assert.NotEqual(sourceUserId, importedUser.Id);
        Assert.Contains(sourceUserName, importedUser.Name);
    }

    [Fact]
    public async Task ExportImport_RoundTrip_ProducesLoadableData()
    {
        var (importedStorage, _) = await ExportAndImportAsync();

        var tasks = await importedStorage.LoadTasksAsync();
        var rewards = await importedStorage.LoadRewardsAsync();
        var profile = await importedStorage.LoadUserProfileAsync();

        Assert.NotNull(tasks);
        Assert.NotNull(rewards);
        Assert.NotNull(profile);
    }

    #endregion

    #region Helpers

    private StorageService CreateStorage()
    {
        return new StorageService(_userService);
    }

    /// <summary>
    /// Exports the current (throwaway) user to a temp zip, imports as a new user,
    /// switches to that user, and returns a fresh StorageService plus the imported User.
    /// Both source and imported users are tracked for cleanup in DisposeAsync.
    /// </summary>
    private async Task<(StorageService storage, User importedUser)> ExportAndImportAsync()
    {
        var exportPath = Path.Combine(_tempDir, $"export_{Guid.NewGuid():N}.zip");

        await _userService.ExportUserAsync(_userService.CurrentUser!.Id, exportPath);
        var importedUser = await _userService.ImportUserAsync(exportPath);
        _testUserIds.Add(importedUser.Id);
        await _userService.SwitchUserAsync(importedUser.Id);

        var importedStorage = new StorageService(_userService);
        return (importedStorage, importedUser);
    }

    #endregion
}
