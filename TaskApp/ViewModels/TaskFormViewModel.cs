using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    public abstract void Save();

    protected void SaveTags(TaskBase task)
    {
        task.Tags = TaskTags.Where(t => t.IsSelected).Select(t => t.Name).ToList();
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

    protected TaskFormViewModel(IEnumerable<SelectableTag> availableTags, IEnumerable<string>? currentTags = null) 
    {
        var currentTagSet = currentTags != null ? new HashSet<string>(currentTags) : new HashSet<string>();
        
        foreach (var tag in availableTags)
        {
            TaskTags.Add(new SelectableTag(tag.Name, currentTagSet.Contains(tag.Name)));
        }
    }
}
