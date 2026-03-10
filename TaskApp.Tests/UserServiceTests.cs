using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskApp.Models;
using TaskApp.Services;
using Xunit;

namespace TaskApp.Tests;

public class UserServiceTests : IDisposable
{
    private readonly string _tempDir;

    public UserServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region LoadSync / LoadAsync

    [Fact]
    public void LoadSync_CreatesDefaultUser_WhenNoUsersExist()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Single(svc.Users);
        Assert.Equal("Default", svc.Users[0].Name);
        Assert.NotNull(svc.CurrentUser);
        Assert.Equal("Default", svc.CurrentUser!.Name);
    }

    [Fact]
    public async Task LoadAsync_CreatesDefaultUser_WhenNoUsersExist()
    {
        var svc = new UserService(_tempDir);
        await svc.LoadAsync();

        Assert.Single(svc.Users);
        Assert.Equal("Default", svc.Users[0].Name);
        Assert.NotNull(svc.CurrentUser);
    }

    [Fact]
    public void LoadSync_PersistsUsersFile()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        var usersFile = Path.Combine(_tempDir, "users.json");
        Assert.True(File.Exists(usersFile));

        var json = File.ReadAllText(usersFile);
        var users = JsonSerializer.Deserialize<User[]>(json);
        Assert.NotNull(users);
        Assert.Single(users);
        Assert.Equal("Default", users![0].Name);
    }

    [Fact]
    public void LoadSync_RestoresExistingUsers()
    {
        // Arrange: pre-create a users file
        var userId = Guid.NewGuid();
        var users = new[] { new User { Id = userId, Name = "Alice" } };
        var usersFile = Path.Combine(_tempDir, "users.json");
        File.WriteAllText(usersFile, JsonSerializer.Serialize(users));

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Single(svc.Users);
        Assert.Equal("Alice", svc.Users[0].Name);
        Assert.Equal(userId, svc.Users[0].Id);
    }

    [Fact]
    public void LoadSync_FallsBackToFirstUser_WhenCurrentUserFileIsMissing()
    {
        var user1 = new User { Name = "First" };
        var user2 = new User { Name = "Second" };
        var usersFile = Path.Combine(_tempDir, "users.json");
        File.WriteAllText(usersFile, JsonSerializer.Serialize(new[] { user1, user2 }));

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Equal(user1.Id, svc.CurrentUser!.Id);
    }

    [Fact]
    public void LoadSync_RestoresCurrentUserSelection()
    {
        var user1 = new User { Name = "First" };
        var user2 = new User { Name = "Second" };
        var usersFile = Path.Combine(_tempDir, "users.json");
        var currentUserFile = Path.Combine(_tempDir, "current_user.json");
        File.WriteAllText(usersFile, JsonSerializer.Serialize(new[] { user1, user2 }));
        File.WriteAllText(currentUserFile, JsonSerializer.Serialize(user2.Id));

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        Assert.Equal(user2.Id, svc.CurrentUser!.Id);
    }

    [Fact]
    public void LoadSync_HandlesCorruptedUsersFile()
    {
        var usersFile = Path.Combine(_tempDir, "users.json");
        File.WriteAllText(usersFile, "NOT VALID JSON!!!");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // Should fall back to creating a default user
        Assert.Single(svc.Users);
        Assert.Equal("Default", svc.Users[0].Name);
    }

    [Fact]
    public void LoadSync_HandlesCorruptedCurrentUserFile()
    {
        var user1 = new User { Name = "Alice" };
        var usersFile = Path.Combine(_tempDir, "users.json");
        var currentUserFile = Path.Combine(_tempDir, "current_user.json");
        File.WriteAllText(usersFile, JsonSerializer.Serialize(new[] { user1 }));
        File.WriteAllText(currentUserFile, "GARBAGE");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // Should fall back to first user
        Assert.Equal(user1.Id, svc.CurrentUser!.Id);
    }

    #endregion

    #region CreateUserAsync

    [Fact]
    public async Task CreateUserAsync_AddsUserToList()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var initialCount = svc.Users.Count;

        var newUser = await svc.CreateUserAsync("Bob");

        Assert.Equal(initialCount + 1, svc.Users.Count);
        Assert.Equal("Bob", newUser.Name);
        Assert.Contains(svc.Users, u => u.Id == newUser.Id);
    }

    [Fact]
    public async Task CreateUserAsync_CreatesUserDataDirectory()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        var newUser = await svc.CreateUserAsync("Bob");

        var userDir = svc.GetUserDataDirectory(newUser.Id);
        Assert.True(Directory.Exists(userDir));
    }

    [Fact]
    public async Task CreateUserAsync_PersistsToUsersFile()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        await svc.CreateUserAsync("Charlie");

        // Reload from scratch
        var svc2 = new UserService(_tempDir);
        svc2.LoadSync();
        Assert.Contains(svc2.Users, u => u.Name == "Charlie");
    }

    #endregion

    #region DeleteUserAsync

    [Fact]
    public async Task DeleteUserAsync_RemovesUser()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");

        await svc.DeleteUserAsync(bob.Id);

        Assert.DoesNotContain(svc.Users, u => u.Id == bob.Id);
    }

    [Fact]
    public async Task DeleteUserAsync_PreventsDeleteLastUser()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        // Only "Default" user exists
        var onlyUser = svc.Users[0];

        await svc.DeleteUserAsync(onlyUser.Id);

        // Should still have the user
        Assert.Single(svc.Users);
        Assert.Equal(onlyUser.Id, svc.Users[0].Id);
    }

    [Fact]
    public async Task DeleteUserAsync_DeletesDataDirectory()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");
        var bobDir = svc.GetUserDataDirectory(bob.Id);
        Assert.True(Directory.Exists(bobDir));

        await svc.DeleteUserAsync(bob.Id);

        Assert.False(Directory.Exists(bobDir));
    }

    [Fact]
    public async Task DeleteUserAsync_SwitchesUser_WhenCurrentIsDeleted()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");
        await svc.SwitchUserAsync(bob.Id);
        Assert.Equal(bob.Id, svc.CurrentUser!.Id);

        await svc.DeleteUserAsync(bob.Id);

        // Should have switched to another user
        Assert.NotNull(svc.CurrentUser);
        Assert.NotEqual(bob.Id, svc.CurrentUser!.Id);
    }

    [Fact]
    public async Task DeleteUserAsync_NoOp_WhenUserNotFound()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var initialCount = svc.Users.Count;

        await svc.DeleteUserAsync(Guid.NewGuid());

        Assert.Equal(initialCount, svc.Users.Count);
    }

    #endregion

    #region SwitchUserAsync

    [Fact]
    public async Task SwitchUserAsync_ChangesCurrentUser()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");

        await svc.SwitchUserAsync(bob.Id);

        Assert.Equal(bob.Id, svc.CurrentUser!.Id);
    }

    [Fact]
    public async Task SwitchUserAsync_PersistsSelection()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");
        await svc.SwitchUserAsync(bob.Id);

        // Reload from scratch
        var svc2 = new UserService(_tempDir);
        svc2.LoadSync();
        Assert.Equal(bob.Id, svc2.CurrentUser!.Id);
    }

    [Fact]
    public async Task SwitchUserAsync_FiresCurrentUserChangedEvent()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var bob = await svc.CreateUserAsync("Bob");
        var fired = false;
        svc.CurrentUserChanged += () => fired = true;

        await svc.SwitchUserAsync(bob.Id);

        Assert.True(fired);
    }

    [Fact]
    public async Task SwitchUserAsync_NoOp_WhenUserNotFound()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var original = svc.CurrentUser!.Id;

        await svc.SwitchUserAsync(Guid.NewGuid());

        Assert.Equal(original, svc.CurrentUser!.Id);
    }

    #endregion

    #region RenameUserAsync

    [Fact]
    public async Task RenameUserAsync_ChangesUserName()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var user = svc.Users[0];

        await svc.RenameUserAsync(user.Id, "NewName");

        Assert.Equal("NewName", user.Name);
    }

    [Fact]
    public async Task RenameUserAsync_TrimsWhitespace()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var user = svc.Users[0];

        await svc.RenameUserAsync(user.Id, "  Trimmed  ");

        Assert.Equal("Trimmed", user.Name);
    }

    [Fact]
    public async Task RenameUserAsync_NoOp_WhenNameIsEmpty()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var user = svc.Users[0];
        var originalName = user.Name;

        await svc.RenameUserAsync(user.Id, "");

        Assert.Equal(originalName, user.Name);
    }

    [Fact]
    public async Task RenameUserAsync_NoOp_WhenNameIsWhitespace()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var user = svc.Users[0];
        var originalName = user.Name;

        await svc.RenameUserAsync(user.Id, "   ");

        Assert.Equal(originalName, user.Name);
    }

    [Fact]
    public async Task RenameUserAsync_NoOp_WhenUserNotFound()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // Should not throw
        await svc.RenameUserAsync(Guid.NewGuid(), "Whatever");
    }

    [Fact]
    public async Task RenameUserAsync_PersistsToFile()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var user = svc.Users[0];

        await svc.RenameUserAsync(user.Id, "Persisted");

        var svc2 = new UserService(_tempDir);
        svc2.LoadSync();
        Assert.Equal("Persisted", svc2.Users.First(u => u.Id == user.Id).Name);
    }

    #endregion

    #region ExportUserAsync / ImportUserAsync

    [Fact]
    public async Task ExportImport_RoundTrip_PreservesUserData()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var original = await svc.CreateUserAsync("ExportMe");

        // Write some data to the user directory
        var userDir = svc.GetUserDataDirectory(original.Id);
        await File.WriteAllTextAsync(Path.Combine(userDir, "tasks.json"), "[{\"test\": true}]");

        var exportPath = Path.Combine(_tempDir, "export.taskapp");
        await svc.ExportUserAsync(original.Id, exportPath);
        Assert.True(File.Exists(exportPath));

        var imported = await svc.ImportUserAsync(exportPath);

        Assert.NotEqual(original.Id, imported.Id); // New ID
        // Name gets deduplicated because "ExportMe" already exists
        Assert.StartsWith("ExportMe", imported.Name);

        // Verify data was copied
        var importedDir = svc.GetUserDataDirectory(imported.Id);
        Assert.True(File.Exists(Path.Combine(importedDir, "tasks.json")));
    }

    [Fact]
    public async Task ImportUserAsync_GeneratesUniqueName_WhenDuplicate()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();
        var original = await svc.CreateUserAsync("Duped");

        var userDir = svc.GetUserDataDirectory(original.Id);
        await File.WriteAllTextAsync(Path.Combine(userDir, "tasks.json"), "[]");

        var exportPath = Path.Combine(_tempDir, "export.taskapp");
        await svc.ExportUserAsync(original.Id, exportPath);

        var imported = await svc.ImportUserAsync(exportPath);

        // Name should be deduplicated
        Assert.NotEqual("Duped", imported.Name);
        Assert.StartsWith("Duped", imported.Name);
    }

    [Fact]
    public async Task ExportUserAsync_Throws_WhenUserNotFound()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        var exportPath = Path.Combine(_tempDir, "export.taskapp");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExportUserAsync(Guid.NewGuid(), exportPath));
    }

    [Fact]
    public async Task ImportUserAsync_Throws_WhenFileNotFound()
    {
        var svc = new UserService(_tempDir);
        svc.LoadSync();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => svc.ImportUserAsync(Path.Combine(_tempDir, "nonexistent.taskapp")));
    }

    #endregion

    #region MigrateExistingDataIfNeeded

    [Fact]
    public void LoadSync_MigratesLegacyData_WhenUserDirIsEmpty()
    {
        // Arrange: create a legacy file in the root
        var legacyTasksPath = Path.Combine(_tempDir, "tasks.json");
        File.WriteAllText(legacyTasksPath, "[{\"Id\":\"00000000-0000-0000-0000-000000000001\"}]");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        // The data should have been copied to the user's directory
        var userDir = svc.GetUserDataDirectory(svc.CurrentUser!.Id);
        var userTasksPath = Path.Combine(userDir, "tasks.json");
        Assert.True(File.Exists(userTasksPath));
        Assert.Equal(File.ReadAllText(legacyTasksPath), File.ReadAllText(userTasksPath));
    }

    [Fact]
    public void LoadSync_SkipsLegacyMigration_WhenLegacyFileTooSmall()
    {
        // Arrange: create a tiny legacy file (< 5 bytes)
        var legacyTasksPath = Path.Combine(_tempDir, "tasks.json");
        File.WriteAllText(legacyTasksPath, "[]");

        var svc = new UserService(_tempDir);
        svc.LoadSync();

        var userDir = svc.GetUserDataDirectory(svc.CurrentUser!.Id);
        var userTasksPath = Path.Combine(userDir, "tasks.json");
        // Migration should have skipped since legacy file is too small
        Assert.False(File.Exists(userTasksPath));
    }

    #endregion

    #region GetUserDataDirectory

    [Fact]
    public void GetUserDataDirectory_ReturnsCorrectPath()
    {
        var svc = new UserService(_tempDir);
        var userId = Guid.NewGuid();

        var dir = svc.GetUserDataDirectory(userId);

        Assert.Equal(Path.Combine(_tempDir, "Users", userId.ToString()), dir);
    }

    [Fact]
    public void GetCurrentUserDataDirectory_ThrowsWhenNoUser()
    {
        var svc = new UserService(_tempDir);
        // Don't call LoadSync so _currentUser is null

        Assert.Throws<InvalidOperationException>(() => svc.GetCurrentUserDataDirectory());
    }

    #endregion
}
