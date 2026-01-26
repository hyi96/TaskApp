using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    private UserProfile _user = new();
    private string _newHabitTitle = string.Empty;
    private string _newDailyTitle = string.Empty;
    private string _newTodoTitle = string.Empty;
    private string _newRewardTitle = string.Empty;
    private string _searchQuery = string.Empty;
    private string _dailiesFilter = "all";
    private string _todosFilter = "active";
    private string _rewardsFilter = "all";

    public string Title => "TaskApp";

    public ObservableCollection<HabitTask> Habits { get; } = new();
    public ObservableCollection<DailyTask> Dailies { get; } = new();
    public ObservableCollection<TodoTask> Todos { get; } = new();
    public ObservableCollection<Reward> Rewards { get; } = new();
    
    public ObservableCollection<SelectableTag> AvailableTags { get; } = new();
 
    public CurrentActivityViewModel CurrentActivity { get; } = new();
 
    public StorageService StorageService => _storageService;

    // Internal full lists
    private readonly List<HabitTask> _allHabits = new();
    private readonly List<DailyTask> _allDailies = new();
    private readonly List<TodoTask> _allTodos = new();
    private readonly List<Reward> _allRewards = new();

    public string NewHabitTitle
    {
        get => _newHabitTitle;
        set => SetProperty(ref _newHabitTitle, value);
    }

    public string NewDailyTitle
    {
        get => _newDailyTitle;
        set => SetProperty(ref _newDailyTitle, value);
    }

    public string NewTodoTitle
    {
        get => _newTodoTitle;
        set => SetProperty(ref _newTodoTitle, value);
    }

    public string NewRewardTitle
    {
        get => _newRewardTitle;
        set => SetProperty(ref _newRewardTitle, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilter();
            }
        }
    }

    public string DailiesFilter
    {
        get => _dailiesFilter;
        set
        {
            if (SetProperty(ref _dailiesFilter, value))
            {
                RefreshFilter();
            }
        }
    }

    public string TodosFilter
    {
        get => _todosFilter;
        set
        {
            if (SetProperty(ref _todosFilter, value))
            {
                RefreshFilter();
            }
        }
    }

    public string RewardsFilter
    {
        get => _rewardsFilter;
        set
        {
            if (SetProperty(ref _rewardsFilter, value))
            {
                RefreshFilter();
            }
        }
    }

    public UserProfile User
    {
        get => _user;
        set => SetProperty(ref _user, value);
    }

    public MainWindowViewModel(StorageService storageService)
    {
        _storageService = storageService;
 
        CurrentActivity.ActivityDurationRecorded += async (duration, title) =>
        {
            await LogActivityDurationAsync(duration, title);
        };
    }

    public void AddHabit()
    {
        if (string.IsNullOrWhiteSpace(NewHabitTitle)) return;
        var habit = new HabitTask();
        habit.UpdateTitle(NewHabitTitle);
        
        _allHabits.Add(habit);
        RefreshFilter();
        
        NewHabitTitle = string.Empty;
    }

    public void AddDaily()
    {
        if (string.IsNullOrWhiteSpace(NewDailyTitle)) return;
        var daily = new DailyTask();
        daily.UpdateTitle(NewDailyTitle);
        
        _allDailies.Add(daily);
        RefreshFilter();
        
        NewDailyTitle = string.Empty;
    }

    public void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;
        var todo = new TodoTask();
        todo.UpdateTitle(NewTodoTitle);
        
        _allTodos.Add(todo);
        RefreshFilter();
        
        NewTodoTitle = string.Empty;
    }

    public void AddReward()
    {
        if (string.IsNullOrWhiteSpace(NewRewardTitle)) return;
        var reward = new Reward(NewRewardTitle);
        _allRewards.Add(reward);
        RefreshFilter();
        NewRewardTitle = string.Empty;
    }

    public void DeleteHabit(HabitTask habit)
    {
        _allHabits.Remove(habit);
        Habits.Remove(habit);
    }
    
    public void DeleteDaily(DailyTask daily)
    {
        _allDailies.Remove(daily);
        Dailies.Remove(daily);
    }

    public void DeleteTodo(TodoTask todo)
    {
        _allTodos.Remove(todo);
        Todos.Remove(todo);
    }

    public void DeleteReward(Reward reward)
    {
        _allRewards.Remove(reward);
        Rewards.Remove(reward);
    }

    public async Task LoadDataAsync()
    {
        User = await _storageService.LoadUserProfileAsync();

        var tags = await _storageService.LoadTagsAsync();
        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            var selectableTag = new SelectableTag(tag);
            selectableTag.SelectionChanged += RefreshFilter;
            AvailableTags.Add(selectableTag);
        }
        
        AvailableTags.CollectionChanged += AvailableTags_CollectionChanged;

        var tasks = await _storageService.LoadTasksAsync();

        _allHabits.Clear();
        _allDailies.Clear();
        _allTodos.Clear();

        foreach (var task in tasks)
        {
            switch (task)
            {
                case HabitTask h:
                    _allHabits.Add(h);
                    break;
                case DailyTask d:
                    _allDailies.Add(d);
                    break;
                case TodoTask t:
                    _allTodos.Add(t);
                    break;
            }
        }
        
        RefreshDailyTasksForNewDay();
        RefreshFilter();

        var rewards = await _storageService.LoadRewardsAsync();
        _allRewards.Clear();
        _allRewards.AddRange(rewards);
        
        RefreshFilter();
    }

    public void RefreshFilter()
    {
        var selectedTags = AvailableTags.Where(t => t.IsSelected).Select(t => t.Name).ToHashSet();
        var searchQuery = SearchQuery?.ToLowerInvariant() ?? string.Empty;
        
        FilterCollection(_allHabits, Habits, selectedTags, searchQuery);
        FilterDailies(_allDailies, Dailies, selectedTags, searchQuery, DailiesFilter);
        FilterTodos(_allTodos, Todos, selectedTags, searchQuery, TodosFilter);
        FilterRewards(_allRewards, Rewards, selectedTags, searchQuery, RewardsFilter);
    }

    private void FilterCollection<T>(List<T> source, ObservableCollection<T> target, HashSet<string> selectedTags, string searchQuery) where T : TaskBase
    {
        target.Clear();
        foreach (var item in source)
        {
            var isTagMatched = selectedTags.Count == 0 || item.Tags.Any(t => selectedTags.Contains(t));
            var isTitleMatched = string.IsNullOrEmpty(searchQuery) || item.Title.ToLowerInvariant().Contains(searchQuery);
            
            if (isTagMatched && isTitleMatched)
            {
                target.Add(item);
            }
        }
    }

    private void FilterDailies(List<DailyTask> source, ObservableCollection<DailyTask> target, HashSet<string> selectedTags, string searchQuery, string filter)
    {
        target.Clear();
        foreach (var item in source)
        {
            var isTagMatched = selectedTags.Count == 0 || item.Tags.Any(t => selectedTags.Contains(t));
            var isTitleMatched = string.IsNullOrEmpty(searchQuery) || item.Title.ToLowerInvariant().Contains(searchQuery);
            
            var isFilterMatched = filter switch
            {
                "due" => !item.IsCompleteForCurrentPeriod,
                "not due" => item.IsCompleteForCurrentPeriod,
                "all" => true,
                _ => true
            };
            
            if (isTagMatched && isTitleMatched && isFilterMatched)
            {
                target.Add(item);
            }
        }
    }

    private void FilterTodos(List<TodoTask> source, ObservableCollection<TodoTask> target, HashSet<string> selectedTags, string searchQuery, string filter)
    {
        target.Clear();
        foreach (var item in source)
        {
            var isTagMatched = selectedTags.Count == 0 || item.Tags.Any(t => selectedTags.Contains(t));
            var isTitleMatched = string.IsNullOrEmpty(searchQuery) || item.Title.ToLowerInvariant().Contains(searchQuery);
            
            var isCompleted = item.LastCompletedDate.HasValue;
            var hasScheduledDate = item.DueDate.HasValue;
            
            var isFilterMatched = filter switch
            {
                "active" => !isCompleted,
                "scheduled" => !isCompleted && hasScheduledDate,
                "completed" => isCompleted,
                _ => true
            };
            
            if (isTagMatched && isTitleMatched && isFilterMatched)
            {
                target.Add(item);
            }
        }
    }

    private void FilterRewards(List<Reward> source, ObservableCollection<Reward> target, HashSet<string> selectedTags, string searchQuery, string filter)
    {
        target.Clear();
        foreach (var item in source)
        {
            var isTagMatched = selectedTags.Count == 0 || item.Tags.Any(t => selectedTags.Contains(t));
            var isTitleMatched = string.IsNullOrEmpty(searchQuery) || item.Title.ToLowerInvariant().Contains(searchQuery);
            
            var isFilterMatched = filter switch
            {
                "one-time" => !item.IsRepeatable,
                "repeatable" => item.IsRepeatable,
                "all" => true,
                _ => true
            };
            
            if (isTagMatched && isTitleMatched && isFilterMatched)
            {
                target.Add(item);
            }
        }
    }

    public void ClaimReward(Reward reward)
    {
        if (reward.TryClaim(User.Gold))
        {
            RemoveGold(reward.GoldCost);
            
            // If reward should be hidden after claim, remove it from the visible collection
            if (!reward.IsRepeatable && reward.IsClaimed)
            {
                Rewards.Remove(reward);
            }
            
            _ = SaveDataAsync();
 
            _ = LogRewardClaimedAsync(reward, reward.GoldCost);
        }
    }

    public async Task SaveDataAsync()
    {
        await _storageService.SaveUserProfileAsync(User);
        await _storageService.SaveTagsAsync(AvailableTags.Select(t => t.Name));

        var allTasks = new List<TaskBase>();
        allTasks.AddRange(_allHabits);
        allTasks.AddRange(_allDailies);
        allTasks.AddRange(_allTodos);

        await _storageService.SaveTasksAsync(allTasks);
        await _storageService.SaveRewardsAsync(_allRewards);
    }

    public void AddGold(double amount)
    {
        User.Gold += amount;
    }

    public void RemoveGold(double amount)
    {
        if (User.Gold >= amount)
        {
            User.Gold -= amount;
        }
    }

    public void SetCurrentActivity(string title)
    {
        CurrentActivity.SetTitleAndReset(title ?? string.Empty);
    }

    public void StartCurrentActivity()
    {
        CurrentActivity.Start();
    }

    public void PauseCurrentActivity()
    {
        CurrentActivity.Pause();
    }

    public void ResetCurrentActivity()
    {
        CurrentActivity.Reset();
    }

    public void RemoveCurrentActivity()
    {
        CurrentActivity.Remove();
    }

    public async Task LogCurrentActivityIfRunningAsync()
    {
        if (CurrentActivity.IsRunning)
        {
            var sessionElapsed = CurrentActivity.Elapsed - CurrentActivity.GetSessionStartElapsed();
            if (!string.IsNullOrWhiteSpace(CurrentActivity.Title) && sessionElapsed > TimeSpan.Zero)
            {
                await LogActivityDurationAsync(sessionElapsed, CurrentActivity.Title);
            }
            CurrentActivity.StopWithoutLogging();
        }
    }
 
    public Task<List<LogEntry>> LoadRecentLogsAsync(int count = 50)
    {
        return _storageService.LoadRecentLogEntriesAsync(count);
    }
 
    public Task LogHabitIncrementAsync(HabitTask habit, double goldDelta)
    {
        return LogAsync(LogType.HabitIncremented, task: habit, goldDelta: goldDelta, countDelta: habit.IncrementAmount);
    }
 
    public Task LogDailyCompletedAsync(DailyTask daily, double goldDelta)
    {
        return LogAsync(LogType.DailyCompleted, task: daily, goldDelta: goldDelta);
    }
 
    public Task LogTodoCompletedAsync(TodoTask todo, double goldDelta)
    {
        return LogAsync(LogType.TodoCompleted, task: todo, goldDelta: goldDelta);
    }
 
    public Task LogRewardClaimedAsync(Reward reward, double goldDelta)
    {
        return LogAsync(LogType.RewardClaimed, reward: reward, goldDelta: -Math.Abs(goldDelta));
    }
 
    public Task LogActivityDurationAsync(TimeSpan duration, string title)
    {
        return LogAsync(LogType.ActivityDuration, duration: duration, title: title);
    }
 
    private Task LogAsync(LogType type, TaskBase? task = null, Reward? reward = null, double goldDelta = 0, double? countDelta = null, TimeSpan? duration = null, string? title = null)
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Type = type,
            TaskId = task?.Id,
            RewardId = reward?.Id,
            GoldDelta = goldDelta,
            CountDelta = countDelta,
            Duration = duration,
            TitleSnapshot = title ?? task?.Title ?? reward?.Title ?? string.Empty
        };
 
        return _storageService.AddLogEntryAsync(entry);
    }
 
     private void AvailableTags_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
     {
         if (e.NewItems != null)
         {
             foreach (SelectableTag tag in e.NewItems)
             {
                 tag.SelectionChanged += RefreshFilter;
             }
         }
         
         if (e.OldItems != null)
         {
             foreach (SelectableTag tag in e.OldItems)
             {
                 tag.SelectionChanged -= RefreshFilter;
             }
         }
         
         RefreshFilter();
     }

    public void RemoveTagFromAllTasks(string tagName)
    {
        foreach (var habit in _allHabits)
        {
            habit.Tags.Remove(tagName);
        }
        
        foreach (var daily in _allDailies)
        {
            daily.Tags.Remove(tagName);
        }
        
        foreach (var todo in _allTodos)
        {
            todo.Tags.Remove(tagName);
        }
        
        foreach (var reward in _allRewards)
        {
            reward.Tags.Remove(tagName);
        }
        
        _ = SaveDataAsync();
    }

    public void RefreshDailyTasksForNewDay()
    {
        foreach (var daily in _allDailies)
        {
            daily.RefreshForCurrentPeriod();
        }
    }

    public List<DailyTask> GetUncompletedDailiesFromYesterday()
    {
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        return _allDailies
            .Where(d => !d.IsCompleteForPeriod(yesterday))
            .ToList();
    }
}


