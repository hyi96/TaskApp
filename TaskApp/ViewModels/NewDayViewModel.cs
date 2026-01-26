using System;
using System.Collections.ObjectModel;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class NewDayViewModel : ViewModelBase
{
    private bool _isLoading = false;

    public ObservableCollection<DailyChecklistItem> UncompletedDailies { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public void SetUncompletedDailies(System.Collections.Generic.List<DailyTask> dailies)
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
}
