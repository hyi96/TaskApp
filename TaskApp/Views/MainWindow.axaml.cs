using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.Threading.Tasks;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.ViewModels;
using TaskApp.Views;

namespace TaskApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void OpenGraphWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm)
        {
            var vm = new GraphViewModel(mainVm.StorageService);
            var graphWindow = new GraphWindow
            {
                DataContext = vm
            };
            graphWindow.Show();
            await vm.LoadAsync();
        }
    }

    public void OpenTagsWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm)
        {
            var vm = new TagsViewModel(mainVm.AvailableTags, mainVm);
            var tagsWindow = new TagsWindow
            {
                DataContext = vm
            };
            tagsWindow.ShowDialog(this);
        }
    }

    public async void OpenLogsWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm)
        {
            var vm = new LogsViewModel(mainVm.StorageService);
            await vm.LoadAsync();

            var logsWindow = new LogsWindow
            {
                DataContext = vm
            };

            await logsWindow.ShowDialog(this);
        }
    }

    public void EditTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.DataContext is object item && DataContext is MainWindowViewModel mainVm)
        {
            TaskFormViewModel? vm = null;
            var tags = mainVm.AvailableTags;

            switch (item)
            {
                case HabitTask habit:
                    vm = new HabitFormViewModel(tags, habit);
                    vm.RequestDelete += () => mainVm.DeleteHabit(habit);
                    break;
                case DailyTask daily:
                    vm = new DailyFormViewModel(tags, daily);
                    vm.RequestDelete += () => mainVm.DeleteDaily(daily);
                    break;
                case TodoTask todo:
                    vm = new TodoFormViewModel(tags, todo);
                    vm.RequestDelete += () => mainVm.DeleteTodo(todo);
                    break;
                case Reward reward:
                    vm = new RewardFormViewModel(tags, reward);
                    vm.RequestDelete += () => mainVm.DeleteReward(reward);
                    break;
            }

            if (vm != null)
            {
                vm.RequestSetAsCurrentActivity += (title, taskId, rewardId) => mainVm.SetCurrentActivity(title, taskId, rewardId);

                var taskWindow = new TaskFormWindow
                {
                    DataContext = vm
                };
                taskWindow.ShowDialog(this);
                
                _ = mainVm.SaveDataAsync();
            }
        }
    }

    public void AddHabit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.AddHabit();
        }
    }

    public void AddDaily_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.AddDaily();
        }
    }

    public void AddTodo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.AddTodo();
        }
    }

    public void AddReward_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.AddReward();
        }
    }

    public void ActivityTitle_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            StartActivityButton?.Focus();
            e.Handled = true;
        }
    }

    public void StartActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StartCurrentActivity();
        }
    }

    public void PauseActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PauseCurrentActivity();
        }
    }

    public void ResetActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ResetCurrentActivity();
        }
    }

    public void RemoveActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RemoveCurrentActivity();
        }
    }
 
    public async void IncrementHabit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.DataContext is HabitTask habit && DataContext is MainWindowViewModel mainVm)
        {
            habit.Increment();
            mainVm.AddGold(habit.GoldReward);
            _ = mainVm.SaveDataAsync();

            await mainVm.LogHabitIncrementAsync(habit, habit.GoldReward);
        }
    }

    public async void CompleteTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.DataContext is TaskBase task && DataContext is MainWindowViewModel mainVm)
        {
            if (task is DailyTask dailyTask && dailyTask.IsCompleteForCurrentPeriod)
            {
                return;
            }

            task.Complete();

            double rewardAmount = task.GoldReward;
            if (task is DailyTask completedDaily)
            {
                rewardAmount = completedDaily.GetGoldRewardWithBonus();
            }

            mainVm.AddGold(rewardAmount);
            
            // Refresh filters to update the display immediately
            mainVm.RefreshFilter();
              
            _ = mainVm.SaveDataAsync();

            switch (task)
            {
                case HabitTask habitTask:
                    await mainVm.LogHabitIncrementAsync(habitTask, rewardAmount);
                    break;
                case DailyTask daily:
                    await mainVm.LogDailyCompletedAsync(daily, rewardAmount);
                    break;
                case TodoTask todoTask:
                    await mainVm.LogTodoCompletedAsync(todoTask, rewardAmount);
                    break;
            }
         }
     }

    public void ClaimReward_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.DataContext is Reward reward && DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ClaimReward(reward);
        }
    }

    public void DailiesFilterTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is MainWindowViewModel mainVm)
        {
            var filterValue = button.Tag?.ToString() ?? "all";
            mainVm.DailiesFilter = filterValue;
        }
    }

    public void TodosFilterTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is MainWindowViewModel mainVm)
        {
            var filterValue = button.Tag?.ToString() ?? "active";
            mainVm.TodosFilter = filterValue;
        }
    }

    public void RewardsFilterTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is MainWindowViewModel mainVm)
        {
            var filterValue = button.Tag?.ToString() ?? "all";
            mainVm.RewardsFilter = filterValue;
        }
    }

    public void OpenSettingsWindow_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new SettingsViewModel();
        var settingsWindow = new SettingsWindow
        {
            DataContext = vm
        };
        settingsWindow.ShowDialog(this);
    }
}
