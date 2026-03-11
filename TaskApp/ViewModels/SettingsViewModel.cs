using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TaskApp.Models;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly UserService _userService;
    private readonly Window _parentWindow;
    private ThemeMode _selectedTheme;
    private string _newUserName = string.Empty;
    private string _renameUserName = string.Empty;
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

    // Commands
    public ICommand SwitchUserCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand RenameUserCommand { get; }
    public ICommand ExportUserCommand { get; }
    public ICommand ImportUserCommand { get; }

    public SettingsViewModel(UserService userService, Window parentWindow)
    {
        _userService = userService;
        _parentWindow = parentWindow;
        _selectedTheme = SettingsService.Instance.ThemeMode;

        RefreshUsers();

        SwitchUserCommand = new AsyncRelayCommand(SwitchUserAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync);
        RenameUserCommand = new AsyncRelayCommand(RenameUserAsync);
        ExportUserCommand = new AsyncRelayCommand(ExportUserAsync);
        ImportUserCommand = new AsyncRelayCommand(ImportUserAsync);
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
