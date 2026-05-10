using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.Models.Tasks;

public class ChecklistItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isCompleted;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted != value)
            {
                _isCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ChecklistItem(string text)
    {
        _text = text;
    }
}
