using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public class StorageService : ILocalTaskAppDataStore
{
    private readonly ILocalUserCatalog _userService;
    private string _dataDirectory = string.Empty;
    private const string TasksFileName = "tasks.json";
    private const string RewardsFileName = "rewards.json";
    private const string TagsFileName = "tags.json";
    private const string UserProfileFileName = "user.json";
    private const string LogsDbFileName = "logs.db";
    private string? _initializedLogsDbPath;

    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private const string BackupExtension = ".bak";
    private const string TempExtension = ".tmp";

    /// <summary>
    /// Detects files corrupted by an unclean shutdown (e.g. filled with null bytes).
    /// </summary>
    private static bool IsCorruptedFile(string json)
    {
        return string.IsNullOrWhiteSpace(json) || json[0] == '\0';
    }

    /// <summary>
    /// Writes text to a file atomically with backup rotation.
    /// Flow: current → .bak, then write .tmp → rename to current.
    /// If the system crashes mid-write, .bak holds the last known-good state.
    /// </summary>
    private static async Task WriteFileAtomicAsync(string filePath, string content)
    {
        var backupPath = filePath + BackupExtension;
        var tempPath = filePath + TempExtension;

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static void WriteFileAtomic(string filePath, string content)
    {
        var backupPath = filePath + BackupExtension;
        var tempPath = filePath + TempExtension;

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        File.WriteAllText(tempPath, content);
        File.Move(tempPath, filePath, overwrite: true);
    }

    /// <summary>
    /// Reads a JSON file, falling back to the .bak copy if the primary is missing or corrupted.
    /// Returns null if neither file yields valid content.
    /// </summary>
    private static async Task<string?> ReadFileWithBackupFallbackAsync(string filePath)
    {
        // Try primary file
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath);
            if (!IsCorruptedFile(json))
            {
                return json;
            }
        }

        // Try backup
        var backupPath = filePath + BackupExtension;
        if (File.Exists(backupPath))
        {
            var backupJson = await File.ReadAllTextAsync(backupPath);
            if (!IsCorruptedFile(backupJson))
            {
                return backupJson;
            }
        }

        return null;
    }

    public StorageService(ILocalUserCatalog userService)
    {
        _userService = userService;
        UpdateDataDirectory();
    }

    private void UpdateDataDirectory()
    {
        _dataDirectory = _userService.GetCurrentUserDataDirectory();

        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    public void RefreshDataDirectory()
    {
        UpdateDataDirectory();
        _initializedLogsDbPath = null;
    }

    public string DataDirectory => _dataDirectory;

    public async Task SaveTagsAsync(IEnumerable<Tag> tags)
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        var tagsData = tags.Select(t => new TagData { Id = t.Id, Name = t.Name }).ToList();
        var json = JsonSerializer.Serialize(tagsData, IndentedJsonOptions);
        await WriteFileAtomicAsync(filePath, json);
    }

    public async Task<List<Tag>> LoadTagsAsync()
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        var json = await ReadFileWithBackupFallbackAsync(filePath);

        if (json == null)
        {
            if (!File.Exists(filePath) && !File.Exists(filePath + BackupExtension))
            {
                // First launch — return defaults
                return new List<Tag>
                {
                    new("Health"),
                    new("Work"),
                    new("Urgent"),
                    new("Personal")
                };
            }

            return new List<Tag>();
        }

        try
        {
            var dataList = JsonSerializer.Deserialize<List<TagData>>(json);
            return dataList?.Select(t => new Tag(t.Name, t.Id)).ToList() ?? new List<Tag>();
        }
        catch (JsonException)
        {
            try
            {
                // Fallback for legacy string format
                var stringList = JsonSerializer.Deserialize<List<string>>(json);
                return stringList?.Select(s => new Tag(s)).ToList() ?? new List<Tag>();
            }
            catch (JsonException)
            {
                return new List<Tag>();
            }
        }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskBase> tasks)
    {
        var dataList = tasks.Select(TaskMapper.ToData).ToList();
        var filePath = Path.Combine(_dataDirectory, TasksFileName);

        var json = JsonSerializer.Serialize(dataList, IndentedJsonOptions);
        await WriteFileAtomicAsync(filePath, json);
    }

    /// <summary>
    /// Synchronous save of all data for use during process shutdown.
    /// </summary>
    public void SaveAllSync(IEnumerable<TaskBase> tasks, IEnumerable<Reward> rewards, UserProfile profile, IEnumerable<Tag> tags)
    {
        var tasksJson = JsonSerializer.Serialize(tasks.Select(TaskMapper.ToData).ToList(), IndentedJsonOptions);
        WriteFileAtomic(Path.Combine(_dataDirectory, TasksFileName), tasksJson);

        var rewardsJson = JsonSerializer.Serialize(rewards.Select(RewardMapper.ToData).ToList(), IndentedJsonOptions);
        WriteFileAtomic(Path.Combine(_dataDirectory, RewardsFileName), rewardsJson);

        var profileJson = JsonSerializer.Serialize(profile, IndentedJsonOptions);
        WriteFileAtomic(Path.Combine(_dataDirectory, UserProfileFileName), profileJson);

        var tagsJson = JsonSerializer.Serialize(tags.Select(t => new TagData { Id = t.Id, Name = t.Name }).ToList(), IndentedJsonOptions);
        WriteFileAtomic(Path.Combine(_dataDirectory, TagsFileName), tagsJson);
    }

    public async Task<List<TaskBase>> LoadTasksAsync()
    {
        var filePath = Path.Combine(_dataDirectory, TasksFileName);
        var json = await ReadFileWithBackupFallbackAsync(filePath);
        if (json == null) return new List<TaskBase>();

        try
        {
            var dataList = JsonSerializer.Deserialize<List<TaskData>>(json);
            if (dataList == null) return new List<TaskBase>();
            return dataList.Select(TaskMapper.ToModel).ToList();
        }
        catch (JsonException)
        {
            return new List<TaskBase>();
        }
    }

    public async Task SaveRewardsAsync(IEnumerable<Reward> rewards)
    {
        var dataList = rewards.Select(RewardMapper.ToData).ToList();
        var filePath = Path.Combine(_dataDirectory, RewardsFileName);
        var json = JsonSerializer.Serialize(dataList, IndentedJsonOptions);
        await WriteFileAtomicAsync(filePath, json);
    }

    public async Task<List<Reward>> LoadRewardsAsync()
    {
        var filePath = Path.Combine(_dataDirectory, RewardsFileName);
        var json = await ReadFileWithBackupFallbackAsync(filePath);
        if (json == null) return new List<Reward>();

        try
        {
            var dataList = JsonSerializer.Deserialize<List<RewardData>>(json);
            if (dataList == null) return new List<Reward>();
            return dataList.Select(RewardMapper.ToModel).ToList();
        }
        catch (JsonException)
        {
            return new List<Reward>();
        }
    }

    public async Task SaveUserProfileAsync(UserProfile user)
    {
        var filePath = Path.Combine(_dataDirectory, UserProfileFileName);
        var json = JsonSerializer.Serialize(user, IndentedJsonOptions);
        await WriteFileAtomicAsync(filePath, json);
    }

    public async Task<UserProfile> LoadUserProfileAsync()
    {
        var filePath = Path.Combine(_dataDirectory, UserProfileFileName);
        var json = await ReadFileWithBackupFallbackAsync(filePath);
        if (json == null) return new UserProfile();

        try
        {
            var user = JsonSerializer.Deserialize<UserProfile>(json);
            return user ?? new UserProfile();
        }
        catch (JsonException)
        {
            return new UserProfile();
        }
    }

    private string GetLogsDbPath() => Path.Combine(_dataDirectory, LogsDbFileName);

    private static readonly Dictionary<string, string> LogEntriesSchema = new()
    {
        { "Id", "TEXT PRIMARY KEY" },
        { "Timestamp", "TEXT NOT NULL" },
        { "Type", "INTEGER NOT NULL" },
        { "TaskId", "TEXT NULL" },
        { "RewardId", "TEXT NULL" },
        { "GoldDelta", "REAL NOT NULL" },
        { "UserGold", "REAL NOT NULL DEFAULT 0" },
        { "CountDelta", "REAL NULL" },
        { "DurationTicks", "INTEGER NULL" },
        { "TitleSnapshot", "TEXT NOT NULL" },
        { "PreviousLastCompletionPeriod", "TEXT NULL" }
    };

    private const string InsertLogEntrySql = @"INSERT INTO LogEntries (Id, Timestamp, Type, TaskId, RewardId, GoldDelta, UserGold, CountDelta, DurationTicks, TitleSnapshot, PreviousLastCompletionPeriod)
                                VALUES ($id, $timestamp, $type, $taskId, $rewardId, $goldDelta, $userGold, $countDelta, $durationTicks, $titleSnapshot, $prevPeriod);";

    private async Task EnsureLogsTableAsync()
    {
        var dbPath = GetLogsDbPath();
        if (_initializedLogsDbPath == dbPath)
            return;

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        var columns = string.Join(",\n                                    ", 
            LogEntriesSchema.Select(kvp => $"{kvp.Key} {kvp.Value}"));
        command.CommandText = $@"CREATE TABLE IF NOT EXISTS LogEntries (
                                    {columns}
                                );";
        await command.ExecuteNonQueryAsync();

        // Ensure all schema columns exist (migration for existing databases)
        await EnsureColumnsExistAsync(connection);

        _initializedLogsDbPath = dbPath;
    }

    private void EnsureLogsTableSync()
    {
        var dbPath = GetLogsDbPath();
        if (_initializedLogsDbPath == dbPath)
            return;

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        var columns = string.Join(",\n                                    ",
            LogEntriesSchema.Select(kvp => $"{kvp.Key} {kvp.Value}"));
        command.CommandText = $@"CREATE TABLE IF NOT EXISTS LogEntries (
                                    {columns}
                                );";
        command.ExecuteNonQuery();

        _initializedLogsDbPath = dbPath;
    }

    private async Task EnsureColumnsExistAsync(SqliteConnection connection)
    {
        try
        {
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "PRAGMA table_info(LogEntries);";
            var existingColumns = new HashSet<string>();
            
            await using var reader = await checkCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }

            foreach (var columnName in LogEntriesSchema.Keys)
            {
                if (!existingColumns.Contains(columnName))
                {
                    var columnDefinition = LogEntriesSchema[columnName];
                    var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = $"ALTER TABLE LogEntries ADD COLUMN {columnName} {columnDefinition};";
                    await alterCommand.ExecuteNonQueryAsync();
                }
            }
        }
        catch (SqliteException)
        {
            // Expected when columns already exist or PRAGMA returns unexpected results
        }
    }

    public async Task AddLogEntryAsync(LogEntry entry)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = InsertLogEntrySql;
        BindLogEntryParameters(command, entry);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Synchronous version of AddLogEntryAsync for use during process shutdown.
    /// </summary>
    public void AddLogEntrySync(LogEntry entry)
    {
        EnsureLogsTableSync();

        var dbPath = GetLogsDbPath();
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = InsertLogEntrySql;
        BindLogEntryParameters(command, entry);

        command.ExecuteNonQuery();
    }

    public async Task ReplaceLogEntriesAsync(IEnumerable<LogEntry> entries)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM LogEntries;";
        await deleteCommand.ExecuteNonQueryAsync();

        foreach (var entry in entries)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = InsertLogEntrySql;
            BindLogEntryParameters(insertCommand, entry);
            await insertCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task DeleteLogEntryAsync(Guid entryId)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM LogEntries WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", entryId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reassigns all orphaned log entries (any type) that match a title (and have no TaskId/RewardId)
    /// to a specific task or reward.
    /// </summary>
    public async Task<int> MergeActivityLogEntriesAsync(string activityTitle, Guid? targetTaskId, Guid? targetRewardId)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"UPDATE LogEntries
                                SET TaskId = $taskId, RewardId = $rewardId
                                WHERE TitleSnapshot = $title COLLATE NOCASE
                                  AND TaskId IS NULL
                                  AND RewardId IS NULL;";
        command.Parameters.AddWithValue("$taskId", (object?)targetTaskId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$rewardId", (object?)targetRewardId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", activityTitle);

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Finds the most recent log entry for a task or reward of a given type,
    /// excluding a specific entry (the one being undone).
    /// </summary>
    public async Task<LogEntry?> FindPreviousLogEntryAsync(LogType type, Guid? taskId, Guid? rewardId, Guid excludeEntryId)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Timestamp, Type, TaskId, RewardId, GoldDelta, UserGold, CountDelta, DurationTicks, TitleSnapshot, PreviousLastCompletionPeriod
                                FROM LogEntries
                                WHERE Type = $type AND Id != $excludeId
                                  AND ($taskId IS NULL OR TaskId = $taskId)
                                  AND ($rewardId IS NULL OR RewardId = $rewardId)
                                ORDER BY Timestamp DESC
                                LIMIT 1;";
        command.Parameters.AddWithValue("$type", (int)type);
        command.Parameters.AddWithValue("$excludeId", excludeEntryId.ToString());
        command.Parameters.AddWithValue("$taskId", (object?)taskId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$rewardId", (object?)rewardId?.ToString() ?? DBNull.Value);

        var results = await ReadLogEntriesAsync(command);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<List<LogEntry>> LoadRecentLogEntriesAsync(int count = 50)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Timestamp, Type, TaskId, RewardId, GoldDelta, UserGold, CountDelta, DurationTicks, TitleSnapshot, PreviousLastCompletionPeriod
                                FROM LogEntries
                                ORDER BY Timestamp DESC
                                LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", count);

        return await ReadLogEntriesAsync(command);
    }

    public async Task<List<LogEntry>> LoadFilteredLogEntriesAsync(int count, DateTimeOffset from, DateTimeOffset to)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Timestamp, Type, TaskId, RewardId, GoldDelta, UserGold, CountDelta, DurationTicks, TitleSnapshot, PreviousLastCompletionPeriod
                                FROM LogEntries
                                WHERE Timestamp >= $from AND Timestamp <= $to
                                ORDER BY Timestamp DESC
                                LIMIT $limit;";
        command.Parameters.AddWithValue("$from", from.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$to", to.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$limit", count);

        return await ReadLogEntriesAsync(command);
    }

    public async Task<List<LogEntry>> LoadAllLogEntriesAsync()
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Timestamp, Type, TaskId, RewardId, GoldDelta, UserGold, CountDelta, DurationTicks, TitleSnapshot, PreviousLastCompletionPeriod
                                FROM LogEntries
                                ORDER BY Timestamp ASC;";

        return await ReadLogEntriesAsync(command);
    }

    public async Task<TimeSpan> GetActivityDurationForTaskSinceAsync(Guid taskId, DateTimeOffset since)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Timestamp, DurationTicks
                                FROM LogEntries
                                WHERE TaskId = $taskId
                                  AND Type = $type
                                  AND DurationTicks IS NOT NULL;";
        command.Parameters.AddWithValue("$taskId", taskId.ToString());
        command.Parameters.AddWithValue("$type", (int)LogType.ActivityDuration);

        long ticks = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var endedAt = DateTimeOffset.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
            var duration = TimeSpan.FromTicks(reader.GetInt64(1));
            var startedAt = endedAt - duration;

            if (endedAt <= since)
            {
                continue;
            }

            var effectiveStart = startedAt > since ? startedAt : since;
            var overlap = endedAt - effectiveStart;
            if (overlap > TimeSpan.Zero)
            {
                ticks += overlap.Ticks;
            }
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static async Task<List<LogEntry>> ReadLogEntriesAsync(SqliteCommand command)
    {
        var entries = new List<LogEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var entry = new LogEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind),
                Type = (LogType)reader.GetInt32(2),
                TaskId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                RewardId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                GoldDelta = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5)),
                UserGold = reader.IsDBNull(6) ? 0 : Convert.ToDouble(reader.GetValue(6)),
                CountDelta = reader.IsDBNull(7) ? null : Convert.ToDouble(reader.GetValue(7)),
                Duration = reader.IsDBNull(8) ? null : TimeSpan.FromTicks(reader.GetInt64(8)),
                TitleSnapshot = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                PreviousLastCompletionPeriod = reader.FieldCount > 10 && !reader.IsDBNull(10)
                    ? DateOnly.Parse(reader.GetString(10))
                    : null
            };

            entries.Add(entry);
        }

        return entries;
    }

    private static void BindLogEntryParameters(SqliteCommand command, LogEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("$type", (int)entry.Type);
        command.Parameters.AddWithValue("$taskId", (object?)entry.TaskId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$rewardId", (object?)entry.RewardId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$goldDelta", entry.GoldDelta);
        command.Parameters.AddWithValue("$userGold", entry.UserGold);
        command.Parameters.AddWithValue("$countDelta", (object?)entry.CountDelta ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationTicks", (object?)entry.Duration?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("$titleSnapshot", entry.TitleSnapshot ?? string.Empty);
        command.Parameters.AddWithValue("$prevPeriod", (object?)entry.PreviousLastCompletionPeriod?.ToString("o") ?? DBNull.Value);
    }
}
