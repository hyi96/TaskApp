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
    public string CloudApiUrl { get; set; } = "https://taskapp-api.hyi96.dev";
    public string CloudAccountId { get; set; } = string.Empty;
    public string CloudApiKey { get; set; } = string.Empty;
    public string CloudAccountSecret { get; set; } = string.Empty;
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

    public string CloudApiUrl
    {
        get => _settings.CloudApiUrl;
        set
        {
            _settings.CloudApiUrl = value;
            _ = SaveAsync();
        }
    }

    public string CloudAccountId
    {
        get => _settings.CloudAccountId;
        set
        {
            _settings.CloudAccountId = value;
            _ = SaveAsync();
        }
    }

    public string CloudApiKey
    {
        get => _settings.CloudApiKey;
        set
        {
            _settings.CloudApiKey = value;
            _ = SaveAsync();
        }
    }

    public string CloudAccountSecret
    {
        get => _settings.CloudAccountSecret;
        set
        {
            _settings.CloudAccountSecret = value;
            _ = SaveAsync();
        }
    }
    
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
            var tempPath = SettingsFile + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, SettingsFile, overwrite: true);
        }
        catch
        {
            // Silently fail on save errors
        }
    }
}
