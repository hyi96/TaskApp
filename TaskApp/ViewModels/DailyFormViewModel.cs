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
            var rules = StreakBonusRules
                .GroupBy(r => r.StreakGoal)
                .Select(g => new StreakBonusRule(g.Key, g.First().BonusPercent))
                .ToList();
            _dailyTask.SetStreakBonusRules(rules);
            SaveTags(_dailyTask);
        }
    }
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
