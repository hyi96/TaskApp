using System;
using TaskApp.Models.Tags;

namespace TaskApp.ViewModels;

public class SelectableTag : ViewModelBase
{
    private bool _isSelected;
    private Tag _tag;

    public Tag Tag
    {
        get => _tag;
        private set => SetProperty(ref _tag, value);
    }

    public string Name
    {
        get => _tag.Name;
        set
        {
            if (_tag.Name != value)
            {
                _tag.Name = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnSelectionChanged();
            }
        }
    }
    
    public event System.Action? SelectionChanged;

    public SelectableTag(Tag tag, bool isSelected = false)
    {
        _tag = tag;
        _isSelected = isSelected;
    }

    protected void OnSelectionChanged()
    {
        SelectionChanged?.Invoke();
    }
}

