using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.Models.Rewards;

public class Reward : INotifyPropertyChanged
{
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

    public bool CanClaim(double availableGold)
    {
        if (availableGold < GoldCost)
        {
            return false;
        }

        if (!IsRepeatable && IsClaimed)
        {
            return false;
        }

        return true;
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

        if (IsRepeatable)
        {
            IsClaimed = false;
        }

        return true;
    }
}
