using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class NewDayViewModel : ViewModelBase
{
    private bool _isLoading;

    public ObservableCollection<DailyChecklistItem> UncompletedDailies { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public void SetUncompletedDailies(List<DailyTask> dailies)
    {
        UncompletedDailies.Clear();
        foreach (var daily in dailies.OrderBy(d => d.Title))
        {
            UncompletedDailies.Add(new DailyChecklistItem { Daily = daily, IsChecked = false });
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
        }
    }
}

public class DailyChecklistItem : ViewModelBase
{
    private DailyTask? _daily;
    private bool _isChecked;

    public DailyTask? Daily
    {
        get => _daily;
        set => SetProperty(ref _daily, value);
    }

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

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
