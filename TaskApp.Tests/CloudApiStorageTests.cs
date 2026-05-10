using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Api.Services;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.Tests;

public class CloudApiStorageTests : IDisposable
{
    private readonly string _tempDir;

    public CloudApiStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppApiTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ProfileSnapshot_RoundTripsThroughSqlite()
    {
        var database = TaskAppCloudDatabase.FromFile(Path.Combine(_tempDir, "taskapp-cloud.db"));
        await database.InitializeAsync();

        var account = await database.CreateAccountAsync("Desktop stack test");
        var profileId = Guid.NewGuid();
        var snapshot = CreateSnapshot();

        Assert.False(string.IsNullOrWhiteSpace(account.LoginSecret));

        var saved = await database.UpsertProfileSnapshotAsync(
            account.Id,
            profileId,
            new UpsertProfileSnapshotRequest("Default", snapshot));

        var loaded = await database.GetProfileSnapshotAsync(account.Id, profileId);
        var profiles = await database.ListProfilesAsync(account.Id);

        Assert.NotNull(saved);
        Assert.NotNull(loaded);
        Assert.Single(profiles);
        Assert.Equal(account.Id, loaded.AccountId);
        Assert.Equal(profileId, loaded.ProfileId);
        Assert.Equal("Default", loaded.ProfileName);
        Assert.Equal(3, loaded.Snapshot.Tasks.Count);
        Assert.Contains(loaded.Snapshot.Tasks, task => task is TodoTaskData);
        Assert.Contains(loaded.Snapshot.Tasks, task => task is DailyTaskData);
        Assert.Contains(loaded.Snapshot.Tasks, task => task is HabitTaskData);
        Assert.Equal("Cloud Reward", loaded.Snapshot.Rewards.Single().Title);
        Assert.Equal("Cloud", loaded.Snapshot.Tags.Single().Name);
        Assert.Equal(42, loaded.Snapshot.UserProfile.Gold);
        Assert.Equal(LogType.TodoCompleted, loaded.Snapshot.LogEntries.Single().Type);
    }

    [Fact]
    public async Task AccountLogin_ValidatesGeneratedSecret()
    {
        var database = TaskAppCloudDatabase.FromFile(Path.Combine(_tempDir, "taskapp-cloud.db"));
        await database.InitializeAsync();

        var account = await database.CreateAccountAsync("Desktop login test");

        var loggedIn = await database.LoginAccountAsync(account.Id, account.LoginSecret!);
        var invalid = await database.LoginAccountAsync(account.Id, "wrong-secret");

        Assert.NotNull(loggedIn);
        Assert.Equal(account.Id, loggedIn.Id);
        Assert.Equal("Desktop login test", loggedIn.DisplayName);
        Assert.Null(loggedIn.LoginSecret);
        Assert.Null(invalid);
    }

    [Fact]
    public async Task UpsertProfileSnapshot_ReturnsNull_WhenAccountDoesNotExist()
    {
        var database = TaskAppCloudDatabase.FromFile(Path.Combine(_tempDir, "taskapp-cloud.db"));
        await database.InitializeAsync();

        var saved = await database.UpsertProfileSnapshotAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpsertProfileSnapshotRequest("Missing", CreateSnapshot()));

        Assert.Null(saved);
    }

    private static TaskAppDataSnapshot CreateSnapshot()
    {
        var tag = new Tag("Cloud");

        var todo = new TodoTask();
        todo.UpdateTitle("Cloud Todo");
        todo.SetDueDate(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        todo.UpdateTags(new[] { tag });

        var daily = new DailyTask();
        daily.UpdateTitle("Cloud Daily");
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetRepeatEvery(2);
        daily.SetGoldReward(3);

        var habit = new HabitTask();
        habit.UpdateTitle("Cloud Habit");
        habit.SetIncrementAmount(2);
        habit.Increment();

        var reward = new Reward("Cloud Reward", goldCost: 10);
        var profile = new UserProfile { Gold = 42 };
        var log = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = LogType.TodoCompleted,
            TaskId = todo.Id,
            GoldDelta = todo.GoldReward,
            UserGold = profile.Gold,
            TitleSnapshot = todo.Title
        };

        return new TaskAppDataSnapshot(
            TaskAppDataSnapshot.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { TaskMapper.ToData(todo), TaskMapper.ToData(daily), TaskMapper.ToData(habit) },
            new[] { RewardMapper.ToData(reward) },
            new[] { new TagData { Id = tag.Id, Name = tag.Name } },
            profile,
            new[] { log });
    }
}
