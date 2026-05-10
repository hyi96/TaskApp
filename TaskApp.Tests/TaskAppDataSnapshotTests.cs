using System;
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

public class TaskAppDataSnapshotTests : IDisposable
{
    private readonly string _tempDir;

    public TaskAppDataSnapshotTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task LoadSnapshotAsync_ReturnsCanonicalStorePayload()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storage = new StorageService(userService);

        var tag = new Tag("Cloud");
        var todo = new TodoTask();
        todo.UpdateTitle("Sync desktop task");
        todo.UpdateTags(new[] { tag });
        todo.SetGoldReward(2.5);

        var reward = new Reward("Coffee", goldCost: 5);
        var profile = new UserProfile { Gold = 12.5 };
        var log = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.TodoCompleted,
            TaskId = todo.Id,
            GoldDelta = 2.5,
            UserGold = profile.Gold,
            TitleSnapshot = todo.Title
        };

        await storage.SaveTagsAsync(new[] { tag });
        await storage.SaveTasksAsync(new TaskBase[] { todo });
        await storage.SaveRewardsAsync(new[] { reward });
        await storage.SaveUserProfileAsync(profile);
        await storage.AddLogEntryAsync(log);

        var snapshot = await storage.LoadSnapshotAsync();

        Assert.Equal(TaskAppDataSnapshot.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.Single(snapshot.Tasks);
        Assert.Single(snapshot.Rewards);
        Assert.Single(snapshot.Tags);
        Assert.Single(snapshot.LogEntries);
        Assert.Equal("Sync desktop task", snapshot.Tasks.Single().Title);
        Assert.Equal("Coffee", snapshot.Rewards.Single().Title);
        Assert.Equal("Cloud", snapshot.Tags.Single().Name);
        Assert.Equal(12.5, snapshot.UserProfile.Gold);
        Assert.Equal(log.Id, snapshot.LogEntries.Single().Id);
    }

    [Fact]
    public async Task SaveSnapshotAsync_ReplacesLocalStorePayload()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storage = new StorageService(userService);

        var oldTask = new TodoTask();
        oldTask.UpdateTitle("Old");
        await storage.SaveTasksAsync(new TaskBase[] { oldTask });
        await storage.AddLogEntryAsync(new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.TodoCompleted,
            TaskId = oldTask.Id,
            TitleSnapshot = oldTask.Title
        });

        var newTask = new TodoTask();
        newTask.UpdateTitle("New");
        var newReward = new Reward("New reward", goldCost: 4);
        var newTag = new Tag("Imported");
        var newProfile = new UserProfile { Gold = 99 };
        var newLog = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.RewardClaimed,
            RewardId = newReward.Id,
            GoldDelta = -newReward.GoldCost,
            UserGold = newProfile.Gold,
            TitleSnapshot = newReward.Title
        };

        var snapshot = new TaskAppDataSnapshot(
            TaskAppDataSnapshot.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { TaskMapper.ToData(newTask) },
            new[] { RewardMapper.ToData(newReward) },
            new[] { new TaskApp.Data.TagData { Id = newTag.Id, Name = newTag.Name } },
            newProfile,
            new[] { newLog });

        await storage.SaveSnapshotAsync(snapshot);

        var loadedTasks = await storage.LoadTasksAsync();
        var loadedRewards = await storage.LoadRewardsAsync();
        var loadedTags = await storage.LoadTagsAsync();
        var loadedProfile = await storage.LoadUserProfileAsync();
        var loadedLogs = await storage.LoadAllLogEntriesAsync();

        Assert.Single(loadedTasks);
        Assert.Equal("New", loadedTasks.Single().Title);
        Assert.Single(loadedRewards);
        Assert.Equal("New reward", loadedRewards.Single().Title);
        Assert.Single(loadedTags);
        Assert.Equal("Imported", loadedTags.Single().Name);
        Assert.Equal(99, loadedProfile.Gold);
        Assert.Single(loadedLogs);
        Assert.Equal(newLog.Id, loadedLogs.Single().Id);
    }
}
