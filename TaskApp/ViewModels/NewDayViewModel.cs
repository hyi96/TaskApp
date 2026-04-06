using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class NewDayViewModel : ViewModelBase
{
    private bool _isLoading;
    private double _userGold;

    public ObservableCollection<DailyChecklistItem> UncompletedDailies { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public double UserGold
    {
        get => _userGold;
        set
        {
            if (SetProperty(ref _userGold, value))
            {
                OnPropertyChanged(nameof(CanAffordProtections));
            }
        }
    }

    public double TotalProtectionCost => UncompletedDailies
        .Where(x => x.IsProtected)
        .Sum(x => x.ProtectionCost);

    public double ProjectedGoldEarned => UncompletedDailies
        .Where(x => x.IsChecked)
        .Sum(x => x.Daily?.GetGoldRewardWithBonus() ?? 0);

    public bool CanAffordProtections => UserGold + ProjectedGoldEarned >= TotalProtectionCost;

    public double ExpectedCost => Math.Max(0, TotalProtectionCost - ProjectedGoldEarned);

    public bool HasExpectedCost => ExpectedCost > 0;

    public void SetUncompletedDailies(List<DailyTask> dailies)
    {
        // Unsubscribe from old items
        foreach (var item in UncompletedDailies)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        UncompletedDailies.Clear();
        foreach (var daily in dailies.OrderBy(d => d.Title))
        {
            var item = new DailyChecklistItem { Daily = daily, IsChecked = false };
            item.PropertyChanged += OnItemPropertyChanged;
            UncompletedDailies.Add(item);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DailyChecklistItem.IsProtected) or nameof(DailyChecklistItem.IsChecked))
        {
            OnPropertyChanged(nameof(TotalProtectionCost));
            OnPropertyChanged(nameof(ProjectedGoldEarned));
            OnPropertyChanged(nameof(CanAffordProtections));
            OnPropertyChanged(nameof(ExpectedCost));
            OnPropertyChanged(nameof(HasExpectedCost));
        }
    }

    public void CheckAll()
    {
        foreach (var item in UncompletedDailies)
        {
            item.IsChecked = true;
        }
    }

    public void UncheckAll()
    {
        foreach (var item in UncompletedDailies)
        {
            item.IsChecked = false;
            item.IsProtected = false;
        }
    }

    public void ProtectAll()
    {
        foreach (var item in UncompletedDailies)
        {
            if (!item.IsChecked)
            {
                item.IsProtected = true;
            }
        }
    }
}

public class DailyChecklistItem : ViewModelBase
{
    private DailyTask? _daily;
    private bool _isChecked;
    private bool _isProtected;

    public DailyTask? Daily
    {
        get => _daily;
        set => SetProperty(ref _daily, value);
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value) && value)
            {
                IsProtected = false;
            }
        }
    }

    public bool IsProtected
    {
        get => _isProtected;
        set
        {
            if (SetProperty(ref _isProtected, value) && value)
            {
                IsChecked = false;
            }
        }
    }

    public int MissedPeriodCount => Daily?.GetMissedPeriodCount() ?? 0;

    public double ProtectionCost => MissedPeriodCount * (Daily?.StreakProtectionCost ?? 1.0);

    public string Title => Daily?.Title ?? string.Empty;

    public string PeriodLabel
    {
        get
        {
            if (Daily == null) return string.Empty;
            var periodStart = Daily.GetPreviousPeriodStart();
            var periodEnd = Daily.GetCurrentPeriodStart().AddDays(-1);

            if (Daily.Cadence == RepeatCadence.Monthly && Daily.RepeatEvery == 1)
                return periodStart.ToString("MMMM yyyy");

            if (Daily.Cadence == RepeatCadence.Yearly && Daily.RepeatEvery == 1)
                return periodStart.Year.ToString();

            if (periodStart == periodEnd)
                return periodStart.ToString("MMM d, yyyy");

            if (periodStart.Year == periodEnd.Year)
                return $"{periodStart:MMM d} \u2013 {periodEnd:MMM d, yyyy}";

            return $"{periodStart:MMM d, yyyy} \u2013 {periodEnd:MMM d, yyyy}";
        }
    }
}
