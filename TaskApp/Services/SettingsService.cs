using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaskApp.Services;

public enum ThemeMode
{
    Light,
    Dark,
    System
}

public class AppSettings
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
}

public class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskApp");
    
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    
    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();
    
    private AppSettings _settings = new();
    
    public ThemeMode ThemeMode
    {
        get => _settings.ThemeMode;
        set
        {
            _settings.ThemeMode = value;
            _ = SaveAsync();
            ThemeChanged?.Invoke(value);
        }
    }
    
    public event Action<ThemeMode>? ThemeChanged;
    
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = await File.ReadAllTextAsync(SettingsFile);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }
    
    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(_settings, IndentedJsonOptions);
            await File.WriteAllTextAsync(SettingsFile, json);
        }
        catch
        {
            // Silently fail on save errors
        }
    }
}
