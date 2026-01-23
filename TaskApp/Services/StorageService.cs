using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public class StorageService
{
    private readonly string _dataDirectory;
    private const string TasksFileName = "tasks.json";
    private const string RewardsFileName = "rewards.json";
    private const string TagsFileName = "tags.json";
    private const string UserProfileFileName = "user.json";

    public StorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dataDirectory = Path.Combine(appData, "TaskApp");
        
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    public async Task SaveTagsAsync(IEnumerable<string> tags)
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        var json = JsonSerializer.Serialize(tags, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<string>> LoadTagsAsync()
    {
        var filePath = Path.Combine(_dataDirectory, TagsFileName);
        if (!File.Exists(filePath))
        {
            // Default tags
            return new List<string> { "Health", "Work", "Urgent", "Personal" };
        }

        var json = await File.ReadAllTextAsync(filePath);
        var dataList = JsonSerializer.Deserialize<List<string>>(json);
        return dataList ?? new List<string>();
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
}
