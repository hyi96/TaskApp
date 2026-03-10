using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;

namespace TaskApp.ViewModels;

public abstract class TaskFormViewModel : ViewModelBase, IDisposable
{
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private double _goldValue;
    private bool _disposed;
    private int _manualHours;
    private int _manualMinutes;
    private int _manualSeconds;
    private string _manualDurationStatus = string.Empty;
    
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

    private string _lastCompletedDisplay = string.Empty;
    public string LastCompletedDisplay
    {
        get => _lastCompletedDisplay;
        protected set => SetProperty(ref _lastCompletedDisplay, value);
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

    // Manual activity duration logging
    public event Action<TimeSpan, string, Guid?, Guid?>? RequestLogManualDuration;

    public int ManualHours
    {
        get => _manualHours;
        set => SetProperty(ref _manualHours, value);
    }

    public int ManualMinutes
    {
        get => _manualMinutes;
        set => SetProperty(ref _manualMinutes, value);
    }

    public int ManualSeconds
    {
        get => _manualSeconds;
        set => SetProperty(ref _manualSeconds, value);
    }

    public string ManualDurationStatus
    {
        get => _manualDurationStatus;
        set => SetProperty(ref _manualDurationStatus, value);
    }

    public void LogManualDuration()
    {
        var duration = new TimeSpan(ManualHours, ManualMinutes, ManualSeconds);
        if (duration <= TimeSpan.Zero)
        {
            ManualDurationStatus = "Enter a duration greater than zero.";
            return;
        }

        RequestLogManualDuration?.Invoke(duration, Title, GetTaskId(), GetRewardId());
        ManualDurationStatus = $"Logged {duration:hh\\:mm\\:ss} successfully.";
        ManualHours = 0;
        ManualMinutes = 0;
        ManualSeconds = 0;
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from tag property changes to prevent memory leaks
            foreach (var tag in TaskTags)
            {
                tag.PropertyChanged -= OnTagSelectionChanged;
            }
            TaskTags.Clear();

            // Clear event handlers
            RequestClose = null;
            RequestDelete = null;
            RequestSetAsCurrentActivity = null;
            RequestLogManualDuration = null;
        }

        _disposed = true;
    }
}

