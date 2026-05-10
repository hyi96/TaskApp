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
    private string _cloudStatus = string.Empty;
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
            }
        }
    }

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
    public ICommand UploadCurrentProfileCommand { get; }
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

        RefreshUsers();

        SwitchUserCommand = new AsyncRelayCommand(SwitchUserAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync);
        RenameUserCommand = new AsyncRelayCommand(RenameUserAsync);
        ExportUserCommand = new AsyncRelayCommand(ExportUserAsync);
        ImportUserCommand = new AsyncRelayCommand(ImportUserAsync);
        CreateCloudAccountCommand = new AsyncRelayCommand(CreateCloudAccountAsync);
        UploadCurrentProfileCommand = new AsyncRelayCommand(UploadCurrentProfileAsync);
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
        if (!TryCreateCloudClient(out var client))
        {
            return;
        }

        try
        {
            var account = await client.CreateAccountAsync("Desktop account");
            CloudAccountId = account.Id.ToString();
            CloudStatus = $"Created account {account.Id}.";
        }
        catch (Exception ex)
        {
            CloudStatus = $"Cloud account creation failed: {ex.Message}";
        }
    }

    private async Task UploadCurrentProfileAsync()
    {
        if (!TryCreateCloudClient(out var client) || !TryGetCloudAccountId(out var accountId))
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
            CloudStatus = $"Uploaded {result.ProfileName} at {result.UpdatedAt.LocalDateTime:g}.";
        }
        catch (Exception ex)
        {
            CloudStatus = $"Cloud upload failed: {ex.Message}";
        }
    }

    private async Task DownloadCurrentProfileAsync()
    {
        if (!TryCreateCloudClient(out var client) || !TryGetCloudAccountId(out var accountId))
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
            if (result == null)
            {
                CloudStatus = "No cloud snapshot exists for the current profile.";
                return;
            }

            await _dataStore.SaveSnapshotAsync(result.Snapshot);
            await _mainViewModel.LoadDataAsync();
            CloudStatus = $"Downloaded {result.ProfileName} from {result.UpdatedAt.LocalDateTime:g}.";
        }
        catch (Exception ex)
        {
            CloudStatus = $"Cloud download failed: {ex.Message}";
        }
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

    private bool TryCreateCloudClient(out TaskAppCloudClient client)
    {
        client = null!;

        if (!Uri.TryCreate(CloudApiUrl, UriKind.Absolute, out var baseUri))
        {
            CloudStatus = "Cloud API URL is invalid.";
            return false;
        }

        client = new TaskAppCloudClient(new HttpClient { BaseAddress = baseUri });
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
