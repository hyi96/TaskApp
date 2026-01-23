using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.Models.Tasks;

public enum TaskType
{
    Todo,
    Daily,
    Habit
}

public abstract class TaskBase : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string? _notes;
    private double _goldReward = 0.1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Title
    {
        get => _title;
        internal set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        internal set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged();
            }
        }
    }

    public System.Collections.Generic.List<string> Tags { get; internal set; } = new();

    public DateTimeOffset? LastCompletedDate { get; internal set; }

    public double GoldReward
    {
        get => _goldReward;
        internal set
        {
            _goldReward = value;
            OnPropertyChanged();
        }
    }

    public abstract TaskType Type { get; }

    public virtual bool IsRewardGoalMet => LastCompletedDate.HasValue;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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

    public void UpdateTags(System.Collections.Generic.IEnumerable<string> tags)
    {
        Tags.Clear();
        if (tags != null)
        {
            Tags.AddRange(tags);
        }
    }
}
