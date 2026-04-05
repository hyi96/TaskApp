using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class DailyFormViewModel : TaskFormViewModel
{
    private readonly DailyTask? _dailyTask;
    private RepeatCadence _cadence = RepeatCadence.Daily;
    private int _repeatEvery = 1;
    private int _currentStreak;
    private int _bestStreak;
    private int _newBonusStreakGoal = 1;
    private double _newBonusPercent;
    private double _streakProtectionCost = 1.0;
    private bool _isAutocompleteEnabled;
    private int _autocompleteHours;
    private int _autocompleteMinutes;
    private int _autocompleteSeconds;

    public double StreakProtectionCost
    {
        get => _streakProtectionCost;
        set => SetProperty(ref _streakProtectionCost, value < 0 ? 0 : value);
    }

    public RepeatCadence Cadence
    {
        get => _cadence;
        set => SetProperty(ref _cadence, value);
    }

    public int RepeatEvery
    {
        get => _repeatEvery;
        set => SetProperty(ref _repeatEvery, value < 1 ? 1 : value);
    }

    public int CurrentStreak
    {
        get => _currentStreak;
        set => SetProperty(ref _currentStreak, value < 0 ? 0 : value);
    }

    public int BestStreak
    {
        get => _bestStreak;
        private set => SetProperty(ref _bestStreak, value < 0 ? 0 : value);
    }

    public int NewBonusStreakGoal
    {
        get => _newBonusStreakGoal;
        set => SetProperty(ref _newBonusStreakGoal, value < 1 ? 1 : value);
    }

    public double NewBonusPercent
    {
        get => _newBonusPercent;
        set => SetProperty(ref _newBonusPercent, value < 0 ? 0 : value);
    }

    public bool IsAutocompleteEnabled
    {
        get => _isAutocompleteEnabled;
        set => SetProperty(ref _isAutocompleteEnabled, value);
    }

    public int AutocompleteHours
    {
        get => _autocompleteHours;
        set => SetProperty(ref _autocompleteHours, value < 0 ? 0 : value);
    }

    public int AutocompleteMinutes
    {
        get => _autocompleteMinutes;
        set => SetProperty(ref _autocompleteMinutes, value < 0 ? 0 : value);
    }

    public int AutocompleteSeconds
    {
        get => _autocompleteSeconds;
        set => SetProperty(ref _autocompleteSeconds, value < 0 ? 0 : value);
    }

    public ObservableCollection<StreakBonusRuleViewModel> StreakBonusRules { get; } = new();

    public List<RepeatCadence> CadenceOptions { get; } = Enum.GetValues<RepeatCadence>().ToList();

    public DailyFormViewModel(IEnumerable<SelectableTag> availableTags, DailyTask? dailyTask = null)
        : base(availableTags, dailyTask?.Tags)
    {
        Type = TaskType.Daily;
        _dailyTask = dailyTask;

        if (_dailyTask != null)
        {
            Title = _dailyTask.Title;
            Notes = _dailyTask.Notes ?? string.Empty;
            GoldValue = _dailyTask.GoldReward;
            Cadence = _dailyTask.Cadence;
            RepeatEvery = _dailyTask.RepeatEvery;
            CurrentStreak = _dailyTask.CurrentStreak;
            BestStreak = _dailyTask.BestStreak;
            StreakProtectionCost = _dailyTask.StreakProtectionCost;
            LastCompletedDisplay = _dailyTask.LastCompletedDate?.ToLocalTime().ToString("g") ?? "Never";

            if (_dailyTask.AutocompleteTimeThreshold is TimeSpan threshold)
            {
                IsAutocompleteEnabled = true;
                AutocompleteHours = (int)threshold.TotalHours;
                AutocompleteMinutes = threshold.Minutes;
                AutocompleteSeconds = threshold.Seconds;
            }

            foreach (var rule in _dailyTask.StreakBonusRules.OrderBy(r => r.StreakGoal))
            {
                StreakBonusRules.Add(new StreakBonusRuleViewModel(rule.StreakGoal, rule.BonusPercent));
            }
        }
    }

    public void AddStreakBonusRule()
    {
        if (StreakBonusRules.Any(r => r.StreakGoal == NewBonusStreakGoal))
        {
            return;
        }

        StreakBonusRules.Add(new StreakBonusRuleViewModel(NewBonusStreakGoal, NewBonusPercent));
        SortRules();
        NewBonusPercent = 0;
        NewBonusStreakGoal = 1;
    }

    public void RemoveStreakBonusRule(StreakBonusRuleViewModel rule)
    {
        StreakBonusRules.Remove(rule);
    }

    private void SortRules()
    {
        var sorted = StreakBonusRules.OrderBy(r => r.StreakGoal).ToList();
        StreakBonusRules.Clear();
        foreach (var rule in sorted)
        {
            StreakBonusRules.Add(rule);
        }
    }

    public override void Save()
    {
        if (_dailyTask != null)
        {
            _dailyTask.UpdateTitle(Title);
            _dailyTask.UpdateNotes(Notes);
            _dailyTask.SetGoldReward(GoldValue);
            _dailyTask.SetCadence(Cadence);
            _dailyTask.SetRepeatEvery(RepeatEvery);
            _dailyTask.SetCurrentStreak(CurrentStreak);
            _dailyTask.SetStreakProtectionCost(StreakProtectionCost);

            if (IsAutocompleteEnabled)
            {
                var threshold = new TimeSpan(AutocompleteHours, AutocompleteMinutes, AutocompleteSeconds);
                _dailyTask.SetAutocompleteTimeThreshold(threshold > TimeSpan.Zero ? threshold : null);
            }
            else
            {
                _dailyTask.SetAutocompleteTimeThreshold(null);
            }

            var rules = StreakBonusRules
                .GroupBy(r => r.StreakGoal)
                .Select(g => new StreakBonusRule(g.Key, g.First().BonusPercent))
                .ToList();
            _dailyTask.SetStreakBonusRules(rules);
            SaveTags(_dailyTask);
        }
    }

    public override Guid? GetTaskId() => _dailyTask?.Id;
    public override Guid? GetRewardId() => null;
}

public class StreakBonusRuleViewModel : ViewModelBase
{
    private int _streakGoal;
    private double _bonusPercent;

    public int StreakGoal
    {
        get => _streakGoal;
        set => SetProperty(ref _streakGoal, value < 1 ? 1 : value);
    }

    public double BonusPercent
    {
        get => _bonusPercent;
        set => SetProperty(ref _bonusPercent, value < 0 ? 0 : value);
    }

    public StreakBonusRuleViewModel(int streakGoal, double bonusPercent)
    {
        StreakGoal = streakGoal;
        BonusPercent = bonusPercent;
    }
}
