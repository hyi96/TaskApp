using System;
using System.Collections.Generic;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private ThemeMode _selectedTheme;
    
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
    
    public SettingsViewModel()
    {
        _selectedTheme = SettingsService.Instance.ThemeMode;
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
