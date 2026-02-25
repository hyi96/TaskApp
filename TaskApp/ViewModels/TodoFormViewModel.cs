using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public class TodoFormViewModel : TaskFormViewModel
{
    private readonly TodoTask? _todoTask;
    private DateTimeOffset? _dueDate;
    private TimeSpan? _dueTime;
    private string _newChecklistItem = string.Empty;

    public DateTimeOffset? DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    public TimeSpan? DueTime
    {
        get => _dueTime;
        set => SetProperty(ref _dueTime, value);
    }

    public string NewChecklistItem
    {
        get => _newChecklistItem;
        set => SetProperty(ref _newChecklistItem, value);
    }

    public ObservableCollection<ChecklistItem> ChecklistItems { get; } = new();

    public TodoFormViewModel(IEnumerable<SelectableTag> availableTags, TodoTask? todoTask = null)
        : base(availableTags, todoTask?.Tags)
    {
        Type = TaskType.Todo;
        _todoTask = todoTask;

        if (_todoTask != null)
        {
            Title = _todoTask.Title;
            Notes = _todoTask.Notes ?? string.Empty;
            GoldValue = _todoTask.GoldReward;
            DueDate = _todoTask.DueDate;
            DueTime = _todoTask.DueDate?.TimeOfDay is { } t && t != TimeSpan.Zero ? t : null;
            LastCompletedDisplay = _todoTask.LastCompletedDate?.ToLocalTime().ToString("g") ?? "Never";

            foreach (var item in _todoTask.Checklist.OrderBy(i => i.IsCompleted))
            {
                item.PropertyChanged += OnChecklistItemPropertyChanged;
                ChecklistItems.Add(item);
            }
        }
    }

    public void AddChecklistItem()
    {
        if (!string.IsNullOrWhiteSpace(NewChecklistItem))
        {
            var item = new ChecklistItem(NewChecklistItem.Trim());
            item.PropertyChanged += OnChecklistItemPropertyChanged;
            var insertIndex = ChecklistItems.Count(i => !i.IsCompleted);
            ChecklistItems.Insert(insertIndex, item);
            NewChecklistItem = string.Empty;
        }
    }

    public void RemoveChecklistItem(ChecklistItem item)
    {
        item.PropertyChanged -= OnChecklistItemPropertyChanged;
        ChecklistItems.Remove(item);
    }

    public override void Save()
    {
        if (_todoTask != null)
        {
            _todoTask.UpdateTitle(Title);
            _todoTask.UpdateNotes(Notes);
            _todoTask.SetGoldReward(GoldValue);
            _todoTask.SetDueDate(CombineDueDateTime());
            SaveTags(_todoTask);

            _todoTask.Checklist.Clear();
            foreach (var item in ChecklistItems)
            {
                _todoTask.Checklist.Add(item);
            }
        }
    }

    public override Guid? GetTaskId() => _todoTask?.Id;
    public override Guid? GetRewardId() => null;

    private void OnChecklistItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChecklistItem.IsCompleted))
            return;

        var sorted = ChecklistItems.OrderBy(i => i.IsCompleted).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var currentIndex = ChecklistItems.IndexOf(sorted[i]);
            if (currentIndex != i)
            {
                ChecklistItems.Move(currentIndex, i);
            }
        }
    }

    private DateTimeOffset? CombineDueDateTime()
    {
        if (!DueDate.HasValue) return null;
        var dateOnly = DueDate.Value.Date;
        var time = DueTime ?? TimeSpan.Zero;
        return new DateTimeOffset(dateOnly + time, DueDate.Value.Offset);
    }
}
