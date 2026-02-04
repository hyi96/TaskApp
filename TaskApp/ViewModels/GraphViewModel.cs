using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.ViewModels;

public partial class GraphViewModel : ViewModelBase, IDisposable
{
    private readonly StorageService _storageService;
    private readonly List<LogEntry> _logEntries = new();
    private readonly List<TaskBase> _tasks = new();
    private readonly List<Reward> _rewards = new();
    private readonly Dictionary<Guid, TaskBase> _taskLookup = new();
    private readonly Dictionary<Guid, Reward> _rewardLookup = new();

    private TimeResolution _selectedResolution = TimeResolution.Day;
    private TargetTypeOption? _selectedTargetType;
    private TargetValueOption? _selectedTargetValue;
    private TargetInstanceOption? _selectedTargetInstance;
    private string _searchQuery = string.Empty;
    private SearchResultOption? _selectedSearchResult;
    private bool _isLoaded;
    private bool _disposed;

    public GraphViewModel(StorageService storageService)
    {
        _storageService = storageService;
        TargetTypes = new ObservableCollection<TargetTypeOption>(
            Enum.GetValues<TargetType>().Select(type => new TargetTypeOption(type)));
        TargetValues = new ObservableCollection<TargetValueOption>();
        TargetInstances = new ObservableCollection<TargetInstanceOption>();
        SearchResults = new ObservableCollection<SearchResultOption>();
    }

    public ObservableCollection<TargetTypeOption> TargetTypes { get; }

    public ObservableCollection<TargetValueOption> TargetValues { get; }

    public ObservableCollection<TargetInstanceOption> TargetInstances { get; }

    public ObservableCollection<SearchResultOption> SearchResults { get; }

    public TimeResolution SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (SetProperty(ref _selectedResolution, value))
            {
                RefreshPlotData();
            }
        }
    }

    public TargetTypeOption? SelectedTargetType
    {
        get => _selectedTargetType;
        set
        {
            if (SetProperty(ref _selectedTargetType, value))
            {
                UpdateTargetValues();
                UpdateTargetInstances();
                RefreshPlotData();
            }
        }
    }

    public TargetValueOption? SelectedTargetValue
    {
        get => _selectedTargetValue;
        set
        {
            if (SetProperty(ref _selectedTargetValue, value))
            {
                RefreshPlotData();
            }
        }
    }

    public TargetInstanceOption? SelectedTargetInstance
    {
        get => _selectedTargetInstance;
        set
        {
            if (SetProperty(ref _selectedTargetInstance, value))
            {
                RefreshPlotData();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                UpdateSearchResults();
            }
        }
    }

    public SearchResultOption? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value) && value != null)
            {
                ApplySearchSelection(value);
            }
        }
    }

    public event Action<PlotData>? PlotDataUpdated;

    public async Task LoadAsync()
    {
        _tasks.Clear();
        _tasks.AddRange(await _storageService.LoadTasksAsync());
        _rewards.Clear();
        _rewards.AddRange(await _storageService.LoadRewardsAsync());

        _taskLookup.Clear();
        foreach (var task in _tasks)
        {
            _taskLookup[task.Id] = task;
        }

        _rewardLookup.Clear();
        foreach (var reward in _rewards)
        {
            _rewardLookup[reward.Id] = reward;
        }

        _logEntries.Clear();
        _logEntries.AddRange(await _storageService.LoadAllLogEntriesAsync());

        _isLoaded = true;

        if (SelectedTargetType == null)
        {
            SelectedTargetType = TargetTypes.FirstOrDefault();
        }

        UpdateTargetValues();
        UpdateTargetInstances();
        UpdateSearchResults();
        RefreshPlotData();
    }

    public void SetResolution(TimeResolution resolution)
    {
        SelectedResolution = resolution;
    }

    private void UpdateTargetValues()
    {
        TargetValues.Clear();
        if (SelectedTargetType == null)
        {
            SelectedTargetValue = null;
            return;
        }

        foreach (var option in TargetValueOption.GetOptions(SelectedTargetType.Value))
        {
            TargetValues.Add(option);
        }

        SelectedTargetValue = TargetValues.FirstOrDefault();
    }

    private void UpdateTargetInstances()
    {
        TargetInstances.Clear();
        if (SelectedTargetType == null)
        {
            SelectedTargetInstance = null;
            return;
        }

        foreach (var option in GetInstancesForType(SelectedTargetType.Value))
        {
            TargetInstances.Add(option);
        }

        SelectedTargetInstance = TargetInstances.FirstOrDefault();
    }

    private IEnumerable<TargetInstanceOption> GetInstancesForType(TargetType type)
    {
        return type switch
        {
            TargetType.Gold => new[] { TargetInstanceOption.ForGold() },
            TargetType.Habit => _tasks.OfType<HabitTask>().Select(TargetInstanceOption.ForTask),
            TargetType.Daily => _tasks.OfType<DailyTask>().Select(TargetInstanceOption.ForTask),
            TargetType.Todo => _tasks.OfType<TodoTask>().Select(TargetInstanceOption.ForTask),
            TargetType.Reward => _rewards.Select(TargetInstanceOption.ForReward),
            TargetType.Activity => GetActivityInstances(),
            _ => Array.Empty<TargetInstanceOption>()
        };
    }

    private IEnumerable<TargetInstanceOption> GetActivityInstances()
    {
        return _logEntries
            .Where(IsActivityEntry)
            .Select(entry => entry.TitleSnapshot)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(title => title)
            .Select(TargetInstanceOption.ForActivity);
    }

    private void UpdateSearchResults()
    {
        SearchResults.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SelectedSearchResult = null;
            return;
        }

        var query = SearchQuery.Trim();
        var results = BuildSearchIndex()
            .Where(result => result.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(result => result.Name)
            .Take(10);

        foreach (var result in results)
        {
            SearchResults.Add(result);
        }
    }

    private IEnumerable<SearchResultOption> BuildSearchIndex()
    {
        foreach (var task in _tasks)
        {
            var type = task switch
            {
                HabitTask => TargetType.Habit,
                DailyTask => TargetType.Daily,
                TodoTask => TargetType.Todo,
                _ => TargetType.Activity
            };

            yield return SearchResultOption.ForDomain(type, task.Id, task.Title);
        }

        foreach (var reward in _rewards)
        {
            yield return SearchResultOption.ForDomain(TargetType.Reward, reward.Id, reward.Title);
        }

        foreach (var activity in GetActivityInstances())
        {
            yield return SearchResultOption.ForActivity(activity.ActivityTitle ?? activity.Name);
        }
    }

    private void ApplySearchSelection(SearchResultOption selection)
    {
        SelectedTargetType = TargetTypes.FirstOrDefault(t => t.Value == selection.TargetType);
        SearchQuery = selection.Name;

        var instance = TargetInstances.FirstOrDefault(option => option.Matches(selection));

        if (instance != null)
        {
            SelectedTargetInstance = instance;
        }

        SelectedSearchResult = null;
    }

    private void RefreshPlotData()
    {
        if (!_isLoaded || SelectedTargetType == null || SelectedTargetValue == null || SelectedTargetInstance == null)
        {
            return;
        }

        var buckets = CreateBuckets(SelectedResolution).ToList();
        if (buckets.Count == 0)
        {
            return;
        }

        var values = new double[buckets.Count];
        var labels = buckets.Select(bucket => bucket.Label).ToArray();

        ApplyTargetData(buckets, values, SelectedTargetType.Value, SelectedTargetValue, SelectedTargetInstance);

        var data = new PlotData(values, labels, SelectedTargetType.Value, SelectedTargetValue.DisplayName);
        PlotDataUpdated?.Invoke(data);
    }

    private void ApplyTargetData(List<TimeBucket> buckets, double[] values, TargetType type, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        switch (type)
        {
            case TargetType.Gold:
                ApplyGoldData(buckets, values, valueOption);
                break;
            case TargetType.Habit:
                ApplyHabitData(buckets, values, valueOption, instance);
                break;
            case TargetType.Daily:
                ApplyDailyData(buckets, values, valueOption, instance);
                break;
            case TargetType.Todo:
                ApplyTodoData(buckets, values, valueOption, instance);
                break;
            case TargetType.Reward:
                ApplyRewardData(buckets, values, valueOption, instance);
                break;
            case TargetType.Activity:
                ApplyActivityData(buckets, values, valueOption, instance);
                break;
        }
    }

    private void ApplyGoldData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption)
    {
        double? lastUserGold = null;
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucketEntries = GetEntriesForBucket(buckets[i]);
            if (valueOption.Value == TargetValueKey.GoldDelta)
            {
                values[i] = bucketEntries.Sum(entry => entry.GoldDelta);
            }
            else
            {
                var latestEntry = bucketEntries.OrderBy(entry => entry.Timestamp).LastOrDefault();
                if (latestEntry != null)
                {
                    lastUserGold = latestEntry.UserGold;
                }

                values[i] = lastUserGold ?? double.NaN;
            }
        }
    }

    private void ApplyHabitData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucketEntries = GetEntriesForBucket(buckets[i])
                .Where(entry => entry.TaskId == instance.EntityId)
                .ToList();

            values[i] = valueOption.Value == TargetValueKey.CountDelta
                ? bucketEntries.Where(entry => entry.Type == LogType.HabitIncremented).Sum(entry => entry.CountDelta ?? 0)
                : bucketEntries.Where(entry => entry.Type == LogType.ActivityDuration).Sum(entry => entry.Duration?.TotalMinutes ?? 0);
        }
    }

    private void ApplyDailyData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucketEntries = GetEntriesForBucket(buckets[i])
                .Where(entry => entry.TaskId == instance.EntityId)
                .ToList();

            values[i] = valueOption.Value == TargetValueKey.Completions
                ? bucketEntries.Count(entry => entry.Type == LogType.DailyCompleted)
                : bucketEntries.Where(entry => entry.Type == LogType.ActivityDuration).Sum(entry => entry.Duration?.TotalMinutes ?? 0);
        }
    }

    private void ApplyTodoData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        var todo = instance.EntityId.HasValue && _taskLookup.TryGetValue(instance.EntityId.Value, out var task)
            ? task as TodoTask
            : null;

        if (todo == null)
        {
            return;
        }

        if (valueOption.Value == TargetValueKey.Created)
        {
            var createdBucketIndex = GetBucketIndexForDate(buckets, todo.CreatedAt.UtcDateTime);
            if (createdBucketIndex.HasValue)
            {
                values[createdBucketIndex.Value] = 1;
            }
            return;
        }

        if (valueOption.Value == TargetValueKey.Completed)
        {
            if (todo.LastCompletedDate.HasValue)
            {
                var completedBucketIndex = GetBucketIndexForDate(buckets, todo.LastCompletedDate.Value.UtcDateTime);
                if (completedBucketIndex.HasValue)
                {
                    values[completedBucketIndex.Value] = 1;
                }
            }
            return;
        }

        for (var i = 0; i < buckets.Count; i++)
        {
            var totalMinutes = GetEntriesForBucket(buckets[i])
                .Where(entry => entry.Type == LogType.ActivityDuration && entry.TaskId == todo.Id)
                .Sum(entry => entry.Duration?.TotalMinutes ?? 0);
            values[i] = totalMinutes;
        }
    }

    private void ApplyRewardData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucketEntries = GetEntriesForBucket(buckets[i])
                .Where(entry => entry.RewardId == instance.EntityId)
                .ToList();

            values[i] = valueOption.Value == TargetValueKey.Claims
                ? bucketEntries.Count(entry => entry.Type == LogType.RewardClaimed)
                : bucketEntries.Where(entry => entry.Type == LogType.ActivityDuration).Sum(entry => entry.Duration?.TotalMinutes ?? 0);
        }
    }

    private void ApplyActivityData(List<TimeBucket> buckets, double[] values, TargetValueOption valueOption, TargetInstanceOption instance)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucketEntries = GetEntriesForBucket(buckets[i])
                .Where(entry => IsActivityEntry(entry) && string.Equals(entry.TitleSnapshot, instance.ActivityTitle, StringComparison.OrdinalIgnoreCase))
                .ToList();

            values[i] = bucketEntries.Sum(entry => entry.Duration?.TotalMinutes ?? 0);
        }
    }

    private IEnumerable<LogEntry> GetEntriesForBucket(TimeBucket bucket)
    {
        return _logEntries.Where(entry => entry.Timestamp >= bucket.Start && entry.Timestamp < bucket.End);
    }

    private int? GetBucketIndexForDate(List<TimeBucket> buckets, DateTime date)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            if (date >= buckets[i].Start && date < buckets[i].End)
            {
                return i;
            }
        }

        return null;
    }

    private static IEnumerable<TimeBucket> CreateBuckets(TimeResolution resolution)
    {
        // Use local time for intuitive bucket boundaries, then convert to UTC for matching
        var localNow = DateTime.Now;
        var currentHourLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, DateTimeKind.Local);
        var currentDayLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Local);
        
        return resolution switch
        {
            TimeResolution.Hour => CreateHourBuckets(currentHourLocal, 72),
            TimeResolution.Day => CreateDayBuckets(currentDayLocal, 14),
            TimeResolution.Week => CreateWeekBuckets(localNow, 8),
            TimeResolution.Month => CreateMonthBuckets(localNow, 12),
            TimeResolution.Year => CreateYearBuckets(localNow, 4),
            _ => Array.Empty<TimeBucket>()
        };
    }

    private static IEnumerable<TimeBucket> CreateHourBuckets(DateTime currentHourLocal, int count)
    {
        var startLocal = currentHourLocal.AddHours(-(count - 1));
        for (var i = 0; i < count; i++)
        {
            var bucketStartLocal = startLocal.AddHours(i);
            var bucketEndLocal = bucketStartLocal.AddHours(1);
            var label = bucketStartLocal.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
            yield return new TimeBucket(bucketStartLocal.ToUniversalTime(), bucketEndLocal.ToUniversalTime(), label);
        }
    }

    private static IEnumerable<TimeBucket> CreateDayBuckets(DateTime currentDayLocal, int count)
    {
        var startLocal = currentDayLocal.AddDays(-(count - 1));
        for (var i = 0; i < count; i++)
        {
            var bucketStartLocal = startLocal.AddDays(i);
            var bucketEndLocal = bucketStartLocal.AddDays(1);
            var label = bucketStartLocal.ToString("MM/dd", CultureInfo.InvariantCulture);
            yield return new TimeBucket(bucketStartLocal.ToUniversalTime(), bucketEndLocal.ToUniversalTime(), label);
        }
    }

    private static IEnumerable<TimeBucket> CreateWeekBuckets(DateTime localNow, int count)
    {
        var currentDateLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Local);
        var currentWeekStartLocal = StartOfWeek(currentDateLocal, DayOfWeek.Monday);
        var startLocal = currentWeekStartLocal.AddDays(-7 * (count - 1));
        for (var i = 0; i < count; i++)
        {
            var bucketStartLocal = startLocal.AddDays(7 * i);
            var bucketEndLocal = bucketStartLocal.AddDays(7);
            var label = bucketStartLocal.ToString("MM/dd", CultureInfo.InvariantCulture);
            yield return new TimeBucket(bucketStartLocal.ToUniversalTime(), bucketEndLocal.ToUniversalTime(), label);
        }
    }

    private static IEnumerable<TimeBucket> CreateMonthBuckets(DateTime localNow, int count)
    {
        var currentMonthStartLocal = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var startLocal = currentMonthStartLocal.AddMonths(-(count - 1));
        for (var i = 0; i < count; i++)
        {
            var bucketStartLocal = startLocal.AddMonths(i);
            var bucketEndLocal = bucketStartLocal.AddMonths(1);
            var label = bucketStartLocal.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            yield return new TimeBucket(bucketStartLocal.ToUniversalTime(), bucketEndLocal.ToUniversalTime(), label);
        }
    }

    private static IEnumerable<TimeBucket> CreateYearBuckets(DateTime localNow, int count)
    {
        var currentYearStartLocal = new DateTime(localNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var startLocal = currentYearStartLocal.AddYears(-(count - 1));
        for (var i = 0; i < count; i++)
        {
            var bucketStartLocal = startLocal.AddYears(i);
            var bucketEndLocal = bucketStartLocal.AddYears(1);
            var label = bucketStartLocal.Year.ToString(CultureInfo.InvariantCulture);
            yield return new TimeBucket(bucketStartLocal.ToUniversalTime(), bucketEndLocal.ToUniversalTime(), label);
        }
    }

    private static DateTime StartOfWeek(DateTime date, DayOfWeek startOfWeek)
    {
        var diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        var start = date.AddDays(-diff);
        return date.Kind == DateTimeKind.Local
            ? DateTime.SpecifyKind(start, DateTimeKind.Local)
            : start;
    }

    private bool IsActivityEntry(LogEntry entry)
    {
        var missingTask = entry.TaskId.HasValue && !_taskLookup.ContainsKey(entry.TaskId.Value);
        var missingReward = entry.RewardId.HasValue && !_rewardLookup.ContainsKey(entry.RewardId.Value);

        return (entry.TaskId == null && entry.RewardId == null) || missingTask || missingReward;
    }
}

public enum TimeResolution
{
    Hour,
    Day,
    Week,
    Month,
    Year
}

public enum TargetType
{
    Gold,
    Habit,
    Daily,
    Todo,
    Reward,
    Activity
}

public enum TargetValueKey
{
    GoldDelta,
    UserGold,
    CountDelta,
    TimeSpent,
    Completions,
    Created,
    Completed,
    Claims
}

public record PlotData(double[] Values, string[] Labels, TargetType TargetType, string TargetValueName);

public record TimeBucket(DateTime Start, DateTime End, string Label);

public record TargetTypeOption(TargetType Value)
{
    public string DisplayName => Value.ToString();
}

public record TargetValueOption(TargetValueKey Value, string DisplayName)
{
    public static IEnumerable<TargetValueOption> GetOptions(TargetType type)
    {
        return type switch
        {
            TargetType.Gold => new[]
            {
                new TargetValueOption(TargetValueKey.GoldDelta, "Change (Gold Delta)"),
                new TargetValueOption(TargetValueKey.UserGold, "Balance (User Gold)")
            },
            TargetType.Habit => new[]
            {
                new TargetValueOption(TargetValueKey.CountDelta, "Count Change"),
                new TargetValueOption(TargetValueKey.TimeSpent, "Total Time Spent (minutes)")
            },
            TargetType.Daily => new[]
            {
                new TargetValueOption(TargetValueKey.Completions, "Completions"),
                new TargetValueOption(TargetValueKey.TimeSpent, "Total Time Spent (minutes)")
            },
            TargetType.Todo => new[]
            {
                new TargetValueOption(TargetValueKey.Created, "Created"),
                new TargetValueOption(TargetValueKey.Completed, "Completed"),
                new TargetValueOption(TargetValueKey.TimeSpent, "Total Time Spent (minutes)")
            },
            TargetType.Reward => new[]
            {
                new TargetValueOption(TargetValueKey.Claims, "Claims"),
                new TargetValueOption(TargetValueKey.TimeSpent, "Total Time Spent (minutes)")
            },
            TargetType.Activity => new[]
            {
                new TargetValueOption(TargetValueKey.TimeSpent, "Total Time Spent (minutes)")
            },
            _ => Array.Empty<TargetValueOption>()
        };
    }
}

public record TargetInstanceOption(TargetType Type, Guid? EntityId, string Name, string? ActivityTitle)
{
    public static TargetInstanceOption ForGold()
    {
        return new TargetInstanceOption(TargetType.Gold, null, "All Gold", null);
    }

    public static TargetInstanceOption ForTask(TaskBase task)
    {
        return new TargetInstanceOption(task switch
        {
            HabitTask => TargetType.Habit,
            DailyTask => TargetType.Daily,
            TodoTask => TargetType.Todo,
            _ => TargetType.Activity
        }, task.Id, task.Title, null);
    }

    public static TargetInstanceOption ForReward(Reward reward)
    {
        return new TargetInstanceOption(TargetType.Reward, reward.Id, reward.Title, null);
    }

    public static TargetInstanceOption ForActivity(string title)
    {
        return new TargetInstanceOption(TargetType.Activity, null, title, title);
    }

    public bool Matches(SearchResultOption result)
    {
        return result.TargetType == Type && result.MatchesEntity(EntityId, ActivityTitle);
    }
}

public record SearchResultOption(TargetType TargetType, Guid? EntityId, string? ActivityTitle, string Name)
{
    public static SearchResultOption ForDomain(TargetType type, Guid id, string name)
    {
        return new SearchResultOption(type, id, null, name);
    }

    public static SearchResultOption ForActivity(string title)
    {
        return new SearchResultOption(TargetType.Activity, null, title, title);
    }

    public bool MatchesEntity(Guid? entityId, string? activityTitle)
    {
        if (TargetType == TargetType.Activity)
        {
            return string.Equals(ActivityTitle, activityTitle, StringComparison.OrdinalIgnoreCase);
        }

        return entityId.HasValue && EntityId.HasValue && entityId.Value == EntityId.Value;
    }
}

public partial class GraphViewModel
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Clear collections to release references
            _logEntries.Clear();
            _tasks.Clear();
            _rewards.Clear();
            _taskLookup.Clear();
            _rewardLookup.Clear();
            TargetTypes.Clear();
            TargetValues.Clear();
            TargetInstances.Clear();
            SearchResults.Clear();

            // Clear event handlers
            PlotDataUpdated = null;
        }

        _disposed = true;
    }
}
