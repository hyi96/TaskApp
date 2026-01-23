using System;

namespace TaskApp.Models.Tasks;

public enum RepeatCadence
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public class DailyTask : TaskBase
{
    public RepeatCadence Cadence { get; internal set; } = RepeatCadence.Daily;

    public int CurrentStreak { get; internal set; }

    public int BestStreak { get; internal set; }

    public int StreakGoal { get; internal set; }

    public DateOnly? LastCompletionPeriod { get; internal set; }

    public bool RewardGoalFulfilled { get; internal set; }

    public override TaskType Type => TaskType.Daily;

    public override bool IsRewardGoalMet => RewardGoalFulfilled;

    public override void Complete(DateTimeOffset? completedAt = null)
    {
        var localTime = (completedAt ?? DateTimeOffset.UtcNow).ToLocalTime();
        var periodStart = GetPeriodStart(localTime, Cadence);

        if (LastCompletionPeriod is DateOnly lastPeriod)
        {
            if (periodStart == lastPeriod)
            {
                // already completed this period; do not change streak
            }
            else
            {
                var expectedPrev = GetPreviousPeriodStart(periodStart, Cadence);
                if (lastPeriod == expectedPrev)
                {
                    CurrentStreak++;
                }
                else
                {
                    CurrentStreak = 1;
                }
            }
        }
        else
        {
            CurrentStreak = 1;
        }

        if (CurrentStreak > BestStreak)
        {
            BestStreak = CurrentStreak;
        }

        if (!RewardGoalFulfilled && StreakGoal > 0 && CurrentStreak >= StreakGoal)
        {
            RewardGoalFulfilled = true;
        }

        LastCompletionPeriod = periodStart;
        base.Complete(localTime);
    }

    public void IncrementStreak()
    {
        Complete();
    }

    public void DecrementStreak()
    {
        if (CurrentStreak > 0)
        {
            CurrentStreak--;
        }
    }

    public void ResetStreak()
    {
        CurrentStreak = 0;
    }

    public void SetCadence(RepeatCadence cadence)
    {
        Cadence = cadence;
    }

    public void SetStreakGoal(int goal)
    {
        StreakGoal = goal < 0 ? 0 : goal;
        RewardGoalFulfilled = StreakGoal > 0 && CurrentStreak >= StreakGoal;
    }

    public override void ResetRewardProgress()
    {
        RewardGoalFulfilled = false;
    }

    private static DateOnly GetPeriodStart(DateTimeOffset localTime, RepeatCadence cadence)
    {
        var date = DateOnly.FromDateTime(localTime.DateTime);
        return cadence switch
        {
            RepeatCadence.Daily => date,
            RepeatCadence.Weekly => date.AddDays(-GetDaysSinceWeekStart(localTime.DayOfWeek)),
            RepeatCadence.Monthly => new DateOnly(date.Year, date.Month, 1),
            RepeatCadence.Yearly => new DateOnly(date.Year, 1, 1),
            _ => date
        };
    }

    private static DateOnly GetPreviousPeriodStart(DateOnly currentPeriodStart, RepeatCadence cadence)
    {
        return cadence switch
        {
            RepeatCadence.Daily => currentPeriodStart.AddDays(-1),
            RepeatCadence.Weekly => currentPeriodStart.AddDays(-7),
            RepeatCadence.Monthly => currentPeriodStart.AddMonths(-1),
            RepeatCadence.Yearly => currentPeriodStart.AddYears(-1),
            _ => currentPeriodStart
        };
    }

    private static int GetDaysSinceWeekStart(DayOfWeek dayOfWeek)
    {
        // Use Monday as start of week
        return ((int)dayOfWeek + 6) % 7;
    }
}
