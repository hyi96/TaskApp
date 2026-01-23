using System;

namespace TaskApp.Models.Tasks;

public class HabitTask : TaskBase
{
    private double _count;
    private double _incrementAmount = 1.0;
    private bool _incrementEnabled = true;
    private bool _decrementEnabled = true;

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

    public override TaskType Type => TaskType.Habit;

    public override bool IsRewardGoalMet => false;

    public override void Complete(DateTimeOffset? completedAt = null)
    {
        Increment();
        LastCompletedDate = completedAt ?? DateTimeOffset.UtcNow;
    }

    public void Increment()
    {
        if (!IncrementEnabled)
        {
            return;
        }

        Count += IncrementAmount;
    }

    public void Decrement()
    {
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
}
