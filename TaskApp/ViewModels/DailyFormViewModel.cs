using System;
using System.Collections.Generic;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class DailyFormViewModel : TaskFormViewModel
{
    private readonly DailyTask? _dailyTask;
    private RepeatCadence _cadence = RepeatCadence.Daily;
    private int _streakGoal;

    public RepeatCadence Cadence
    {
        get => _cadence;
        set => SetProperty(ref _cadence, value);
    }

    public int StreakGoal
    {
        get => _streakGoal;
        set => SetProperty(ref _streakGoal, value);
    }

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
            StreakGoal = _dailyTask.StreakGoal;
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
            _dailyTask.SetStreakGoal(StreakGoal);
            SaveTags(_dailyTask);
        }
    }
}
