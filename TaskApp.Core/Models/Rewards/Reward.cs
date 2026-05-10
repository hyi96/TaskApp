using System;
using System.Collections.Generic;
using TaskApp.Models.Tags;

namespace TaskApp.Models.Rewards;

public class Reward : DomainEntity
{
    private bool _isClaimed;
    private int _claimCount;

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

    public void UpdateTags(IEnumerable<Tag> tags)
    {
        Tags.Clear();
        if (tags != null)
        {
            Tags.AddRange(tags);
        }
    }

    public bool CanClaim(double availableGold)
    {
        return availableGold >= GoldCost && (IsRepeatable || !IsClaimed);
    }

    public bool TryClaim(double availableGold, DateTimeOffset? claimedAt = null)
    {
        if (!CanClaim(availableGold))
        {
            return false;
        }

        ClaimedAt = claimedAt ?? DateTimeOffset.UtcNow;
        ClaimCount++;

        if (!IsRepeatable)
        {
            IsClaimed = true;
        }

        return true;
    }
}
