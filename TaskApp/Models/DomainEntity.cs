using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskApp.Models.Tags;

namespace TaskApp.Models;

public abstract class DomainEntity : INotifyPropertyChanged
{
    protected string _title = string.Empty;
    protected string? _notes;
    protected double _goldValue;
    protected List<Tag> _tags = new();

    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

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

    public List<Tag> Tags
    {
        get => _tags;
        internal set
        {
            if (_tags != value)
            {
                _tags = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
