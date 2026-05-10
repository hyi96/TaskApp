using System;
using System.Collections.Generic;
using TaskApp.Models.Tags;

namespace TaskApp.Models.Tasks;

public enum TaskType
{
    Todo,
    Daily,
    Habit
}

public abstract class TaskBase : DomainEntity
{
    protected TaskBase()
    {
        _goldValue = 0.1;
    }

    public DateTimeOffset? LastCompletedDate { get; internal set; }

    public double GoldReward
    {
        get => _goldValue;
        internal set
        {
            var newValue = value < 0 ? 0 : value;
            if (Math.Abs(_goldValue - newValue) > 0.001)
            {
                _goldValue = newValue;
                OnPropertyChanged();
            }
        }
    }

    public abstract TaskType Type { get; }

    public virtual bool IsRewardGoalMet => LastCompletedDate.HasValue;
    
    public virtual void Complete(DateTimeOffset? completedAt = null)
    {
        LastCompletedDate = completedAt ?? DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(LastCompletedDate));
        OnPropertyChanged(nameof(IsRewardGoalMet));
    }

    public virtual void ResetRewardProgress()
    {
        // default: nothing to reset
    }

    public void SetGoldReward(double amount)
    {
        GoldReward = amount < 0 ? 0 : amount;
    }

    public void UpdateTitle(string title)
    {
        Title = title ?? string.Empty;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    public void UpdateTags(IEnumerable<Tag> tags)
    {
        Tags.Clear();
        if (tags != null)
        {
            Tags.AddRange(tags);
        }
    }
}

