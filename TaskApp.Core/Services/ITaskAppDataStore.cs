using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public interface ITaskAppDataStore
{
    Task SaveTagsAsync(IEnumerable<Tag> tags);
    Task<List<Tag>> LoadTagsAsync();

    Task SaveTasksAsync(IEnumerable<TaskBase> tasks);
    Task<List<TaskBase>> LoadTasksAsync();

    Task SaveRewardsAsync(IEnumerable<Reward> rewards);
    Task<List<Reward>> LoadRewardsAsync();

    Task SaveUserProfileAsync(UserProfile user);
    Task<UserProfile> LoadUserProfileAsync();

    void SaveAllSync(IEnumerable<TaskBase> tasks, IEnumerable<Reward> rewards, UserProfile profile, IEnumerable<Tag> tags);

    Task AddLogEntryAsync(LogEntry entry);
    void AddLogEntrySync(LogEntry entry);
    Task ReplaceLogEntriesAsync(IEnumerable<LogEntry> entries);
    Task DeleteLogEntryAsync(Guid entryId);
    Task<int> MergeActivityLogEntriesAsync(string activityTitle, Guid? targetTaskId, Guid? targetRewardId);
    Task<LogEntry?> FindPreviousLogEntryAsync(LogType type, Guid? taskId, Guid? rewardId, Guid excludeEntryId);
    Task<List<LogEntry>> LoadRecentLogEntriesAsync(int count = 50);
    Task<List<LogEntry>> LoadFilteredLogEntriesAsync(int count, DateTimeOffset from, DateTimeOffset to);
    Task<List<LogEntry>> LoadAllLogEntriesAsync();
    Task<TimeSpan> GetActivityDurationForTaskSinceAsync(Guid taskId, DateTimeOffset since);
}

public interface ILocalTaskAppDataStore : ITaskAppDataStore
{
    string DataDirectory { get; }

    void RefreshDataDirectory();
}
