using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;
using TaskApp.Views;

namespace TaskApp
{
    public partial class App : Application
    {
        private DayDetectionService? _dayDetectionService;
        private MainWindowViewModel? _viewModel;
        private UserService? _userService;
        private StorageService? _storageService;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Subscribe to theme changes
            SettingsService.Instance.ThemeChanged += OnThemeChanged;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Console.WriteLine("OnFrameworkInitializationCompleted started");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    Console.WriteLine("Initializing UserService...");
                    _userService = new UserService();
                    // Use synchronous file I/O to avoid deadlocks
                    _userService.LoadSync();
                    Console.WriteLine($"UserService loaded. Current user: {_userService.CurrentUser?.Name}");

                    Console.WriteLine("Creating StorageService...");
                    _storageService = new StorageService(_userService);
                    Console.WriteLine($"StorageService created. Data dir: {_storageService.DataDirectory}");

                    Console.WriteLine("Creating MainWindowViewModel...");
                    _viewModel = new MainWindowViewModel(_storageService, _userService);

                    // Subscribe to user changes to reload data
                    _userService.CurrentUserChanged += OnCurrentUserChanged;

                    Console.WriteLine("Creating MainWindow...");
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = _viewModel
                    };
                    Console.WriteLine("MainWindow created and assigned");

                    desktop.Startup += async (s, e) =>
                    {
                        Console.WriteLine("Startup event - loading data and settings...");
                        await SettingsService.Instance.LoadAsync();
                        ApplyTheme(SettingsService.Instance.ThemeMode);
                        await _viewModel.LoadDataAsync();
                        InitializeDayDetection();
                        Console.WriteLine("Startup complete");
                    };

                    var isClosing = false;
                    desktop.MainWindow.Closing += async (s, e) =>
                    {
                        if (!isClosing)
                        {
                            // Cancel the close to perform async save
                            e.Cancel = true;
                            try
                            {
                                _dayDetectionService?.Stop();
                                await _viewModel.LogCurrentActivityIfRunningAsync();
                                await _viewModel.SaveDataAsync();
                            }
                            finally
                            {
                                isClosing = true;
                                // Re-initiate close
                                desktop.MainWindow.Close();
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    // Show error and create a minimal window so the app doesn't just disappear
                    Console.WriteLine($"Startup error: {ex}");
                    
                    var errorWindow = new Window
                    {
                        Title = "TaskApp - Startup Error",
                        Width = 500,
                        Height = 200,
                        Content = new TextBlock
                        {
                            Text = $"Failed to start application:\n\n{ex.Message}\n\n{ex.StackTrace}",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Thickness(20)
                        }
                    };
                    desktop.MainWindow = errorWindow;
                }
            }

            Console.WriteLine("Calling base.OnFrameworkInitializationCompleted");
            base.OnFrameworkInitializationCompleted();
            Console.WriteLine("OnFrameworkInitializationCompleted finished");
        }

        private async void OnCurrentUserChanged()
        {
            if (_viewModel == null || _storageService == null) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Refresh the storage service data directory
                _storageService.RefreshDataDirectory();

                // Reload all data for the new user
                await _viewModel.LoadDataAsync();

                // Update the displayed user name
                _viewModel.RefreshCurrentUserName();
            });
        }
        
        private void OnThemeChanged(ThemeMode mode)
        {
            Dispatcher.UIThread.Post(() => ApplyTheme(mode));
        }
        
        private void ApplyTheme(ThemeMode mode)
        {
            RequestedThemeVariant = mode switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                ThemeMode.System => ThemeVariant.Default,
                _ => ThemeVariant.Default
            };
        }

        private void InitializeDayDetection()
        {
            if (_viewModel == null) return;

            _dayDetectionService = new DayDetectionService();
            _dayDetectionService.NewDayDetected += async (uncompletedDailies) =>
            {
                // Marshal to UI thread
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await HandleNewDay(uncompletedDailies);
                });
            };
            _dayDetectionService.Start();
        }

        private async Task HandleNewDay(List<Models.Tasks.DailyTask> uncompletedDailies)
        {
            if (_viewModel == null) return;

            // Get unchecked dailies from yesterday BEFORE refreshing
            var uncheckedDailies = _viewModel.GetUncompletedDailiesFromYesterday();

            if (uncheckedDailies.Count > 0)
            {
                // Show the new day window
                await ShowNewDayWindow(uncheckedDailies);
            }

            // Now refresh all daily tasks for the new day
            _viewModel.RefreshDailyTasksForNewDay();

            // Save the changes
            await _viewModel.SaveDataAsync();
        }

        private async Task ShowNewDayWindow(List<Models.Tasks.DailyTask> uncheckedDailies)
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var newDayViewModel = new NewDayViewModel();
            newDayViewModel.SetUncompletedDailies(uncheckedDailies);

            var window = new NewDayWindow
            {
                DataContext = newDayViewModel
            };

            await window.ShowDialog(desktop.MainWindow);

            // Complete the checked dailies
            var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
            foreach (var item in newDayViewModel.UncompletedDailies.Where(x => x.IsChecked))
            {
                if (item.Daily == null)
                {
                    continue;
                }

                var previousPeriodStart = item.Daily.GetPeriodStartFor(yesterday);
                item.Daily.CompleteForPeriod(previousPeriodStart);
            }

            // Save changes
            if (_viewModel != null)
            {
                await _viewModel.SaveDataAsync();
            }
        }
    }
}