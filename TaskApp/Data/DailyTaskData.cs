using System;
using TaskApp.Models.Tasks;

namespace TaskApp.Data;

public class DailyTaskData : TaskData
{
    public RepeatCadence Cadence { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int StreakGoal { get; set; }
    public DateOnly? LastCompletionPeriod { get; set; }
    public bool RewardGoalFulfilled { get; set; }
}
