using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TaskApp.Models.Tasks;
using TaskApp.ViewModels;

namespace TaskApp.Models.Rewards;

public class Reward : INotifyPropertyChanged
{
    private readonly List<TaskBase> _linkedTasks = new();
    private string _title = string.Empty;
    private string? _notes;
    private double _goldCost;
    private bool _isClaimed;
    private int _claimCount;

    public Guid Id { get; init; } = Guid.NewGuid();

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

    public bool IsClaimed
    {
        get => _isClaimed;
        internal set
        {
            if (_isClaimed != value)
            {
                _isClaimed = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRepeatable { get; internal set; }

    public int ClaimCount
    {
        get => _claimCount;
        internal set
        {
            if (_claimCount != value)
            {
                _claimCount = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTimeOffset? ClaimedAt { get; internal set; }

    public double GoldCost
    {
        get => _goldCost;
        internal set
        {
            if (Math.Abs(_goldCost - value) > 0.001)
            {
                _goldCost = value;
                OnPropertyChanged();
            }
        }
    }

    public System.Collections.Generic.List<string> Tags { get; internal set; } = new();

    public IReadOnlyList<TaskBase> LinkedTasks => _linkedTasks;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Reward(string title, string? notes = null, bool isRepeatable = false, double goldCost = 0)
    {
        UpdateTitle(title);
        UpdateNotes(notes);
        IsRepeatable = isRepeatable;
        GoldCost = goldCost < 0 ? 0 : goldCost;
    }

    public void UpdateTitle(string title)
    {
        Title = title ?? string.Empty;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    public void SetRepeatable(bool isRepeatable)
    {
        IsRepeatable = isRepeatable;
    }

    public void SetGoldCost(double amount)
    {
        GoldCost = amount < 0 ? 0 : amount;
    }

    public void UpdateTags(System.Collections.Generic.IEnumerable<string> tags)
    {
        Tags.Clear();
        if (tags != null)
        {
            Tags.AddRange(tags);
        }
    }

    public void LinkTask(TaskBase task)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (task.Type == TaskType.Habit)
        {
            throw new InvalidOperationException("Habit tasks cannot be linked to rewards.");
        }

        if (_linkedTasks.Any(t => t.Id == task.Id))
        {
            return; // already linked
        }

        _linkedTasks.Add(task);
    }

    public void UnlinkTask(Guid taskId)
    {
        var existing = _linkedTasks.FirstOrDefault(t => t.Id == taskId);
        if (existing != null)
        {
            existing.ResetRewardProgress();
            _linkedTasks.Remove(existing);
        }
    }

    public bool CanClaim(double availableGold)
    {
        if (availableGold < GoldCost)
        {
            return false;
        }

        if (_linkedTasks.Count == 0)
        {
            return IsRepeatable || !IsClaimed;
        }

        if (!IsRepeatable && IsClaimed)
        {
            return false;
        }

        return _linkedTasks.All(t => t.IsRewardGoalMet);
    }

    public bool TryClaim(double availableGold, DateTimeOffset? claimedAt = null)
    {
        if (!CanClaim(availableGold))
        {
            return false;
        }

        IsClaimed = true;
        ClaimedAt = claimedAt ?? DateTimeOffset.UtcNow;
        ClaimCount++;

        foreach (var task in _linkedTasks.ToList())
        {
            task.ResetRewardProgress();
            if (task.Type == TaskType.Todo)
            {
                _linkedTasks.Remove(task);
            }
        }

        if (IsRepeatable)
        {
            IsClaimed = false;
        }

        return true;
    }
}
