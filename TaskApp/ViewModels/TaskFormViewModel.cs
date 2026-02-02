using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public abstract class TaskFormViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private double _goldValue;
    
    // Selectable tags for this specific task
    public ObservableCollection<SelectableTag> TaskTags { get; } = new();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public double GoldValue
    {
        get => _goldValue;
        set => SetProperty(ref _goldValue, value);
    }
    
    public string SelectedTagsDisplay
    {
        get
        {
            var selected = TaskTags.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            if (selected.Count == 0) return "(none)";
            return string.Join(", ", selected);
        }
    }

    public TaskType Type { get; protected set; }

    public virtual string FormTitle => Type switch
    {
        TaskType.Habit => "Edit Habit",
        TaskType.Daily => "Edit Daily",
        TaskType.Todo => "Edit To-do",
        _ => "Edit Task"
    };

    public event Action? RequestClose;
    public event Action? RequestDelete;
    public event Action<string, Guid?, Guid?>? RequestSetAsCurrentActivity;

    public abstract void Save();
    public abstract Guid? GetTaskId();
    public abstract Guid? GetRewardId();

    protected void SaveTags(TaskBase task)
    {
        task.Tags.Clear();
        task.Tags.AddRange(TaskTags.Where(t => t.IsSelected).Select(t => t.Tag));
    }

    public void SaveTask()
    {
        Save();
        RequestClose?.Invoke();
    }

    public void DeleteTask()
    {
        RequestDelete?.Invoke();
    }

    public void SetAsCurrentActivity()
    {
        RequestSetAsCurrentActivity?.Invoke(Title, GetTaskId(), GetRewardId());
    }

    protected TaskFormViewModel(IEnumerable<SelectableTag> availableTags, IEnumerable<Tag>? currentTags = null) 
    {
        var currentTagIds = currentTags != null ? new HashSet<Guid>(currentTags.Select(t => t.Id)) : new HashSet<Guid>();
        
        foreach (var tag in availableTags)
        {
            var selectableTag = new SelectableTag(tag.Tag, currentTagIds.Contains(tag.Tag.Id));
            selectableTag.PropertyChanged += OnTagSelectionChanged;
            TaskTags.Add(selectableTag);
        }
    }
    
    private void OnTagSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableTag.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedTagsDisplay));
        }
    }
}

