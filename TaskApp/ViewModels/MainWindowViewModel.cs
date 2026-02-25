using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private const string SortNameAsc = "Name (A-Z)";
    private const string SortNameDesc = "Name (Z-A)";
    private const string SortCreatedNew = "Created time (new to old)";
    private const string SortCreatedOld = "Created time (old to new)";
    private const string SortGoldHigh = "Gold value (high to low)";
    private const string SortGoldLow = "Gold value (low to high)";
    private const string SortHabitCountHigh = "Count (high to low)";
    private const string SortHabitCountLow = "Count (low to high)";
    private const string SortDailyCurrentStreakHigh = "Current streak (high to low)";
    private const string SortDailyCurrentStreakLow = "Current streak (low to high)";
    private const string SortDailyBestStreakHigh = "Best streak (high to low)";
    private const string SortDailyBestStreakLow = "Best streak (low to high)";
    private const string SortDailyDueDateEarly = "Due date (earliest to latest)";
    private const string SortDailyDueDateLate = "Due date (latest to earliest)";
    private const string SortTodoDueDateEarly = "Due date (earliest to latest)";
    private const string SortTodoDueDateLate = "Due date (latest to earliest)";

    private readonly StorageService _storageService;
    private readonly UserService _userService;

    private UserProfile _user = new();
    private string _newHabitTitle = string.Empty;
    private string _newDailyTitle = string.Empty;
    private string _newTodoTitle = string.Empty;
    private string _newRewardTitle = string.Empty;
    private string _searchQuery = string.Empty;
    private string _habitsFilter = "all";
    private string _dailiesFilter = "all";
    private string _todosFilter = "active";
    private string _rewardsFilter = "all";
    private string _habitsSortMode = SortNameAsc;
    private string _dailiesSortMode = SortNameAsc;
    private string _todosSortMode = SortNameAsc;
    private string _rewardsSortMode = SortNameAsc;
    private bool _isVerbose;

    public string Title => "TaskApp";

    public string CurrentUserName => _userService.CurrentUser?.Name ?? "Unknown";

    public void RefreshCurrentUserName()
    {
        OnPropertyChanged(nameof(CurrentUserName));
    }
    
    public bool IsVerbose
    {
        get => _isVerbose;
        set => SetProperty(ref _isVerbose, value);
    }

    public ObservableCollection<HabitTask> Habits { get; } = new();
    public ObservableCollection<DailyTask> Dailies { get; } = new();
    public ObservableCollection<TodoTask> Todos { get; } = new();
    public ObservableCollection<Reward> Rewards { get; } = new();

    public IReadOnlyList<string> HabitsSortOptions { get; } = new[]
    {
        SortNameAsc,
        SortNameDesc,
        SortCreatedNew,
        SortCreatedOld,
        SortGoldHigh,
        SortGoldLow,
        SortHabitCountHigh,
        SortHabitCountLow
    };

    public IReadOnlyList<string> DailiesSortOptions { get; } = new[]
    {
        SortNameAsc,
        SortNameDesc,
        SortCreatedNew,
        SortCreatedOld,
        SortGoldHigh,
        SortGoldLow,
        SortDailyCurrentStreakHigh,
        SortDailyCurrentStreakLow,
        SortDailyBestStreakHigh,
        SortDailyBestStreakLow,
        SortDailyDueDateEarly,
        SortDailyDueDateLate
    };

    public IReadOnlyList<string> TodosSortOptions { get; } = new[]
    {
        SortNameAsc,
        SortNameDesc,
        SortCreatedNew,
        SortCreatedOld,
        SortGoldHigh,
        SortGoldLow,
        SortTodoDueDateEarly,
        SortTodoDueDateLate
    };

    public IReadOnlyList<string> RewardsSortOptions { get; } = new[]
    {
        SortNameAsc,
        SortNameDesc,
        SortCreatedNew,
        SortCreatedOld,
        SortGoldHigh,
        SortGoldLow
    };
    
    public ObservableCollection<SelectableTag> AvailableTags { get; } = new();
 
    public CurrentActivityViewModel CurrentActivity { get; } = new();
 
    public StorageService StorageService => _storageService;
    public UserService UserService => _userService;

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

    public string HabitsFilter
    {
        get => _habitsFilter;
        set
        {
            if (SetProperty(ref _habitsFilter, value))
            {
                RefreshFilter();
            }
        }
    }

    public string HabitsSortMode
    {
        get => _habitsSortMode;
        set
        {
            if (SetProperty(ref _habitsSortMode, value))
            {
                RefreshFilter();
            }
        }
    }

    public string DailiesSortMode
    {
        get => _dailiesSortMode;
        set
        {
            if (SetProperty(ref _dailiesSortMode, value))
            {
                RefreshFilter();
            }
        }
    }

    public string TodosSortMode
    {
        get => _todosSortMode;
        set
        {
            if (SetProperty(ref _todosSortMode, value))
            {
                RefreshFilter();
            }
        }
    }

    public string RewardsSortMode
    {
        get => _rewardsSortMode;
        set
        {
            if (SetProperty(ref _rewardsSortMode, value))
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

    public MainWindowViewModel(StorageService storageService, UserService userService)
    {
        _storageService = storageService;
        _userService = userService;
 
        CurrentActivity.ActivityDurationRecorded += async (duration, title, taskId, rewardId) =>
        {
            await LogActivityDurationAsync(duration, title, taskId, rewardId);
        };

        CurrentActivity.GetAutocompleteRemainingTime = GetAutocompleteRemainingTimeAsync;
        CurrentActivity.AutocompleteTriggered += OnAutocompleteTriggered;
    }

    public void AddHabit()
    {
        if (string.IsNullOrWhiteSpace(NewHabitTitle)) return;
        var habit = new HabitTask();
        habit.UpdateTitle(NewHabitTitle);

        habit.PropertyChanged += OnItemPropertyChanged;
        _allHabits.Add(habit);
        RefreshFilter();

        NewHabitTitle = string.Empty;
    }

    public void AddDaily()
    {
        if (string.IsNullOrWhiteSpace(NewDailyTitle)) return;
        var daily = new DailyTask();
        daily.UpdateTitle(NewDailyTitle);

        daily.PropertyChanged += OnItemPropertyChanged;
        _allDailies.Add(daily);
        RefreshFilter();

        NewDailyTitle = string.Empty;
    }

    public void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;
        var todo = new TodoTask();
        todo.UpdateTitle(NewTodoTitle);

        todo.PropertyChanged += OnItemPropertyChanged;
        _allTodos.Add(todo);
        RefreshFilter();

        NewTodoTitle = string.Empty;
    }

    public void AddReward()
    {
        if (string.IsNullOrWhiteSpace(NewRewardTitle)) return;
        var reward = new Reward(NewRewardTitle);
        reward.PropertyChanged += OnItemPropertyChanged;
        _allRewards.Add(reward);
        RefreshFilter();
        NewRewardTitle = string.Empty;
    }

    public void DeleteHabit(HabitTask habit)
    {
        habit.PropertyChanged -= OnItemPropertyChanged;
        _allHabits.Remove(habit);
        Habits.Remove(habit);
    }
    
    public void DeleteDaily(DailyTask daily)
    {
        daily.PropertyChanged -= OnItemPropertyChanged;
        _allDailies.Remove(daily);
        Dailies.Remove(daily);
    }

    public void DeleteTodo(TodoTask todo)
    {
        todo.PropertyChanged -= OnItemPropertyChanged;
        _allTodos.Remove(todo);
        Todos.Remove(todo);
    }

    public void DeleteReward(Reward reward)
    {
        reward.PropertyChanged -= OnItemPropertyChanged;
        _allRewards.Remove(reward);
        Rewards.Remove(reward);
    }

    public async Task LoadDataAsync()
    {
        User = await _storageService.LoadUserProfileAsync();

        var tags = await _storageService.LoadTagsAsync();
        
        // Unsubscribe from old tags before clearing
        foreach (var selectableTag in AvailableTags)
        {
            selectableTag.SelectionChanged -= RefreshFilter;
        }
        AvailableTags.CollectionChanged -= AvailableTags_CollectionChanged;
        
        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            var selectableTag = new SelectableTag(tag);
            selectableTag.SelectionChanged += RefreshFilter;
            AvailableTags.Add(selectableTag);
        }
        
        AvailableTags.CollectionChanged += AvailableTags_CollectionChanged;

        var tasks = await _storageService.LoadTasksAsync();

        UnsubscribeAllItems();
        _allHabits.Clear();
        _allDailies.Clear();
        _allTodos.Clear();

        foreach (var task in tasks)
        {
            task.PropertyChanged += OnItemPropertyChanged;
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
        
        RefreshTasksForNewDay();
        RefreshFilter();

        var rewards = await _storageService.LoadRewardsAsync();
        foreach (var r in _allRewards)
            r.PropertyChanged -= OnItemPropertyChanged;
        _allRewards.Clear();
        foreach (var r in rewards)
            r.PropertyChanged += OnItemPropertyChanged;
        _allRewards.AddRange(rewards);
        
        RefreshFilter();
    }

    public void RefreshFilter()
    {
        var selectedTagIds = AvailableTags.Where(t => t.IsSelected).Select(t => t.Tag.Id).ToHashSet();
        var searchQuery = SearchQuery ?? string.Empty;
        var hasSearchQuery = !string.IsNullOrWhiteSpace(searchQuery);

        FilterCollection(_allHabits, Habits, selectedTagIds, searchQuery, hasSearchQuery, HabitsFilter, null, items => SortHabits(items, HabitsSortMode));
        FilterDailies(_allDailies, Dailies, selectedTagIds, searchQuery, hasSearchQuery, DailiesFilter, DailiesSortMode);
        FilterTodos(_allTodos, Todos, selectedTagIds, searchQuery, hasSearchQuery, TodosFilter, TodosSortMode);
        FilterRewards(_allRewards, Rewards, selectedTagIds, searchQuery, hasSearchQuery, RewardsFilter, RewardsSortMode);
    }

    private void FilterCollection<T>(
        List<T> source,
        ObservableCollection<T> target,
        HashSet<Guid> selectedTagIds,
        string searchQuery,
        bool hasSearchQuery,
        string columnFilter,
        Func<T, bool>? extraFilter,
        Action<List<T>> sortAction) where T : DomainEntity
    {
        var showHidden = columnFilter == "hidden";
        var filtered = new List<T>(source.Count);
        foreach (var item in source)
        {
            if (item.IsHidden != showHidden)
                continue;

            var isTagMatched = selectedTagIds.Count == 0 || item.Tags.Any(t => selectedTagIds.Contains(t.Id));

            var isTitleMatched = !hasSearchQuery || item.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);

            var isFilterMatched = extraFilter?.Invoke(item) ?? true;

            if (isTagMatched && isTitleMatched && isFilterMatched)
            {
                filtered.Add(item);
            }
        }

        sortAction(filtered);

        UpdateCollection(target, filtered);
    }

    private void FilterDailies(List<DailyTask> source, ObservableCollection<DailyTask> target, HashSet<Guid> selectedTagIds, string searchQuery, bool hasSearchQuery, string filter, string sortMode)
    {
        FilterCollection(
            source,
            target,
            selectedTagIds,
            searchQuery,
            hasSearchQuery,
            filter,
            item => filter switch
            {
                "due" => !item.IsCompleteForCurrentPeriod,
                "not due" => item.IsCompleteForCurrentPeriod,
                "all" => true,
                "hidden" => true,
                _ => true
            },
            items => SortDailies(items, sortMode));
    }

    private void FilterTodos(List<TodoTask> source, ObservableCollection<TodoTask> target, HashSet<Guid> selectedTagIds, string searchQuery, bool hasSearchQuery, string filter, string sortMode)
    {
        FilterCollection(
            source,
            target,
            selectedTagIds,
            searchQuery,
            hasSearchQuery,
            filter,
            item =>
            {
                var isCompleted = item.LastCompletedDate.HasValue;
                var hasScheduledDate = item.DueDate.HasValue;

                return filter switch
                {
                    "active" => !isCompleted,
                    "scheduled" => !isCompleted && hasScheduledDate,
                    "completed" => isCompleted,
                    "hidden" => true,
                    _ => true
                };
            },
            items => SortTodos(items, sortMode));
    }

    private void FilterRewards(List<Reward> source, ObservableCollection<Reward> target, HashSet<Guid> selectedTagIds, string searchQuery, bool hasSearchQuery, string filter, string sortMode)
    {
        FilterCollection(
            source,
            target,
            selectedTagIds,
            searchQuery,
            hasSearchQuery,
            filter,
            item => filter switch
            {
                "one-time" => !item.IsRepeatable,
                "repeatable" => item.IsRepeatable,
                "all" => true,
                "hidden" => true,
                _ => true
            },
            items => SortRewards(items, sortMode));
    }

    private static void SortHabits(List<HabitTask> habits, string sortMode)
    {
        habits.Sort((a, b) => sortMode switch
        {
            SortNameAsc => CompareTitles(a, b),
            SortNameDesc => CompareTitles(b, a),
            SortCreatedNew => CompareWithTitle(b.CreatedAt.CompareTo(a.CreatedAt), a, b),
            SortCreatedOld => CompareWithTitle(a.CreatedAt.CompareTo(b.CreatedAt), a, b),
            SortGoldHigh => CompareWithTitle(b.GoldReward.CompareTo(a.GoldReward), a, b),
            SortGoldLow => CompareWithTitle(a.GoldReward.CompareTo(b.GoldReward), a, b),
            SortHabitCountHigh => CompareWithTitle(b.Count.CompareTo(a.Count), a, b),
            SortHabitCountLow => CompareWithTitle(a.Count.CompareTo(b.Count), a, b),
            _ => CompareTitles(a, b)
        });
    }

    private static void SortDailies(List<DailyTask> dailies, string sortMode)
    {
        dailies.Sort((a, b) => sortMode switch
        {
            SortNameAsc => CompareTitles(a, b),
            SortNameDesc => CompareTitles(b, a),
            SortCreatedNew => CompareWithTitle(b.CreatedAt.CompareTo(a.CreatedAt), a, b),
            SortCreatedOld => CompareWithTitle(a.CreatedAt.CompareTo(b.CreatedAt), a, b),
            SortGoldHigh => CompareWithTitle(b.GoldReward.CompareTo(a.GoldReward), a, b),
            SortGoldLow => CompareWithTitle(a.GoldReward.CompareTo(b.GoldReward), a, b),
            SortDailyCurrentStreakHigh => CompareWithTitle(b.CurrentStreak.CompareTo(a.CurrentStreak), a, b),
            SortDailyCurrentStreakLow => CompareWithTitle(a.CurrentStreak.CompareTo(b.CurrentStreak), a, b),
            SortDailyBestStreakHigh => CompareWithTitle(b.BestStreak.CompareTo(a.BestStreak), a, b),
            SortDailyBestStreakLow => CompareWithTitle(a.BestStreak.CompareTo(b.BestStreak), a, b),
            SortDailyDueDateEarly => CompareWithTitle(a.CurrentPeriodEndDate.CompareTo(b.CurrentPeriodEndDate), a, b),
            SortDailyDueDateLate => CompareWithTitle(b.CurrentPeriodEndDate.CompareTo(a.CurrentPeriodEndDate), a, b),
            _ => CompareTitles(a, b)
        });
    }

    private static void SortTodos(List<TodoTask> todos, string sortMode)
    {
        todos.Sort((a, b) => sortMode switch
        {
            SortNameAsc => CompareTitles(a, b),
            SortNameDesc => CompareTitles(b, a),
            SortCreatedNew => CompareWithTitle(b.CreatedAt.CompareTo(a.CreatedAt), a, b),
            SortCreatedOld => CompareWithTitle(a.CreatedAt.CompareTo(b.CreatedAt), a, b),
            SortGoldHigh => CompareWithTitle(b.GoldReward.CompareTo(a.GoldReward), a, b),
            SortGoldLow => CompareWithTitle(a.GoldReward.CompareTo(b.GoldReward), a, b),
            SortTodoDueDateEarly => CompareTodoDueDates(a, b, true),
            SortTodoDueDateLate => CompareTodoDueDates(a, b, false),
            _ => CompareTitles(a, b)
        });
    }

    private static void SortRewards(List<Reward> rewards, string sortMode)
    {
        rewards.Sort((a, b) => sortMode switch
        {
            SortNameAsc => CompareTitles(a, b),
            SortNameDesc => CompareTitles(b, a),
            SortCreatedNew => CompareWithTitle(b.CreatedAt.CompareTo(a.CreatedAt), a, b),
            SortCreatedOld => CompareWithTitle(a.CreatedAt.CompareTo(b.CreatedAt), a, b),
            SortGoldHigh => CompareWithTitle(b.GoldCost.CompareTo(a.GoldCost), a, b),
            SortGoldLow => CompareWithTitle(a.GoldCost.CompareTo(b.GoldCost), a, b),
            _ => CompareTitles(a, b)
        });
    }

    private static int CompareTitles(DomainEntity left, DomainEntity right)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
    }

    private static int CompareWithTitle(int primary, DomainEntity left, DomainEntity right)
    {
        return primary != 0 ? primary : CompareTitles(left, right);
    }

    private static int CompareTodoDueDates(TodoTask left, TodoTask right, bool ascending)
    {
        var leftHasDate = left.DueDate.HasValue;
        var rightHasDate = right.DueDate.HasValue;

        if (leftHasDate && rightHasDate)
        {
            var comparison = left.DueDate!.Value.CompareTo(right.DueDate!.Value);
            var ordered = ascending ? comparison : -comparison;
            return CompareWithTitle(ordered, left, right);
        }

        if (leftHasDate != rightHasDate)
        {
            return leftHasDate ? -1 : 1;
        }

        return CompareTitles(left, right);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshFilter();
    }

    private void UnsubscribeAllItems()
    {
        foreach (var h in _allHabits)
            h.PropertyChanged -= OnItemPropertyChanged;
        foreach (var d in _allDailies)
            d.PropertyChanged -= OnItemPropertyChanged;
        foreach (var t in _allTodos)
            t.PropertyChanged -= OnItemPropertyChanged;
        foreach (var r in _allRewards)
            r.PropertyChanged -= OnItemPropertyChanged;
    }

    private static void UpdateCollection<T>(ObservableCollection<T> target, List<T> filtered)
    {
        for (var i = 0; i < filtered.Count; i++)
        {
            var item = filtered[i];
            if (i < target.Count && ReferenceEquals(target[i], item))
            {
                continue;
            }

            var existingIndex = target.IndexOf(item);
            if (existingIndex >= 0)
            {
                target.Move(existingIndex, i);
            }
            else
            {
                target.Insert(i, item);
            }
        }

        while (target.Count > filtered.Count)
        {
            target.RemoveAt(target.Count - 1);
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
        await _storageService.SaveTagsAsync(AvailableTags.Select(t => t.Tag).ToList());

        var allTasks = _allHabits.Cast<TaskBase>().Concat(_allDailies).Concat(_allTodos).ToList();
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

    public void SetCurrentActivity(string title, Guid? taskId = null, Guid? rewardId = null)
    {
        CurrentActivity.SetTitleAndReset(title ?? string.Empty, taskId, rewardId);
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
                await LogActivityDurationAsync(sessionElapsed, CurrentActivity.Title, CurrentActivity.TaskId, CurrentActivity.RewardId);
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

    public Task LogDailyCompletedAsync(DailyTask daily, double goldDelta, DateTime timestampUtc)
    {
        return LogAsync(LogType.DailyCompleted, task: daily, goldDelta: goldDelta, timestampUtc: timestampUtc);
    }
 
    public Task LogTodoCompletedAsync(TodoTask todo, double goldDelta)
    {
        return LogAsync(LogType.TodoCompleted, task: todo, goldDelta: goldDelta);
    }
 
    public Task LogRewardClaimedAsync(Reward reward, double goldDelta)
    {
        return LogAsync(LogType.RewardClaimed, reward: reward, goldDelta: -Math.Abs(goldDelta));
    }
 
    public Task LogActivityDurationAsync(TimeSpan duration, string title, Guid? taskId = null, Guid? rewardId = null)
    {
        return LogAsync(LogType.ActivityDuration, duration: duration, title: title, taskId: taskId, rewardId: rewardId);
    }
 
    private Task LogAsync(LogType type, TaskBase? task = null, Reward? reward = null, double goldDelta = 0, double? countDelta = null, TimeSpan? duration = null, string? title = null, Guid? taskId = null, Guid? rewardId = null, DateTime? timestampUtc = null)
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = timestampUtc ?? DateTime.UtcNow,
            Type = type,
            TaskId = taskId ?? task?.Id,
            RewardId = rewardId ?? reward?.Id,
            GoldDelta = goldDelta,
            UserGold = User.Gold, // Capture user's gold after GoldDelta has been applied
            CountDelta = countDelta,
            Duration = duration,
            TitleSnapshot = title ?? task?.Title ?? reward?.Title ?? string.Empty
        };
 
        return _storageService.AddLogEntryAsync(entry);
    }
 
    public void RemoveTagFromAllItems(Guid tagId)
    {
        foreach (var task in _allHabits.Concat<DomainEntity>(_allDailies).Concat(_allTodos))
        {
            task.Tags.RemoveAll(t => t.Id == tagId);
        }

        foreach (var reward in _allRewards)
        {
            reward.Tags.RemoveAll(t => t.Id == tagId);
        }

        RefreshFilter();
        _ = SaveDataAsync();
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

    public void RefreshTasksForNewDay()
    {
        foreach (var daily in _allDailies)
        {
            daily.RefreshForCurrentPeriod();
            daily.NotifyPeriodChanged();
        }

        foreach (var habit in _allHabits)
        {
            habit.RefreshForCurrentPeriod();
        }

        RefreshFilter();
    }

    public List<DailyTask> GetUncompletedDailiesFromYesterday()
    {
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var yesterday = now.AddDays(-1);
        
        return _allDailies
            .Where(d => 
            {
                var currentPeriodStart = d.GetCurrentPeriodStart();
                var yesterdayPeriodStart = d.GetPeriodStartFor(yesterday);
                
                // Only check tasks that are in a NEW period (period changed from yesterday to today)
                if (currentPeriodStart == yesterdayPeriodStart)
                {
                    return false; // Same period, don't show
                }
                
                // Now check if the task was completed in the previous period (yesterday's period)
                var dateInPreviousPeriod = new DateTimeOffset(yesterdayPeriodStart.ToDateTime(new TimeOnly(12, 0)), now.Offset);
                
                // Task should appear if it was NOT completed in the previous period
                return !d.IsCompleteForPeriod(dateInPreviousPeriod);
            })
            .ToList();
    }

    private async Task<TimeSpan?> GetAutocompleteRemainingTimeAsync(Guid taskId, TimeSpan currentSessionElapsed)
    {
        var daily = _allDailies.FirstOrDefault(d => d.Id == taskId);
        if (daily == null || daily.AutocompleteTimeThreshold is not TimeSpan threshold || daily.IsCompleteForCurrentPeriod)
            return null;

        var periodStart = daily.GetCurrentPeriodStart();
        var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var loggedDuration = await _storageService.GetActivityDurationForTaskSinceAsync(taskId, periodStartUtc);
        var totalTimeSpent = loggedDuration + currentSessionElapsed;
        return threshold - totalTimeSpent;
    }

    private async void OnAutocompleteTriggered(Guid taskId)
    {
        var daily = _allDailies.FirstOrDefault(d => d.Id == taskId);
        if (daily == null || daily.IsCompleteForCurrentPeriod)
            return;

        daily.Complete();
        var rewardAmount = daily.GetGoldRewardWithBonus();
        AddGold(rewardAmount);
        RefreshFilter();
        _ = SaveDataAsync();
        await LogDailyCompletedAsync(daily, rewardAmount);
    }
}






