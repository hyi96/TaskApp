using Avalonia;
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

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Subscribe to theme changes
            SettingsService.Instance.ThemeChanged += OnThemeChanged;
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            // Load settings in background - don't block startup
            _ = Task.Run(async () =>
            {
                await SettingsService.Instance.LoadAsync();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                    ApplyTheme(SettingsService.Instance.ThemeMode));
            });
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var storageService = new StorageService();
                _viewModel = new MainWindowViewModel(storageService);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = _viewModel
                };

                desktop.Startup += async (s, e) =>
                {
                    await _viewModel.LoadDataAsync();
                    InitializeDayDetection();
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

            base.OnFrameworkInitializationCompleted();
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