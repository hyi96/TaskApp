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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    _userService = new UserService();
                    _userService.LoadSync();

                    _storageService = new StorageService(_userService);
                    _viewModel = new MainWindowViewModel(_storageService, _userService);

                    _userService.CurrentUserChanged += OnCurrentUserChanged;

                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = _viewModel
                    };

                    desktop.Startup += async (s, e) =>
                    {
                        await SettingsService.Instance.LoadAsync();
                        ApplyTheme(SettingsService.Instance.ThemeMode);
                        await _viewModel.LoadDataAsync();
                        InitializeDayDetection();
                    };

                    var isClosing = false;
                    desktop.MainWindow.Closing += async (s, e) =>
                    {
                        if (!isClosing)
                        {
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
                                desktop.MainWindow.Close();
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
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

            base.OnFrameworkInitializationCompleted();
        }

        private async void OnCurrentUserChanged()
        {
            if (_viewModel == null || _storageService == null) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                _viewModel.RemoveCurrentActivity();
                await _viewModel.SaveDataAsync();

                _storageService.RefreshDataDirectory();
                await _viewModel.LoadDataAsync();
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
            _dayDetectionService.NewDayDetected += async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await HandleNewDay();
                });
            };
            _dayDetectionService.Start();
        }

        private async Task HandleNewDay()
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

        private async Task ShowNewDayWindow(List<TaskApp.Models.Tasks.DailyTask> uncheckedDailies)
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var newDayViewModel = new NewDayViewModel();
            newDayViewModel.SetUncompletedDailies(uncheckedDailies);

            var window = new NewDayWindow
            {
                DataContext = newDayViewModel
            };

            if (desktop.MainWindow != null)
            {
                await window.ShowDialog(desktop.MainWindow);
            }

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