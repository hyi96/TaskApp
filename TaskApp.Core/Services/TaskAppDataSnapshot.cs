using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Tags;

namespace TaskApp.Services;

public sealed record TaskAppDataSnapshot(
    string SchemaVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<TaskData> Tasks,
    IReadOnlyList<RewardData> Rewards,
    IReadOnlyList<TagData> Tags,
    UserProfile UserProfile,
    IReadOnlyList<LogEntry> LogEntries)
{
    public const string CurrentSchemaVersion = "1";
}

public static class TaskAppDataStoreSnapshotExtensions
{
    public static async Task<TaskAppDataSnapshot> LoadSnapshotAsync(this ITaskAppDataStore dataStore)
    {
        var tasks = (await dataStore.LoadTasksAsync())
            .Select(TaskMapper.ToData)
            .ToList();
        var rewards = (await dataStore.LoadRewardsAsync())
            .Select(RewardMapper.ToData)
            .ToList();
        var tags = (await dataStore.LoadTagsAsync())
            .Select(tag => new TagData { Id = tag.Id, Name = tag.Name })
            .ToList();
        var userProfile = await dataStore.LoadUserProfileAsync();
        var logEntries = await dataStore.LoadAllLogEntriesAsync();

        return new TaskAppDataSnapshot(
            TaskAppDataSnapshot.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tasks,
            rewards,
            tags,
            userProfile,
            logEntries);
    }

    public static async Task SaveSnapshotAsync(this ITaskAppDataStore dataStore, TaskAppDataSnapshot snapshot)
    {
        await dataStore.SaveTasksAsync(snapshot.Tasks.Select(TaskMapper.ToModel));
        await dataStore.SaveRewardsAsync(snapshot.Rewards.Select(RewardMapper.ToModel));
        await dataStore.SaveTagsAsync(snapshot.Tags.Select(tag => new Tag(tag.Name, tag.Id)));
        await dataStore.SaveUserProfileAsync(snapshot.UserProfile);
        await dataStore.ReplaceLogEntriesAsync(snapshot.LogEntries);
    }
}
