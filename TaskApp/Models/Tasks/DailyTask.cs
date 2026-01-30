using System;
using System.Collections.Generic;
using System.Linq;

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
    private RepeatCadence _cadence = RepeatCadence.Daily;
    private int _repeatEvery = 1;
    private int _currentStreak;
    private int _bestStreak;
    private DateOnly? _lastCompletionPeriod;
    private bool _rewardGoalFulfilled;
    private readonly List<StreakBonusRule> _streakBonusRules = new List<StreakBonusRule> { new(7, 10), new(14, 20), new(30, 30) };

    public RepeatCadence Cadence
    {
        get => _cadence;
        internal set
        {
            if (_cadence != value)
            {
                _cadence = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
            }
        }
    }

    public int RepeatEvery
    {
        get => _repeatEvery;
        internal set
        {
            var newValue = value < 1 ? 1 : value;
            if (_repeatEvery != newValue)
            {
                _repeatEvery = newValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
            }
        }
    }

    public int CurrentStreak
    {
        get => _currentStreak;
        internal set
        {
            if (_currentStreak != value)
            {
                _currentStreak = value;
                OnPropertyChanged();
            }
        }
    }

    public int BestStreak
    {
        get => _bestStreak;
        internal set
        {
            if (_bestStreak != value)
            {
                _bestStreak = value;
                OnPropertyChanged();
            }
        }
    }

    public DateOnly? LastCompletionPeriod
    {
        get => _lastCompletionPeriod;
        internal set
        {
            if (_lastCompletionPeriod != value)
            {
                _lastCompletionPeriod = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
            }
        }
    }

    public bool RewardGoalFulfilled
    {
        get => _rewardGoalFulfilled;
        internal set
        {
            if (_rewardGoalFulfilled != value)
            {
                _rewardGoalFulfilled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleteForCurrentPeriod => IsCompleteForPeriod(DateTimeOffset.UtcNow.ToLocalTime());

    public IReadOnlyList<StreakBonusRule> StreakBonusRules => _streakBonusRules;

    public override TaskType Type => TaskType.Daily;

    public override bool IsRewardGoalMet => RewardGoalFulfilled;

    public double GetGoldRewardWithBonus()
    {
        var bonusPercent = GetCurrentBonusPercent();
        return GoldReward * (1 + bonusPercent / 100.0);
    }

    public void SetStreakBonusRules(IEnumerable<StreakBonusRule> rules)
    {
        _streakBonusRules.Clear();
        if (rules == null) return;

        foreach (var rule in rules)
        {
            if (!_streakBonusRules.Any(r => r.StreakGoal == rule.StreakGoal))
            {
                _streakBonusRules.Add(new StreakBonusRule(rule.StreakGoal, rule.BonusPercent));
            }
        }

        OnPropertyChanged(nameof(StreakBonusRules));
    }

    private double GetCurrentBonusPercent()
    {
        if (_streakBonusRules.Count == 0) return 0;
        return _streakBonusRules
            .Where(r => CurrentStreak >= r.StreakGoal)
            .Select(r => r.BonusPercent)
            .DefaultIfEmpty(0)
            .Max();
    }

    public override void Complete(DateTimeOffset? completedAt = null)
    {
        var localTime = (completedAt ?? DateTimeOffset.UtcNow).ToLocalTime();
        RefreshForCurrentPeriod(localTime);
        var periodStart = GetPeriodStart(localTime, Cadence, RepeatEvery, CreatedAt);

        if (LastCompletionPeriod is DateOnly lastPeriod && lastPeriod == periodStart)
        {
            // already completed this period
            return;
        }

        if (LastCompletionPeriod is DateOnly existingPeriod)
        {
            var expectedPrev = GetPreviousPeriodStart(periodStart, Cadence, RepeatEvery);
            if (existingPeriod == expectedPrev)
            {
                CurrentStreak++;
            }
            else
            {
                CurrentStreak = 1;
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

    public void SetRepeatEvery(int repeatEvery)
    {
        RepeatEvery = repeatEvery;
    }

    public void SetCurrentStreak(int value)
    {
        CurrentStreak = value < 0 ? 0 : value;
        if (CurrentStreak > BestStreak)
        {
            BestStreak = CurrentStreak;
        }
    }

    public override void ResetRewardProgress()
    {
        RewardGoalFulfilled = false;
    }

    public void RefreshForCurrentPeriod(DateTimeOffset? now = null)
    {
        var localTime = (now ?? DateTimeOffset.UtcNow).ToLocalTime();
        var currentPeriod = GetPeriodStart(localTime, Cadence, RepeatEvery, CreatedAt);
        var wasComplete = IsCompleteForPeriod(localTime);

        if (LastCompletionPeriod is DateOnly lastPeriod)
        {
            var expectedPrev = GetPreviousPeriodStart(currentPeriod, Cadence, RepeatEvery);
            if (lastPeriod < expectedPrev)
            {
                CurrentStreak = 0;
                RewardGoalFulfilled = false;
            }
        }
        else
        {
            RewardGoalFulfilled = false;
        }

        var isComplete = IsCompleteForPeriod(localTime);
        if (isComplete != wasComplete)
        {
            OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
        }
    }

    public bool IsCompleteForPeriod(DateTimeOffset localTime)
    {
        return LastCompletionPeriod is DateOnly period && period == GetPeriodStart(localTime, Cadence, RepeatEvery, CreatedAt);
    }

    public DateOnly GetPeriodStart(DateTimeOffset localTime, RepeatCadence cadence, int repeatEvery, DateTimeOffset createdAt)
    {
        var date = DateOnly.FromDateTime(localTime.DateTime);
        var anchor = DateOnly.FromDateTime(createdAt.ToLocalTime().DateTime);
        var interval = repeatEvery < 1 ? 1 : repeatEvery;

        return cadence switch
        {
            RepeatCadence.Daily => anchor.AddDays(((date.DayNumber - anchor.DayNumber) / interval) * interval),
            RepeatCadence.Weekly => GetWeeklyPeriodStart(date, anchor, interval),
            RepeatCadence.Monthly => GetMonthlyPeriodStart(date, anchor, interval),
            RepeatCadence.Yearly => new DateOnly(anchor.Year + ((date.Year - anchor.Year) / interval) * interval, 1, 1),
            _ => date
        };
    }

    public DateOnly GetPeriodStartFor(DateTimeOffset localTime)
    {
        return GetPeriodStart(localTime, Cadence, RepeatEvery, CreatedAt);
    }

    private static DateOnly GetWeeklyPeriodStart(DateOnly currentDate, DateOnly anchor, int interval)
    {
        var currentStart = currentDate.AddDays(-GetDaysSinceWeekStart(currentDate.DayOfWeek));
        var anchorStart = anchor.AddDays(-GetDaysSinceWeekStart(anchor.DayOfWeek));
        var weeks = (currentStart.DayNumber - anchorStart.DayNumber) / 7;
        var periodIndex = weeks / interval * interval;
        return anchorStart.AddDays(periodIndex * 7);
    }

    private static DateOnly GetMonthlyPeriodStart(DateOnly currentDate, DateOnly anchor, int interval)
    {
        var anchorMonthIndex = anchor.Year * 12 + anchor.Month - 1;
        var currentMonthIndex = currentDate.Year * 12 + currentDate.Month - 1;
        var monthsDiff = currentMonthIndex - anchorMonthIndex;
        var periodIndex = monthsDiff / interval * interval;
        var targetMonthIndex = anchorMonthIndex + periodIndex;
        var year = targetMonthIndex / 12;
        var month = targetMonthIndex % 12 + 1;
        return new DateOnly(year, month, 1);
    }

    private static DateOnly GetPreviousPeriodStart(DateOnly currentPeriodStart, RepeatCadence cadence, int repeatEvery)
    {
        var interval = repeatEvery < 1 ? 1 : repeatEvery;
        return cadence switch
        {
            RepeatCadence.Daily => currentPeriodStart.AddDays(-interval),
            RepeatCadence.Weekly => currentPeriodStart.AddDays(-7 * interval),
            RepeatCadence.Monthly => currentPeriodStart.AddMonths(-interval),
            RepeatCadence.Yearly => currentPeriodStart.AddYears(-interval),
            _ => currentPeriodStart
        };
    }

    public DateOnly GetCurrentPeriodStart()
    {
        return GetPeriodStartFor(DateTimeOffset.UtcNow.ToLocalTime());
    }

    public DateOnly GetPreviousPeriodStart()
    {
        var currentPeriod = GetCurrentPeriodStart();
        return GetPreviousPeriodStart(currentPeriod, Cadence, RepeatEvery);
    }

    public void CompleteForPeriod(DateOnly periodStart)
    {
        if (LastCompletionPeriod is DateOnly lastPeriod && lastPeriod == periodStart)
        {
            return;
        }

        if (LastCompletionPeriod is DateOnly existingPeriod)
        {
            var expectedPrev = GetPreviousPeriodStart(periodStart, Cadence, RepeatEvery);
            CurrentStreak = existingPeriod == expectedPrev ? CurrentStreak + 1 : 1;
        }
        else
        {
            CurrentStreak = 1;
        }

        if (CurrentStreak > BestStreak)
        {
            BestStreak = CurrentStreak;
        }

        LastCompletionPeriod = periodStart;
        var completedAt = new DateTimeOffset(periodStart.ToDateTime(new TimeOnly(12, 0)), DateTimeOffset.UtcNow.ToLocalTime().Offset);
        base.Complete(completedAt);
        OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
    }

    public void NotifyPeriodChanged()
    {
        OnPropertyChanged(nameof(IsCompleteForCurrentPeriod));
    }

    private static int GetDaysSinceWeekStart(DayOfWeek dayOfWeek)
    {
        // Use Monday as start of week
        return ((int)dayOfWeek + 6) % 7;
    }
}
