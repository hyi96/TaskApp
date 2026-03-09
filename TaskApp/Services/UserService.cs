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
    private static readonly string DefaultAppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskApp");

    private readonly string AppDataFolder;
    private readonly string UsersFile;
    private readonly string CurrentUserFile;
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private List<User> _users = new();
    private User? _currentUser;

    public IReadOnlyList<User> Users => _users.AsReadOnly();
    public User? CurrentUser => _currentUser;

    public event Action? CurrentUserChanged;

    /// <summary>
    /// Creates a UserService that stores data in the default application data folder.
    /// </summary>
    public UserService() : this(DefaultAppDataFolder) { }

    /// <summary>
    /// Creates a UserService that stores data in the specified folder.
    /// Use this constructor in tests to avoid touching real user data.
    /// </summary>
    public UserService(string appDataFolder)
    {
        AppDataFolder = appDataFolder;
        UsersFile = Path.Combine(AppDataFolder, "users.json");
        CurrentUserFile = Path.Combine(AppDataFolder, "current_user.json");
    }

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
    /// Migrates legacy data from the root AppData folder to the user-specific folder.
    /// Prioritizes larger/non-empty legacy files to handle cases where user directory has placeholder files.
    /// </summary>
    private void MigrateExistingDataIfNeeded(Guid userId)
    {
        var userDataDir = GetUserDataDirectory(userId);
        Directory.CreateDirectory(userDataDir);

        var filesToMigrate = new[] { "tasks.json", "rewards.json", "tags.json", "user.json", "logs.db" };
        const int MinFileSize = 5; // Minimum meaningful file size (in bytes)

        foreach (var fileName in filesToMigrate)
        {
            var legacyPath = Path.Combine(AppDataFolder, fileName);
            var userPath = Path.Combine(userDataDir, fileName);

            // Skip if legacy file doesn't exist or is too small
            if (!File.Exists(legacyPath))
                continue;

            var legacyFileInfo = new FileInfo(legacyPath);
            if (legacyFileInfo.Length < MinFileSize)
                continue; // Legacy file is empty/too small, skip it

            // If user file doesn't exist, copy legacy
            if (!File.Exists(userPath))
            {
                try
                {
                    File.Copy(legacyPath, userPath);
                }
                catch
                {
                    // Ignore copy errors
                }
            }
            else
            {
                // Both exist: prioritize legacy if user file is empty or legacy is significantly larger
                try
                {
                    var userFileInfo = new FileInfo(userPath);
                    
                    // If user file is essentially empty, use legacy
                    if (userFileInfo.Length < MinFileSize)
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
            ExportedAt = DateTimeOffset.UtcNow,
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
            await JsonSerializer.SerializeAsync(stream, exportMetadata, IndentedJsonOptions);
        }

        // Add all user data files
        if (Directory.Exists(userDataDir))
        {
            foreach (var filePath in Directory.GetFiles(userDataDir))
            {
                var fileName = Path.GetFileName(filePath);
                try
                {
                    archive.CreateEntryFromFile(filePath, $"data/{fileName}");
                }
                catch (IOException)
                {
                    // File may be locked (e.g. logs.db held by SQLite) — copy to a temp file first
                    var tempPath = Path.Combine(Path.GetTempPath(), $"taskapp_export_{fileName}");
                    try
                    {
                        File.Copy(filePath, tempPath, overwrite: true);
                        archive.CreateEntryFromFile(tempPath, $"data/{fileName}");
                    }
                    finally
                    {
                        try { File.Delete(tempPath); } catch { /* cleanup best-effort */ }
                    }
                }
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
            CreatedAt = DateTimeOffset.UtcNow
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
        var json = JsonSerializer.Serialize(_users, IndentedJsonOptions);
        File.WriteAllText(UsersFile, json);
    }

    private async Task SaveUsersAsync()
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(_users, IndentedJsonOptions);
        await File.WriteAllTextAsync(UsersFile, json);
    }

    /// <summary>
    /// Gets diagnostic info about where user data is located (legacy vs. user-specific directory).
    /// Useful for debugging migration issues.
    /// </summary>
    public string GetDataLocationDiagnostics(Guid userId)
    {
        var userDataDir = GetUserDataDirectory(userId);
        var filesToCheck = new[] { "tasks.json", "rewards.json", "tags.json", "user.json", "logs.db" };
        var diagnostics = new System.Text.StringBuilder();

        diagnostics.AppendLine($"Diagnostics for user {userId}:");
        diagnostics.AppendLine($"User data directory: {userDataDir}");
        diagnostics.AppendLine($"Legacy data directory: {AppDataFolder}");
        diagnostics.AppendLine();

        foreach (var fileName in filesToCheck)
        {
            var legacyPath = Path.Combine(AppDataFolder, fileName);
            var userPath = Path.Combine(userDataDir, fileName);

            var legacyExists = File.Exists(legacyPath);
            var userExists = File.Exists(userPath);
            var legacySize = legacyExists ? new FileInfo(legacyPath).Length : 0;
            var userSize = userExists ? new FileInfo(userPath).Length : 0;

            diagnostics.AppendLine($"{fileName}:");
            diagnostics.AppendLine($"  Legacy: {(legacyExists ? $"✓ ({legacySize} bytes)" : "✗")}");
            diagnostics.AppendLine($"  User:   {(userExists ? $"✓ ({userSize} bytes)" : "✗")}");
        }

        return diagnostics.ToString();
    }
}
