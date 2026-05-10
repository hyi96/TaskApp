using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TaskApp.Models;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ILocalUserCatalog _userService;
    private readonly ITaskAppDataStore _dataStore;
    private readonly MainWindowViewModel _mainViewModel;
    private readonly Window _parentWindow;
    private ThemeMode _selectedTheme;
    private string _newUserName = string.Empty;
    private string _renameUserName = string.Empty;
    private string _cloudApiUrl = string.Empty;
    private string _cloudAccountId = string.Empty;
    private string _cloudApiKey = string.Empty;
    private string _cloudAccountSecret = string.Empty;
    private string _cloudStatus = string.Empty;
    private bool _isCloudLoginVerified;
    private Guid? _verifiedCloudAccountId;
    private string _verifiedCloudDisplayName = string.Empty;
    private User? _selectedUser;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(ThemeMode.System, "Follow System"),
        new ThemeOption(ThemeMode.Light, "Light Mode"),
        new ThemeOption(ThemeMode.Dark, "Dark Mode")
    };

    public ThemeMode SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                SettingsService.Instance.ThemeMode = value;
            }
        }
    }

    // User management properties
    public ObservableCollection<User> Users { get; } = new();

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            SetProperty(ref _selectedUser, value);
            OnPropertyChanged(nameof(CanDeleteUser));
            OnPropertyChanged(nameof(CanExportUser));
        }
    }

    public string CurrentUserName => _userService.CurrentUser?.Name ?? "Unknown";

    public string NewUserName
    {
        get => _newUserName;
        set => SetProperty(ref _newUserName, value);
    }

    public bool CanDeleteUser => Users.Count > 1 && SelectedUser != null &&
                                  SelectedUser.Id != _userService.CurrentUser?.Id;

    public bool CanExportUser => SelectedUser != null;

    public string RenameUserName
    {
        get => _renameUserName;
        set => SetProperty(ref _renameUserName, value);
    }

    public string CloudApiUrl
    {
        get => _cloudApiUrl;
        set
        {
            if (SetProperty(ref _cloudApiUrl, value))
            {
                SettingsService.Instance.CloudApiUrl = value;
                InvalidateCloudLogin();
            }
        }
    }

    public string CloudAccountId
    {
        get => _cloudAccountId;
        set
        {
            if (SetProperty(ref _cloudAccountId, value))
            {
                SettingsService.Instance.CloudAccountId = value;
                InvalidateCloudLogin();
            }
        }
    }

    public string CloudApiKey
    {
        get => _cloudApiKey;
        set
        {
            if (SetProperty(ref _cloudApiKey, value))
            {
                SettingsService.Instance.CloudApiKey = value;
            }
        }
    }

    public string CloudAccountSecret
    {
        get => _cloudAccountSecret;
        set
        {
            if (SetProperty(ref _cloudAccountSecret, value))
            {
                SettingsService.Instance.CloudAccountSecret = value;
                InvalidateCloudLogin();
            }
        }
    }

    public string CloudLoginStatus
    {
        get
        {
            if (_isCloudLoginVerified && _verifiedCloudAccountId is Guid accountId)
            {
                var displayName = string.IsNullOrWhiteSpace(_verifiedCloudDisplayName)
                    ? "cloud account"
                    : _verifiedCloudDisplayName.Trim();
                return $"Cloud login: verified as {displayName} ({ShortAccountId(accountId)}).";
            }

            if (HasSavedCloudCredentials)
            {
                return "Cloud login: saved credentials are not verified. Click Login.";
            }

            return "Cloud login: not logged in.";
        }
    }

    public bool IsCloudLoginVerified => _isCloudLoginVerified;

    public string CloudStatus
    {
        get => _cloudStatus;
        private set => SetProperty(ref _cloudStatus, value);
    }

    // Commands
    public ICommand SwitchUserCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand RenameUserCommand { get; }
    public ICommand ExportUserCommand { get; }
    public ICommand ImportUserCommand { get; }
    public ICommand CreateCloudAccountCommand { get; }
    public ICommand LoginCloudAccountCommand { get; }
    public ICommand CopyCloudAccountSecretCommand { get; }
    public ICommand UploadCurrentProfileCommand { get; }
    public ICommand UploadAllProfilesCommand { get; }
    public ICommand DownloadCurrentProfileCommand { get; }

    public SettingsViewModel(
        ILocalUserCatalog userService,
        ITaskAppDataStore dataStore,
        MainWindowViewModel mainViewModel,
        Window parentWindow)
    {
        _userService = userService;
        _dataStore = dataStore;
        _mainViewModel = mainViewModel;
        _parentWindow = parentWindow;
        _selectedTheme = SettingsService.Instance.ThemeMode;
        _cloudApiUrl = SettingsService.Instance.CloudApiUrl;
        _cloudAccountId = SettingsService.Instance.CloudAccountId;
        _cloudApiKey = SettingsService.Instance.CloudApiKey;
        _cloudAccountSecret = SettingsService.Instance.CloudAccountSecret;

        RefreshUsers();

        SwitchUserCommand = new AsyncRelayCommand(SwitchUserAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync);
        RenameUserCommand = new AsyncRelayCommand(RenameUserAsync);
        ExportUserCommand = new AsyncRelayCommand(ExportUserAsync);
        ImportUserCommand = new AsyncRelayCommand(ImportUserAsync);
        CreateCloudAccountCommand = new AsyncRelayCommand(CreateCloudAccountAsync);
        LoginCloudAccountCommand = new AsyncRelayCommand(LoginCloudAccountAsync);
        CopyCloudAccountSecretCommand = new AsyncRelayCommand(CopyCloudAccountSecretAsync);
        UploadCurrentProfileCommand = new AsyncRelayCommand(UploadCurrentProfileAsync);
        UploadAllProfilesCommand = new AsyncRelayCommand(UploadAllProfilesAsync);
        DownloadCurrentProfileCommand = new AsyncRelayCommand(DownloadCurrentProfileAsync);
    }

    private async Task SwitchUserAsync()
    {
        if (SelectedUser == null || SelectedUser.Id == _userService.CurrentUser?.Id) 
            return;
        
        // Close the settings window first to avoid UI conflicts with popups
        _parentWindow.Close();
        
        await _userService.SwitchUserAsync(SelectedUser.Id);
    }

    private async Task CreateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUserName)) return;
        await _userService.CreateUserAsync(NewUserName);
        NewUserName = string.Empty;
        RefreshUsers();
    }

    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null || !CanDeleteUser) return;

        var confirmed = await ShowConfirmationAsync(
            "Delete User",
            $"Are you sure you want to delete \"{SelectedUser.Name}\"? This cannot be undone.");
        if (!confirmed) return;

        await _userService.DeleteUserAsync(SelectedUser.Id);
        RefreshUsers();
        OnPropertyChanged(nameof(CurrentUserName));
    }

    private async Task RenameUserAsync()
    {
        if (SelectedUser == null || string.IsNullOrWhiteSpace(RenameUserName)) return;
        await _userService.RenameUserAsync(SelectedUser.Id, RenameUserName);
        RenameUserName = string.Empty;
        RefreshUsers();
        OnPropertyChanged(nameof(CurrentUserName));
    }

    private async Task ExportUserAsync()
    {
        if (SelectedUser == null) return;

        var storageProvider = _parentWindow.StorageProvider;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export User Data",
            SuggestedFileName = $"{SelectedUser.Name}_backup.taskapp",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("TaskApp Backup") { Patterns = new[] { "*.taskapp" } },
                new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } }
            }
        });

        if (file != null)
        {
            try
            {
                await _userService.ExportUserAsync(SelectedUser.Id, file.Path.LocalPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            }
        }
    }

    private async Task ImportUserAsync()
    {
        var storageProvider = _parentWindow.StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import User Data",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("TaskApp Backup") { Patterns = new[] { "*.taskapp", "*.zip" } }
            }
        });

        if (files.Count > 0)
        {
            try
            {
                var importedUser = await _userService.ImportUserAsync(files[0].Path.LocalPath);
                RefreshUsers();
                SelectedUser = importedUser;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex.Message}");
            }
        }
    }

    private async Task CreateCloudAccountAsync()
    {
        if (!TryCreateCloudClient(out var client, includeServerApiKey: true))
        {
            return;
        }

        try
        {
            var account = await client.CreateAccountAsync("Desktop account");
            CloudAccountId = account.Id.ToString();
            if (!string.IsNullOrWhiteSpace(account.LoginSecret))
            {
                CloudAccountSecret = account.LoginSecret;
                MarkCloudLoginVerified(account);
                CloudStatus = $"Created account {account.Id}. Account secret saved. Use Copy Secret for Android login.";
                return;
            }

            InvalidateCloudLogin();
            CloudStatus = "Cloud account was created, but the API did not return an account secret. Redeploy the latest API and create a new account.";
        }
        catch (Exception ex)
        {
            InvalidateCloudLogin();
            CloudStatus = $"Cloud account creation failed: {ex.Message}";
        }
    }

    private async Task LoginCloudAccountAsync()
    {
        if (!TryCreateCloudClient(out var client) ||
            !TryGetCloudAccountId(out var accountId) ||
            !TryGetCloudAccountSecret(out var accountSecret))
        {
            return;
        }

        try
        {
            var account = await client.LoginAccountAsync(accountId, accountSecret);
            MarkCloudLoginVerified(account);
            CloudStatus = $"Logged in to {account.DisplayName} ({account.Id}).";
        }
        catch (Exception ex)
        {
            InvalidateCloudLogin();
            CloudStatus = $"Cloud login failed: {ex.Message}";
        }
    }

    private async Task CopyCloudAccountSecretAsync()
    {
        var accountSecret = CloudAccountSecret.Trim();
        if (string.IsNullOrWhiteSpace(accountSecret))
        {
            CloudStatus = "No account secret is saved. Create a new account first.";
            return;
        }

        if (_parentWindow.Clipboard == null)
        {
            CloudStatus = "Clipboard is unavailable. Select and copy the Account Secret field manually.";
            return;
        }

        await _parentWindow.Clipboard.SetTextAsync(accountSecret);
        CloudStatus = "Account secret copied. Paste it into the Android Account Secret field.";
    }

    private async Task UploadCurrentProfileAsync()
    {
        if (!TryCreateCloudClient(out var client) ||
            !TryGetCloudAccountId(out var accountId) ||
            !TryGetCloudAccountSecret(out _))
        {
            return;
        }

        var currentUser = _userService.CurrentUser;
        if (currentUser == null)
        {
            CloudStatus = "No current profile selected.";
            return;
        }

        try
        {
            await _mainViewModel.SaveDataAsync();
            var snapshot = await _dataStore.LoadSnapshotAsync();
            var result = await client.UploadProfileSnapshotAsync(accountId, currentUser.Id, currentUser.Name, snapshot);
            MarkCloudLoginVerified(accountId);
            CloudStatus = $"Uploaded {result.ProfileName} at {result.UpdatedAt.LocalDateTime:g}.";
        }
        catch (Exception ex)
        {
            InvalidateCloudLogin();
            CloudStatus = $"Cloud upload failed: {ex.Message}";
        }
    }

    private async Task UploadAllProfilesAsync()
    {
        if (!TryCreateCloudClient(out var client) ||
            !TryGetCloudAccountId(out var accountId) ||
            !TryGetCloudAccountSecret(out _))
        {
            return;
        }

        try
        {
            await _mainViewModel.SaveDataAsync();

            var uploadedCount = 0;
            foreach (var user in _userService.Users)
            {
                var userStore = CreateDataStoreForUser(user);
                var snapshot = await userStore.LoadSnapshotAsync();
                await client.UploadProfileSnapshotAsync(accountId, user.Id, user.Name, snapshot);
                uploadedCount++;
            }

            MarkCloudLoginVerified(accountId);
            CloudStatus = $"Uploaded {uploadedCount} local profile(s).";
        }
        catch (Exception ex)
        {
            InvalidateCloudLogin();
            CloudStatus = $"Cloud upload failed: {ex.Message}";
        }
    }

    private async Task DownloadCurrentProfileAsync()
    {
        if (!TryCreateCloudClient(out var client) ||
            !TryGetCloudAccountId(out var accountId) ||
            !TryGetCloudAccountSecret(out _))
        {
            return;
        }

        var currentUser = _userService.CurrentUser;
        if (currentUser == null)
        {
            CloudStatus = "No current profile selected.";
            return;
        }

        try
        {
            var result = await client.DownloadProfileSnapshotAsync(accountId, currentUser.Id);
            MarkCloudLoginVerified(accountId);
            if (result == null)
            {
                CloudStatus = "No cloud snapshot exists for the current profile.";
                return;
            }

            var confirmed = await ShowConfirmationAsync(
                "Download Profile",
                "Replace the current local profile with the cloud snapshot?");
            if (!confirmed)
            {
                CloudStatus = "Cloud download canceled.";
                return;
            }

            await _dataStore.SaveSnapshotAsync(result.Snapshot);
            await _mainViewModel.LoadDataAsync();
            CloudStatus = $"Downloaded {result.ProfileName} from {result.UpdatedAt.LocalDateTime:g}.";
        }
        catch (Exception ex)
        {
            InvalidateCloudLogin();
            CloudStatus = $"Cloud download failed: {ex.Message}";
        }
    }

    private bool HasSavedCloudCredentials =>
        Uri.TryCreate(CloudApiUrl, UriKind.Absolute, out _) &&
        Guid.TryParse(CloudAccountId, out _) &&
        !string.IsNullOrWhiteSpace(CloudAccountSecret);

    private void MarkCloudLoginVerified(AccountResponse account)
    {
        MarkCloudLoginVerified(account.Id, account.DisplayName);
    }

    private void MarkCloudLoginVerified(Guid accountId, string displayName = "")
    {
        _isCloudLoginVerified = true;
        _verifiedCloudAccountId = accountId;
        _verifiedCloudDisplayName = displayName;
        RefreshCloudLoginStatus();
    }

    private void InvalidateCloudLogin()
    {
        _isCloudLoginVerified = false;
        _verifiedCloudAccountId = null;
        _verifiedCloudDisplayName = string.Empty;
        RefreshCloudLoginStatus();
    }

    private void RefreshCloudLoginStatus()
    {
        OnPropertyChanged(nameof(CloudLoginStatus));
        OnPropertyChanged(nameof(IsCloudLoginVerified));
    }

    private static string ShortAccountId(Guid accountId)
    {
        var value = accountId.ToString();
        return value[..8];
    }

    private void RefreshUsers()
    {
        Users.Clear();
        foreach (var user in _userService.Users)
        {
            Users.Add(user);
        }
        SelectedUser = _userService.CurrentUser;
        OnPropertyChanged(nameof(CanDeleteUser));
        OnPropertyChanged(nameof(CanExportUser));
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = _parentWindow.Background
        };

        var yesButton = new Button { Content = "Yes", Width = 80 };
        var noButton = new Button { Content = "No", Width = 80 };

        yesButton.Click += (_, _) => { result = true; dialog.Close(); };
        noButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = Avalonia.Media.Brushes.White
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { yesButton, noButton }
                }
            }
        };

        await dialog.ShowDialog(_parentWindow);
        return result;
    }

    private bool TryCreateCloudClient(out TaskAppCloudClient client, bool includeServerApiKey = false)
    {
        client = null!;

        if (!Uri.TryCreate(CloudApiUrl, UriKind.Absolute, out var baseUri))
        {
            CloudStatus = "Cloud API URL is invalid.";
            return false;
        }

        client = new TaskAppCloudClient(
            new HttpClient { BaseAddress = baseUri },
            includeServerApiKey ? CloudApiKey : null,
            includeServerApiKey ? null : CloudAccountSecret);
        return true;
    }

    private bool TryGetCloudAccountId(out Guid accountId)
    {
        if (Guid.TryParse(CloudAccountId, out accountId))
        {
            return true;
        }

        CloudStatus = "Cloud account ID is invalid.";
        return false;
    }

    private bool TryGetCloudAccountSecret(out string accountSecret)
    {
        accountSecret = CloudAccountSecret.Trim();
        if (!string.IsNullOrWhiteSpace(accountSecret))
        {
            return true;
        }

        CloudStatus = "Cloud account secret is required. Create an account or paste the secret and log in.";
        return false;
    }

    private StorageService CreateDataStoreForUser(User user)
    {
        return new StorageService(new SingleUserCatalog(user, _userService.GetUserDataDirectory(user.Id)));
    }

    private sealed class SingleUserCatalog : ILocalUserCatalog
    {
        private readonly User _user;
        private readonly string _dataDirectory;

        public SingleUserCatalog(User user, string dataDirectory)
        {
            _user = user;
            _dataDirectory = dataDirectory;
        }

        public IReadOnlyList<User> Users => new[] { _user };
        public User? CurrentUser => _user;
        public event Action? CurrentUserChanged { add { } remove { } }
        public void LoadSync() { }
        public Task LoadAsync() => Task.CompletedTask;
        public Task<User> CreateUserAsync(string name) => throw new NotSupportedException();
        public Task DeleteUserAsync(Guid userId) => throw new NotSupportedException();
        public Task SwitchUserAsync(Guid userId) => Task.CompletedTask;
        public Task RenameUserAsync(Guid userId, string newName) => throw new NotSupportedException();
        public Task ExportUserAsync(Guid userId, string exportFilePath) => throw new NotSupportedException();
        public Task<User> ImportUserAsync(string importFilePath, string? newUserName = null) => throw new NotSupportedException();
        public string GetUserDataDirectory(Guid userId) => _dataDirectory;
        public string GetCurrentUserDataDirectory() => _dataDirectory;
        public string GetDataLocationDiagnostics(Guid userId) => _dataDirectory;
    }
}

public class ThemeOption
{
    public ThemeMode Value { get; }
    public string DisplayName { get; }

    public ThemeOption(ThemeMode value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}
