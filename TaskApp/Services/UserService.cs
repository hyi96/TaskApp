using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskApp.Models;

namespace TaskApp.Services;

public class UserService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskApp");

    private static readonly string UsersFile = Path.Combine(AppDataFolder, "users.json");
    private static readonly string CurrentUserFile = Path.Combine(AppDataFolder, "current_user.json");

    private List<User> _users = new();
    private User? _currentUser;

    public IReadOnlyList<User> Users => _users.AsReadOnly();
    public User? CurrentUser => _currentUser;

    public event Action? CurrentUserChanged;

    /// <summary>
    /// Synchronous load for use during app startup to avoid deadlocks.
    /// </summary>
    public void LoadSync()
    {
        Directory.CreateDirectory(AppDataFolder);

        // Load users list
        if (File.Exists(UsersFile))
        {
            try
            {
                var json = File.ReadAllText(UsersFile);
                _users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch
            {
                _users = new List<User>();
            }
        }

        // Create default user if none exist
        if (_users.Count == 0)
        {
            var defaultUser = new User { Name = "Default" };
            _users.Add(defaultUser);
            SaveUsersSync();
        }

        // Load current user selection
        Guid? currentUserId = null;
        if (File.Exists(CurrentUserFile))
        {
            try
            {
                var json = File.ReadAllText(CurrentUserFile);
                currentUserId = JsonSerializer.Deserialize<Guid>(json);
            }
            catch
            {
                // Ignore deserialization errors
            }
        }

        _currentUser = _users.FirstOrDefault(u => u.Id == currentUserId) ?? _users.First();

        // Always try to migrate legacy data for the current user if needed
        MigrateExistingDataIfNeeded(_currentUser.Id);
    }

    public async Task LoadAsync()
    {
        Directory.CreateDirectory(AppDataFolder);

        // Load users list
        if (File.Exists(UsersFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(UsersFile);
                _users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch
            {
                _users = new List<User>();
            }
        }

        // Create default user if none exist
        if (_users.Count == 0)
        {
            var defaultUser = new User { Name = "Default" };
            _users.Add(defaultUser);
            await SaveUsersAsync();
        }

        // Load current user selection
        Guid? currentUserId = null;
        if (File.Exists(CurrentUserFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(CurrentUserFile);
                currentUserId = JsonSerializer.Deserialize<Guid>(json);
            }
            catch
            {
                // Ignore deserialization errors
            }
        }

        _currentUser = _users.FirstOrDefault(u => u.Id == currentUserId) ?? _users.First();

        // Always try to migrate legacy data for the current user if needed
        MigrateExistingDataIfNeeded(_currentUser.Id);
    }

    /// <summary>
    /// Migrates legacy data from the root AppData folder to the user-specific folder
    /// if the user folder is missing files but legacy files exist.
    /// </summary>
    private void MigrateExistingDataIfNeeded(Guid userId)
    {
        var userDataDir = GetUserDataDirectory(userId);
        Directory.CreateDirectory(userDataDir);

        var filesToMigrate = new[] { "tasks.json", "rewards.json", "tags.json", "user.json", "logs.db" };

        foreach (var fileName in filesToMigrate)
        {
            var legacyPath = Path.Combine(AppDataFolder, fileName);
            var userPath = Path.Combine(userDataDir, fileName);

            // If legacy file exists and user file doesn't exist
            if (File.Exists(legacyPath) && !File.Exists(userPath))
            {
                try
                {
                    File.Copy(legacyPath, userPath);
                }
                catch
                {
                    // Ignore migration errors
                }
            }
            // Also copy if user file is empty but legacy has content
            else if (File.Exists(legacyPath) && File.Exists(userPath) && fileName.EndsWith(".json"))
            {
                try
                {
                    var userFileInfo = new FileInfo(userPath);
                    var legacyFileInfo = new FileInfo(legacyPath);

                    if (userFileInfo.Length < 10 && legacyFileInfo.Length > 10)
                    {
                        File.Copy(legacyPath, userPath, overwrite: true);
                    }
                }
                catch
                {
                    // Ignore migration errors
                }
            }
        }
    }

    public async Task<User> CreateUserAsync(string name)
    {
        var user = new User { Name = name };
        _users.Add(user);
        await SaveUsersAsync();

        // Create user data directory
        Directory.CreateDirectory(GetUserDataDirectory(user.Id));

        return user;
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user == null || _users.Count <= 1) return; // Prevent deleting last user

        _users.Remove(user);

        // Delete user's data directory
        var userDir = GetUserDataDirectory(userId);
        if (Directory.Exists(userDir))
        {
            try
            {
                Directory.Delete(userDir, recursive: true);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        await SaveUsersAsync();

        // Switch to another user if current was deleted
        if (_currentUser?.Id == userId)
        {
            await SwitchUserAsync(_users.First().Id);
        }
    }

    public async Task SwitchUserAsync(Guid userId)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return;

        _currentUser = user;
        var json = JsonSerializer.Serialize(userId);
        await File.WriteAllTextAsync(CurrentUserFile, json);

        CurrentUserChanged?.Invoke();
    }

    /// <summary>
    /// Exports user data to a ZIP archive at the specified path.
    /// </summary>
    public async Task ExportUserAsync(Guid userId, string exportFilePath)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user == null) throw new InvalidOperationException("User not found");

        var userDataDir = GetUserDataDirectory(userId);

        // Create export metadata
        var exportMetadata = new UserExportMetadata
        {
            ExportedAt = DateTime.UtcNow,
            AppVersion = "1.0.0",
            UserName = user.Name,
            OriginalUserId = user.Id
        };

        // Delete existing file if present
        if (File.Exists(exportFilePath))
        {
            File.Delete(exportFilePath);
        }

        using var archive = ZipFile.Open(exportFilePath, ZipArchiveMode.Create);

        // Add metadata
        var metadataEntry = archive.CreateEntry("metadata.json");
        await using (var stream = metadataEntry.Open())
        {
            await JsonSerializer.SerializeAsync(stream, exportMetadata, new JsonSerializerOptions { WriteIndented = true });
        }

        // Add all user data files
        if (Directory.Exists(userDataDir))
        {
            foreach (var filePath in Directory.GetFiles(userDataDir))
            {
                var fileName = Path.GetFileName(filePath);
                archive.CreateEntryFromFile(filePath, $"data/{fileName}");
            }
        }
    }

    /// <summary>
    /// Imports user data from a ZIP archive and creates a new user.
    /// </summary>
    /// <returns>The newly created user from imported data</returns>
    public async Task<User> ImportUserAsync(string importFilePath, string? newUserName = null)
    {
        if (!File.Exists(importFilePath))
            throw new FileNotFoundException("Import file not found", importFilePath);

        using var archive = ZipFile.OpenRead(importFilePath);

        // Read metadata
        var metadataEntry = archive.GetEntry("metadata.json");
        UserExportMetadata? metadata = null;

        if (metadataEntry != null)
        {
            await using var stream = metadataEntry.Open();
            metadata = await JsonSerializer.DeserializeAsync<UserExportMetadata>(stream);
        }

        // Determine user name
        var userName = newUserName
            ?? metadata?.UserName
            ?? $"Imported User {DateTime.Now:yyyy-MM-dd}";

        // Ensure unique name
        var baseName = userName;
        var counter = 1;
        while (_users.Any(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase)))
        {
            userName = $"{baseName} ({counter++})";
        }

        // Create new user with new ID (don't reuse original ID to avoid conflicts)
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Name = userName,
            CreatedAt = DateTime.UtcNow
        };

        var newUserDataDir = GetUserDataDirectory(newUser.Id);
        Directory.CreateDirectory(newUserDataDir);

        // Extract data files
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("data/") && !string.IsNullOrEmpty(entry.Name))
            {
                var destPath = Path.Combine(newUserDataDir, entry.Name);
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        _users.Add(newUser);
        await SaveUsersAsync();

        return newUser;
    }

    public string GetUserDataDirectory(Guid userId)
    {
        return Path.Combine(AppDataFolder, "Users", userId.ToString());
    }

    public string GetCurrentUserDataDirectory()
    {
        if (_currentUser == null) throw new InvalidOperationException("No user selected");
        return GetUserDataDirectory(_currentUser.Id);
    }

    private void SaveUsersSync()
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UsersFile, json);
    }

    private async Task SaveUsersAsync()
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(UsersFile, json);
    }
}
