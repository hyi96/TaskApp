using TaskApp.Models;

namespace TaskApp.Services;

public interface IUserCatalog
{
    IReadOnlyList<User> Users { get; }

    User? CurrentUser { get; }

    event Action? CurrentUserChanged;

    Task LoadAsync();
    Task<User> CreateUserAsync(string name);
    Task DeleteUserAsync(Guid userId);
    Task SwitchUserAsync(Guid userId);
    Task RenameUserAsync(Guid userId, string newName);
}

public interface ILocalUserCatalog : IUserCatalog
{
    void LoadSync();

    Task ExportUserAsync(Guid userId, string exportFilePath);
    Task<User> ImportUserAsync(string importFilePath, string? newUserName = null);

    string GetUserDataDirectory(Guid userId);
    string GetCurrentUserDataDirectory();
    string GetDataLocationDiagnostics(Guid userId);
}
