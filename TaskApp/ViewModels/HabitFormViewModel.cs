using System;
using System.Collections.Generic;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class HabitFormViewModel : TaskFormViewModel
{
    private readonly HabitTask? _habitTask;
    private double _incrementAmount = 1.0;
    private double _count;
    private HabitResetCadence _resetCadence = HabitResetCadence.Never;

    public double Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }
    
    public double IncrementAmount
    {
        get => _incrementAmount;
        set => SetProperty(ref _incrementAmount, value);
    }

    public HabitResetCadence ResetCadence
    {
        get => _resetCadence;
        set => SetProperty(ref _resetCadence, value);
    }

    public HabitResetCadence[] ResetCadenceOptions { get; } = Enum.GetValues<HabitResetCadence>();

    public HabitFormViewModel(IEnumerable<SelectableTag> availableTags, HabitTask? habitTask = null)
        : base(availableTags, habitTask?.Tags)
    {
        Type = TaskType.Habit;
        _habitTask = habitTask;

        if (_habitTask != null)
        {
            Title = _habitTask.Title;
            Notes = _habitTask.Notes ?? string.Empty;
            GoldValue = _habitTask.GoldReward;
            IncrementAmount = _habitTask.IncrementAmount;
            Count = _habitTask.Count;
            ResetCadence = _habitTask.ResetCadence;
        }
    }

    public override void Save()
    {
        if (_habitTask != null)
        {
            _habitTask.UpdateTitle(Title);
            _habitTask.UpdateNotes(Notes);
            _habitTask.SetGoldReward(GoldValue);
            _habitTask.SetIncrementAmount(IncrementAmount);
            _habitTask.SetResetCadence(ResetCadence);
            _habitTask.Count = Count;
            SaveTags(_habitTask);
        }
    }

    public override Guid? GetTaskId() => _habitTask?.Id;
    public override Guid? GetRewardId() => null;
}
