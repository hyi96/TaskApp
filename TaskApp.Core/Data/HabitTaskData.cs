using System;
using TaskApp.Models.Tasks;

namespace TaskApp.Data;

public class HabitTaskData : TaskData
{
    public double Count { get; set; }
    public double IncrementAmount { get; set; }
    public bool IncrementEnabled { get; set; }
    public bool DecrementEnabled { get; set; }
    public HabitResetCadence ResetCadence { get; set; }
    public DateOnly? LastResetPeriod { get; set; }
}
