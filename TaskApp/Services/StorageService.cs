using System;
using System.Collections.Generic;
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

public class StorageService
{
    private readonly string _dataDirectory;
    private const string TasksFileName = "tasks.json";
    private const string RewardsFileName = "rewards.json";
    private const string TagsFileName = "tags.json";
    private const string UserProfileFileName = "user.json";
    private const string LogsDbFileName = "logs.db";

    public StorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dataDirectory = Path.Combine(appData, "TaskApp");
        
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    public string DataDirectory => _dataDirectory;

    public async Task SaveTagsAsync(IEnumerable<Tag> tags)
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        var tagsData = tags.Select(t => new TagData { Id = t.Id, Name = t.Name }).ToList();
        var json = JsonSerializer.Serialize(tagsData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Tag>> LoadTagsAsync()
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        if (!File.Exists(filePath))
        {
            // Default tags
            return new List<Tag>
            {
                new("Health"),
                new("Work"),
                new("Urgent"),
                new("Personal")
            };
        }

        var json = await File.ReadAllTextAsync(filePath);
        try
        {
            var dataList = JsonSerializer.Deserialize<List<TagData>>(json);
            return dataList?.Select(t => new Tag(t.Name, t.Id)).ToList() ?? new List<Tag>();
        }
        catch (JsonException)
        {
            // Fallback for legacy string format
            var stringList = JsonSerializer.Deserialize<List<string>>(json);
            return stringList?.Select(s => new Tag(s)).ToList() ?? new List<Tag>();
        }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskBase> tasks)
    {
        var dataList = tasks.Select(TaskMapper.ToData).ToList();
        var filePath = Path.Combine(_dataDirectory, TasksFileName);

        var json = JsonSerializer.Serialize(dataList, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<TaskBase>> LoadTasksAsync()
    {
        var filePath = Path.Combine(_dataDirectory, TasksFileName);
        if (!File.Exists(filePath))
        {
            return new List<TaskBase>();
        }

        var json = await File.ReadAllTextAsync(filePath);
        var dataList = JsonSerializer.Deserialize<List<TaskData>>(json);
        
        if (dataList == null) return new List<TaskBase>();

        return dataList.Select(TaskMapper.ToModel).ToList();
    }

    public async Task SaveRewardsAsync(IEnumerable<Reward> rewards)
    {
        var dataList = rewards.Select(RewardMapper.ToData).ToList();
        var filePath = Path.Combine(_dataDirectory, RewardsFileName);
        var json = JsonSerializer.Serialize(dataList, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Reward>> LoadRewardsAsync()
    {
        var filePath = Path.Combine(_dataDirectory, RewardsFileName);
        if (!File.Exists(filePath))
        {
            return new List<Reward>();
        }

        var json = await File.ReadAllTextAsync(filePath);
        var dataList = JsonSerializer.Deserialize<List<RewardData>>(json);
        
        if (dataList == null) return new List<Reward>();

        return dataList.Select(RewardMapper.ToModel).ToList();
    }

    public async Task SaveUserProfileAsync(UserProfile user)
    {
        var filePath = Path.Combine(_dataDirectory, UserProfileFileName);
        var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<UserProfile> LoadUserProfileAsync()
    {
        var filePath = Path.Combine(_dataDirectory, UserProfileFileName);
        if (!File.Exists(filePath))
        {
            return new UserProfile();
        }

        var json = await File.ReadAllTextAsync(filePath);
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
        { "CountDelta", "REAL NULL" },
        { "DurationTicks", "INTEGER NULL" },
        { "TitleSnapshot", "TEXT NOT NULL" }
    };

    private async Task EnsureLogsTableAsync()
    {
        var dbPath = GetLogsDbPath();
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
        catch
        {
            // If PRAGMA fails or columns already exist, continue
        }
    }

    public async Task AddLogEntryAsync(LogEntry entry)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO LogEntries (Id, Timestamp, Type, TaskId, RewardId, GoldDelta, CountDelta, DurationTicks, TitleSnapshot)
                                VALUES ($id, $timestamp, $type, $taskId, $rewardId, $goldDelta, $countDelta, $durationTicks, $titleSnapshot);";
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("$type", (int)entry.Type);
        command.Parameters.AddWithValue("$taskId", (object?)entry.TaskId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$rewardId", (object?)entry.RewardId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$goldDelta", entry.GoldDelta);
        command.Parameters.AddWithValue("$countDelta", (object?)entry.CountDelta ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationTicks", (object?)entry.Duration?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("$titleSnapshot", entry.TitleSnapshot ?? string.Empty);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<LogEntry>> LoadRecentLogEntriesAsync(int count = 50)
    {
        await EnsureLogsTableAsync();

        var dbPath = GetLogsDbPath();
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Timestamp, Type, TaskId, RewardId, GoldDelta, CountDelta, DurationTicks, TitleSnapshot
                                FROM LogEntries
                                ORDER BY Timestamp DESC
                                LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", count);

        var entries = new List<LogEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var entry = new LogEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Type = (LogType)reader.GetInt32(2),
                TaskId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                RewardId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                GoldDelta = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5)),
                CountDelta = reader.IsDBNull(6) ? null : Convert.ToDouble(reader.GetValue(6)),
                Duration = reader.IsDBNull(7) ? null : TimeSpan.FromTicks(reader.GetInt64(7)),
                TitleSnapshot = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
            };

            entries.Add(entry);
        }

        return entries;
    }
}
