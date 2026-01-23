using System;
using System.Collections.Generic;
using TaskApp.Models.Tasks;

namespace TaskApp.Data;

public class DailyTaskData : TaskData
{
    public RepeatCadence Cadence { get; set; }
    public int RepeatEvery { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateOnly? LastCompletionPeriod { get; set; }
    public bool RewardGoalFulfilled { get; set; }
    public List<StreakBonusRuleData> StreakBonusRules { get; set; } = new();
}
