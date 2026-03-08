using System;

namespace TaskApp.Models.Tasks;

public enum HabitResetCadence
{
    Never,
    Daily,
    Weekly,
    Monthly
}

public class HabitTask : TaskBase
{
    private double _count;
    private double _incrementAmount = 1.0;
    private bool _incrementEnabled = true;
    private bool _decrementEnabled;
    private HabitResetCadence _resetCadence = HabitResetCadence.Never;
    private DateOnly? _lastResetPeriod;

    public double Count
    {
        get => _count;
        internal set
        {
            if (Math.Abs(_count - value) > 0.001)
            {
                _count = value;
                OnPropertyChanged();
            }
        }
    }

    public double IncrementAmount
    {
        get => _incrementAmount;
        internal set
        {
            if (Math.Abs(_incrementAmount - value) > 0.001)
            {
                _incrementAmount = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IncrementEnabled
    {
        get => _incrementEnabled;
        internal set
        {
            if (_incrementEnabled != value)
            {
                _incrementEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DecrementEnabled
    {
        get => _decrementEnabled;
        internal set
        {
            if (_decrementEnabled != value)
            {
                _decrementEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public HabitResetCadence ResetCadence
    {
        get => _resetCadence;
        internal set
        {
            if (_resetCadence != value)
            {
                _resetCadence = value;
                OnPropertyChanged();
            }
        }
    }

    public DateOnly? LastResetPeriod
    {
        get => _lastResetPeriod;
        internal set
        {
            if (_lastResetPeriod != value)
            {
                _lastResetPeriod = value;
                OnPropertyChanged();
            }
        }
    }

    public override TaskType Type => TaskType.Habit;

    public override bool IsRewardGoalMet => false;

    public override void Complete(DateTimeOffset? completedAt = null)
    {
        var now = completedAt ?? DateTimeOffset.UtcNow;
        EnsureReset(now);
        IncrementInternal(now);
        LastCompletedDate = now;
    }

    public void Increment()
    {
        var now = DateTimeOffset.UtcNow;
        EnsureReset(now);
        IncrementInternal(now);
    }

    private void IncrementInternal(DateTimeOffset now)
    {
        if (!IncrementEnabled)
        {
            return;
        }

        Count += IncrementAmount;
        LastCompletedDate = now;
    }

    public void Decrement()
    {
        var now = DateTimeOffset.UtcNow;
        EnsureReset(now);
        if (!DecrementEnabled)
        {
            return;
        }

        var newValue = Count - IncrementAmount;
        Count = newValue < 0 ? 0 : newValue;
    }

    public void SetIncrementAmount(double amount)
    {
        IncrementAmount = amount;
    }

    public void SetIncrementEnabled(bool enabled)
    {
        IncrementEnabled = enabled;
    }

    public void SetDecrementEnabled(bool enabled)
    {
        DecrementEnabled = enabled;
    }

    public void SetResetCadence(HabitResetCadence cadence)
    {
        ResetCadence = cadence;
    }

    public void RefreshForCurrentPeriod()
    {
        EnsureReset(DateTimeOffset.UtcNow);
    }

    private void EnsureReset(DateTimeOffset now)
    {
        if (ResetCadence == HabitResetCadence.Never)
        {
            return;
        }

        var periodStart = GetResetPeriodStart(now, ResetCadence);
        if (LastResetPeriod != periodStart)
        {
            Count = 0;
            LastResetPeriod = periodStart;
        }
    }

    private static DateOnly GetResetPeriodStart(DateTimeOffset currentTime, HabitResetCadence cadence)
    {
        var date = DateOnly.FromDateTime(currentTime.ToLocalTime().DateTime);
        return cadence switch
        {
            HabitResetCadence.Daily => date,
            HabitResetCadence.Weekly => date.AddDays(-GetDaysSinceWeekStart(date.DayOfWeek)),
            HabitResetCadence.Monthly => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };
    }

    private static int GetDaysSinceWeekStart(DayOfWeek dayOfWeek)
    {
        // Use Monday as start of week
        return ((int)dayOfWeek + 6) % 7;
    }
}
