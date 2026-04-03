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
        private bool _emergencySaved;

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

                        // Handle new day BEFORE refreshing tasks so streaks are preserved
                        var today = DateOnly.FromDateTime(DateTime.Now);
                        if (_viewModel.User.LastActiveDate.HasValue && _viewModel.User.LastActiveDate.Value < today)
                        {
                            await HandleNewDay();
                        }
                        else
                        {
                            _viewModel.RefreshTasksForNewDay();
                        }

                        InitializeDayDetection();
                    };

                    desktop.ShutdownRequested += (s, e) =>
                    {
                        PerformEmergencySave();
                    };

                    AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                    {
                        PerformEmergencySave();
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
                                _emergencySaved = true;
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

                // Show the new day window if the profile was last active before today.
                // HandleNewDay must run BEFORE RefreshTasksForNewDay so streaks are preserved.
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_viewModel.User.LastActiveDate.HasValue && _viewModel.User.LastActiveDate.Value < today)
                {
                    await HandleNewDay();
                }
                else
                {
                    _viewModel.RefreshTasksForNewDay();
                }
            });
        }
        
        private void OnThemeChanged(ThemeMode mode)
        {
            Dispatcher.UIThread.Post(() => ApplyTheme(mode));
        }

        private void PerformEmergencySave()
        {
            if (_emergencySaved || _viewModel == null) return;
            _emergencySaved = true;

            _dayDetectionService?.Stop();
            _viewModel.EmergencySaveSync();
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

            // Get unchecked dailies since last active date BEFORE refreshing
            var lastActiveDate = _viewModel.User.LastActiveDate
                ?? DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
            var uncheckedDailies = _viewModel.GetUncompletedDailiesSinceLastActive(lastActiveDate);

            if (uncheckedDailies.Count > 0)
            {
                // Show the new day window
                await ShowNewDayWindow(uncheckedDailies);
            }

            // Now refresh all tasks for the new day
            _viewModel.RefreshTasksForNewDay();

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
            foreach (var item in newDayViewModel.UncompletedDailies.Where(x => x.IsChecked))
            {
                if (item.Daily == null)
                {
                    continue;
                }

                var previousPeriodStart = item.Daily.GetPreviousPeriodStart();
                item.Daily.CompleteForPeriod(previousPeriodStart);

                // Log with timestamp at the last minute of the previous period
                var currentPeriodStart = item.Daily.GetCurrentPeriodStart();
                var endOfPreviousPeriod = currentPeriodStart.ToDateTime(new TimeOnly(0, 0)).AddMinutes(-1);
                var localOffset = DateTimeOffset.Now.Offset;
                var endOfPreviousPeriodOffset = new DateTimeOffset(endOfPreviousPeriod, localOffset);
                var goldReward = item.Daily.GetGoldRewardWithBonus();
                _viewModel?.AddGold(goldReward);
                await _viewModel!.LogDailyCompletedAsync(item.Daily, goldReward, endOfPreviousPeriodOffset);
            }

            // Save changes
            if (_viewModel != null)
            {
                await _viewModel.SaveDataAsync();
            }
        }
    }
}