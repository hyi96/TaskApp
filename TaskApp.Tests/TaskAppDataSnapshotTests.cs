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
}
