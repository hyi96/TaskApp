using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskApp.Models;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using Xunit;

namespace TaskApp.Tests;

/// <summary>
/// Tests that verify the backup/recovery mechanism protects data
/// against corrupted files (e.g. from unexpected system shutdowns).
/// </summary>
public class BackupRecoveryTests : IDisposable
{
    private readonly string _tempDir;

    public BackupRecoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private (UserService userService, StorageService storageService) CreateServices()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return (userService, storageService);
    }

    #region StorageService — Backup file creation

    [Fact]
    public async Task SaveTasks_CreatesBackupFile_OnSecondSave()
    {
        var (_, storage) = CreateServices();
        var task = new TodoTask();
        task.UpdateTitle("First save");

        await storage.SaveTasksAsync(new[] { task });
        var tasksPath = Path.Combine(storage.DataDirectory, "tasks.json");
        Assert.True(File.Exists(tasksPath));
        Assert.False(File.Exists(tasksPath + ".bak"));

        // Second save should create a backup
        task.UpdateTitle("Second save");
        await storage.SaveTasksAsync(new[] { task });
        Assert.True(File.Exists(tasksPath + ".bak"));

        // Backup should contain the first save's data
        var backupContent = await File.ReadAllTextAsync(tasksPath + ".bak");
        Assert.Contains("First save", backupContent);
    }

    [Fact]
    public async Task SaveRewards_CreatesBackupFile_OnSecondSave()
    {
        var (_, storage) = CreateServices();
        var reward = new Reward("First reward", goldCost: 5);

        await storage.SaveRewardsAsync(new[] { reward });
        var rewardsPath = Path.Combine(storage.DataDirectory, "rewards.json");
        Assert.True(File.Exists(rewardsPath));
        Assert.False(File.Exists(rewardsPath + ".bak"));

        reward.UpdateTitle("Updated reward");
        await storage.SaveRewardsAsync(new[] { reward });
        Assert.True(File.Exists(rewardsPath + ".bak"));

        var backupContent = await File.ReadAllTextAsync(rewardsPath + ".bak");
        Assert.Contains("First reward", backupContent);
    }

    [Fact]
    public async Task SaveTags_CreatesBackupFile_OnSecondSave()
    {
        var (_, storage) = CreateServices();
        var tags = new List<Tag> { new("Original") };

        await storage.SaveTagsAsync(tags);
        var tagsPath = Path.Combine(storage.DataDirectory, "tags.json");
        Assert.True(File.Exists(tagsPath));
        Assert.False(File.Exists(tagsPath + ".bak"));

        await storage.SaveTagsAsync(new List<Tag> { new("Updated") });
        Assert.True(File.Exists(tagsPath + ".bak"));

        var backupContent = await File.ReadAllTextAsync(tagsPath + ".bak");
        Assert.Contains("Original", backupContent);
    }

    [Fact]
    public async Task SaveUserProfile_CreatesBackupFile_OnSecondSave()
    {
        var (_, storage) = CreateServices();
        var profile = new UserProfile { Gold = 100 };

        await storage.SaveUserProfileAsync(profile);
        var profilePath = Path.Combine(storage.DataDirectory, "user.json");
        Assert.True(File.Exists(profilePath));
        Assert.False(File.Exists(profilePath + ".bak"));

        profile.Gold = 200;
        await storage.SaveUserProfileAsync(profile);
        Assert.True(File.Exists(profilePath + ".bak"));

        var backupContent = await File.ReadAllTextAsync(profilePath + ".bak");
        Assert.Contains("100", backupContent);
    }

    #endregion

    #region StorageService — Recovery from corrupted primary file

    [Fact]
    public async Task LoadTasks_RecoverFromBackup_WhenPrimaryCorrupted()
    {
        var (_, storage) = CreateServices();
        var task = new TodoTask();
        task.UpdateTitle("Important task");

        // Save twice to create a backup
        await storage.SaveTasksAsync(new[] { task });
        await storage.SaveTasksAsync(new[] { task });

        // Corrupt the primary file with null bytes
        var tasksPath = Path.Combine(storage.DataDirectory, "tasks.json");
        await File.WriteAllTextAsync(tasksPath, "\0\0\0\0\0");

        var loaded = await storage.LoadTasksAsync();

        Assert.Single(loaded);
        Assert.Equal("Important task", loaded[0].Title);
    }

    [Fact]
    public async Task LoadRewards_RecoverFromBackup_WhenPrimaryCorrupted()
    {
        var (_, storage) = CreateServices();
        var reward = new Reward("Saved reward", goldCost: 10);

        await storage.SaveRewardsAsync(new[] { reward });
        await storage.SaveRewardsAsync(new[] { reward });

        var rewardsPath = Path.Combine(storage.DataDirectory, "rewards.json");
        await File.WriteAllTextAsync(rewardsPath, "\0\0\0");

        var loaded = await storage.LoadRewardsAsync();

        Assert.Single(loaded);
        Assert.Equal("Saved reward", loaded[0].Title);
    }

    [Fact]
    public async Task LoadTags_RecoverFromBackup_WhenPrimaryCorrupted()
    {
        var (_, storage) = CreateServices();
        var tags = new List<Tag> { new("Recovered") };

        await storage.SaveTagsAsync(tags);
        await storage.SaveTagsAsync(tags);

        var tagsPath = Path.Combine(storage.DataDirectory, "tags.json");
        await File.WriteAllTextAsync(tagsPath, "\0\0\0");

        var loaded = await storage.LoadTagsAsync();

        Assert.Single(loaded);
        Assert.Equal("Recovered", loaded[0].Name);
    }

    [Fact]
    public async Task LoadUserProfile_RecoverFromBackup_WhenPrimaryCorrupted()
    {
        var (_, storage) = CreateServices();
        var profile = new UserProfile { Gold = 42 };

        await storage.SaveUserProfileAsync(profile);
        await storage.SaveUserProfileAsync(profile);

        var profilePath = Path.Combine(storage.DataDirectory, "user.json");
        await File.WriteAllTextAsync(profilePath, "\0\0\0");

        var loaded = await storage.LoadUserProfileAsync();

        Assert.Equal(42, loaded.Gold);
    }

    #endregion

    #region StorageService — Recovery from missing primary file

    [Fact]
    public async Task LoadTasks_RecoverFromBackup_WhenPrimaryMissing()
    {
        var (_, storage) = CreateServices();
        var task = new HabitTask();
        task.UpdateTitle("Habit from backup");

        await storage.SaveTasksAsync(new[] { task });
        await storage.SaveTasksAsync(new[] { task });

        // Delete the primary, leaving only the backup
        var tasksPath = Path.Combine(storage.DataDirectory, "tasks.json");
        File.Delete(tasksPath);
        Assert.True(File.Exists(tasksPath + ".bak"));

        var loaded = await storage.LoadTasksAsync();

        Assert.Single(loaded);
        Assert.Equal("Habit from backup", loaded[0].Title);
    }

    [Fact]
    public async Task LoadRewards_RecoverFromBackup_WhenPrimaryMissing()
    {
        var (_, storage) = CreateServices();
        var reward = new Reward("Backup reward");

        await storage.SaveRewardsAsync(new[] { reward });
        await storage.SaveRewardsAsync(new[] { reward });

        var rewardsPath = Path.Combine(storage.DataDirectory, "rewards.json");
        File.Delete(rewardsPath);

        var loaded = await storage.LoadRewardsAsync();

        Assert.Single(loaded);
        Assert.Equal("Backup reward", loaded[0].Title);
    }

    #endregion

    #region StorageService — Graceful empty defaults when both files are corrupted

    [Fact]
    public async Task LoadTasks_ReturnsEmpty_WhenBothPrimaryAndBackupCorrupted()
    {
        var (_, storage) = CreateServices();
        var task = new TodoTask();
        task.UpdateTitle("Doomed task");

        await storage.SaveTasksAsync(new[] { task });
        await storage.SaveTasksAsync(new[] { task });

        var tasksPath = Path.Combine(storage.DataDirectory, "tasks.json");
        await File.WriteAllTextAsync(tasksPath, "\0\0\0");
        await File.WriteAllTextAsync(tasksPath + ".bak", "\0\0\0");

        var loaded = await storage.LoadTasksAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadRewards_ReturnsEmpty_WhenBothPrimaryAndBackupCorrupted()
    {
        var (_, storage) = CreateServices();
        await storage.SaveRewardsAsync(new[] { new Reward("Gone") });
        await storage.SaveRewardsAsync(new[] { new Reward("Gone") });

        var rewardsPath = Path.Combine(storage.DataDirectory, "rewards.json");
        await File.WriteAllTextAsync(rewardsPath, "\0\0\0");
        await File.WriteAllTextAsync(rewardsPath + ".bak", "\0\0\0");

        var loaded = await storage.LoadRewardsAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadTags_ReturnsEmpty_WhenBothCorrupted()
    {
        var (_, storage) = CreateServices();
        await storage.SaveTagsAsync(new List<Tag> { new("Lost") });
        await storage.SaveTagsAsync(new List<Tag> { new("Lost") });

        var tagsPath = Path.Combine(storage.DataDirectory, "tags.json");
        await File.WriteAllTextAsync(tagsPath, "\0\0\0");
        await File.WriteAllTextAsync(tagsPath + ".bak", "\0\0\0");

        var loaded = await storage.LoadTagsAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadUserProfile_ReturnsDefault_WhenBothCorrupted()
    {
        var (_, storage) = CreateServices();
        await storage.SaveUserProfileAsync(new UserProfile { Gold = 999 });
        await storage.SaveUserProfileAsync(new UserProfile { Gold = 999 });

        var profilePath = Path.Combine(storage.DataDirectory, "user.json");
        await File.WriteAllTextAsync(profilePath, "\0\0\0");
        await File.WriteAllTextAsync(profilePath + ".bak", "\0\0\0");

        var loaded = await storage.LoadUserProfileAsync();

        Assert.Equal(0, loaded.Gold);
    }

    #endregion

    #region StorageService — Recovery from invalid JSON (not just null bytes)

    [Fact]
    public async Task LoadTasks_ReturnsEmpty_WhenPrimaryHasInvalidJsonAndBackupAlsoInvalid()
    {
        var (_, storage) = CreateServices();
        var task = new TodoTask();
        task.UpdateTitle("Survives bad JSON");

        await storage.SaveTasksAsync(new[] { task });
        await storage.SaveTasksAsync(new[] { task });

        // Both have structurally invalid JSON (not null bytes)
        var tasksPath = Path.Combine(storage.DataDirectory, "tasks.json");
        await File.WriteAllTextAsync(tasksPath, "{ broken json !!!");
        await File.WriteAllTextAsync(tasksPath + ".bak", "also broken");

        var loaded = await storage.LoadTasksAsync();

        // Neither is detectable as corrupted at read time, but both fail deserialization
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadRewards_ReturnsEmpty_WhenBothHaveInvalidJson()
    {
        var (_, storage) = CreateServices();
        await storage.SaveRewardsAsync(new[] { new Reward("Test") });
        await storage.SaveRewardsAsync(new[] { new Reward("Test") });

        var rewardsPath = Path.Combine(storage.DataDirectory, "rewards.json");
        await File.WriteAllTextAsync(rewardsPath, "not json");
        await File.WriteAllTextAsync(rewardsPath + ".bak", "also not json");

        var loaded = await storage.LoadRewardsAsync();

        Assert.Empty(loaded);
    }

    #endregion

    #region StorageService — SaveAllSync backup rotation

    [Fact]
    public async Task SaveAllSync_CreatesBackupFiles()
    {
        var (_, storage) = CreateServices();
        var tasks = new List<TaskBase> { CreateTodo("Task A") };
        var rewards = new List<Reward> { new("Reward A") };
        var profile = new UserProfile { Gold = 50 };
        var tags = new List<Tag> { new("Tag A") };

        // First save — no backups yet
        storage.SaveAllSync(tasks, rewards, profile, tags);

        // Second save — backups should be created
        tasks[0].UpdateTitle("Task B");
        storage.SaveAllSync(tasks, rewards, profile, tags);

        var dir = storage.DataDirectory;
        Assert.True(File.Exists(Path.Combine(dir, "tasks.json.bak")));
        Assert.True(File.Exists(Path.Combine(dir, "rewards.json.bak")));
        Assert.True(File.Exists(Path.Combine(dir, "user.json.bak")));
        Assert.True(File.Exists(Path.Combine(dir, "tags.json.bak")));

        // Backup should have the first save's content
        var backupContent = await File.ReadAllTextAsync(Path.Combine(dir, "tasks.json.bak"));
        Assert.Contains("Task A", backupContent);
    }

    #endregion

    #region StorageService — No data loss when files never existed

    [Fact]
    public async Task LoadTasks_ReturnsEmpty_WhenNeitherFileExists()
    {
        var (_, storage) = CreateServices();
        var loaded = await storage.LoadTasksAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadRewards_ReturnsEmpty_WhenNeitherFileExists()
    {
        var (_, storage) = CreateServices();
        var loaded = await storage.LoadRewardsAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadTags_ReturnsDefaults_WhenNeitherFileExists()
    {
        var (_, storage) = CreateServices();
        var loaded = await storage.LoadTagsAsync();

        // Should return default tags on first launch
        Assert.Equal(4, loaded.Count);
        Assert.Contains(loaded, t => t.Name == "Health");
    }

    [Fact]
    public async Task LoadUserProfile_ReturnsDefault_WhenNeitherFileExists()
    {
        var (_, storage) = CreateServices();
        var loaded = await storage.LoadUserProfileAsync();
        Assert.Equal(0, loaded.Gold);
    }

    #endregion

    #region UserService — Backup recovery for users file

    [Fact]
    public void LoadSync_RecoverUsersFromBackup_WhenPrimaryCorrupted()
    {
        var user = new User { Name = "Alice" };
        var usersFile = Path.Combine(_tempDir, "users.json");
        var backupFile = usersFile + ".bak";

        // Write a valid backup and a corrupted primary
        File.WriteAllText(backupFile, JsonSerializer.Serialize(new[] { user }));
        File.WriteAllText(usersFile, "\0\0\0\0\0");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Single(svc.Users);
        Assert.Equal("Alice", svc.Users[0].Name);
    }

    [Fact]
    public void LoadSync_RecoverUsersFromBackup_WhenPrimaryMissing()
    {
        var user = new User { Name = "Bob" };
        var backupFile = Path.Combine(_tempDir, "users.json.bak");
        File.WriteAllText(backupFile, JsonSerializer.Serialize(new[] { user }));

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // Should find Bob from backup, not create a new default
        Assert.Contains(svc.Users, u => u.Name == "Bob");
    }

    [Fact]
    public void LoadSync_RecoverCurrentUserFromBackup_WhenPrimaryCorrupted()
    {
        var user1 = new User { Name = "First" };
        var user2 = new User { Name = "Second" };
        var usersFile = Path.Combine(_tempDir, "users.json");
        var currentUserFile = Path.Combine(_tempDir, "current_user.json");

        File.WriteAllText(usersFile, JsonSerializer.Serialize(new[] { user1, user2 }));
        File.WriteAllText(currentUserFile, "\0\0\0"); // corrupted
        File.WriteAllText(currentUserFile + ".bak", JsonSerializer.Serialize(user2.Id));

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Equal(user2.Id, svc.CurrentUser!.Id);
    }

    [Fact]
    public void LoadSync_CreatesDefault_WhenBothUsersFilesCorrupted()
    {
        var usersFile = Path.Combine(_tempDir, "users.json");
        File.WriteAllText(usersFile, "\0\0\0");
        File.WriteAllText(usersFile + ".bak", "\0\0\0");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // Should fall back to creating a default user
        Assert.Single(svc.Users);
        Assert.Equal("Default", svc.Users[0].Name);
    }

    #endregion

    #region UserService — Save creates backups

    [Fact]
    public async Task SwitchUser_CreatesBackupOfCurrentUserFile()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var defaultUser = svc.CurrentUser!;

        var user2 = await svc.CreateUserAsync("Second");
        var user3 = await svc.CreateUserAsync("Third");
        var currentUserFile = Path.Combine(_tempDir, "current_user.json");

        // First switch creates current_user.json (no backup yet since file didn't exist)
        await svc.SwitchUserAsync(user2.Id);
        Assert.True(File.Exists(currentUserFile));

        // Second switch should backup the previous current_user.json
        await svc.SwitchUserAsync(user3.Id);
        Assert.True(File.Exists(currentUserFile + ".bak"));

        var backupContent = await File.ReadAllTextAsync(currentUserFile + ".bak");
        Assert.Contains(user2.Id.ToString(), backupContent);
    }

    [Fact]
    public async Task CreateUser_CreatesBackupOfUsersFile()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var usersFile = Path.Combine(_tempDir, "users.json");

        // First create should make a backup
        await svc.CreateUserAsync("NewUser");

        Assert.True(File.Exists(usersFile + ".bak"));

        // Backup should contain only the original default user
        var backupContent = await File.ReadAllTextAsync(usersFile + ".bak");
        Assert.Contains("Default", backupContent);
        Assert.DoesNotContain("NewUser", backupContent);
    }

    #endregion

    #region StorageService — Backup contains previous version, not current

    [Fact]
    public async Task BackupAlwaysContainsPreviousVersion()
    {
        var (_, storage) = CreateServices();
        var dir = storage.DataDirectory;
        var tasksPath = Path.Combine(dir, "tasks.json");

        // Save version 1
        await storage.SaveTasksAsync(new[] { CreateTodo("Version1") });

        // Save version 2 — backup should have version 1
        await storage.SaveTasksAsync(new[] { CreateTodo("Version2") });
        var backup1 = await File.ReadAllTextAsync(tasksPath + ".bak");
        Assert.Contains("Version1", backup1);
        Assert.DoesNotContain("Version2", backup1);

        // Save version 3 — backup should have version 2
        await storage.SaveTasksAsync(new[] { CreateTodo("Version3") });
        var backup2 = await File.ReadAllTextAsync(tasksPath + ".bak");
        Assert.Contains("Version2", backup2);
        Assert.DoesNotContain("Version3", backup2);

        // Current should have version 3
        var current = await File.ReadAllTextAsync(tasksPath);
        Assert.Contains("Version3", current);
    }

    #endregion

    #region StorageService — Full round-trip recovery scenario

    [Fact]
    public async Task FullRecoveryScenario_SimulatesCrashAndRecovers()
    {
        var (userService, storage) = CreateServices();

        // Build up state over multiple saves
        var task = new TodoTask();
        task.UpdateTitle("My important task");
        await storage.SaveTasksAsync(new[] { task });

        var reward = new Reward("Weekly treat", goldCost: 5);
        await storage.SaveRewardsAsync(new[] { reward });

        var profile = new UserProfile { Gold = 150 };
        await storage.SaveUserProfileAsync(profile);

        var tags = new List<Tag> { new("Custom") };
        await storage.SaveTagsAsync(tags);

        // Save again so backups exist
        task.UpdateTitle("My updated task");
        await storage.SaveTasksAsync(new[] { task });
        await storage.SaveRewardsAsync(new[] { reward });
        await storage.SaveUserProfileAsync(profile);
        await storage.SaveTagsAsync(tags);

        // Simulate crash — corrupt ALL primary files
        var dir = storage.DataDirectory;
        await File.WriteAllTextAsync(Path.Combine(dir, "tasks.json"), "\0\0\0");
        await File.WriteAllTextAsync(Path.Combine(dir, "rewards.json"), "\0\0\0");
        await File.WriteAllTextAsync(Path.Combine(dir, "user.json"), "\0\0\0");
        await File.WriteAllTextAsync(Path.Combine(dir, "tags.json"), "\0\0\0");

        // Reload — should recover from backups
        var loadedTasks = await storage.LoadTasksAsync();
        var loadedRewards = await storage.LoadRewardsAsync();
        var loadedProfile = await storage.LoadUserProfileAsync();
        var loadedTags = await storage.LoadTagsAsync();

        // Backups contain the pre-update versions
        Assert.Single(loadedTasks);
        Assert.Equal("My important task", loadedTasks[0].Title);
        Assert.Single(loadedRewards);
        Assert.Equal("Weekly treat", loadedRewards[0].Title);
        Assert.Equal(150, loadedProfile.Gold);
        Assert.Single(loadedTags);
        Assert.Equal("Custom", loadedTags[0].Name);
    }

    #endregion

    private static TodoTask CreateTodo(string title)
    {
        var task = new TodoTask();
        task.UpdateTitle(title);
        return task;
    }
}
