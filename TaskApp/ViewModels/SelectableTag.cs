using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.ViewModels;

public class SelectableTag : ViewModelBase
{
    private bool _isSelected;
    private string _name;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
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

    public SelectableTag(string name, bool isSelected = false)
    {
        _name = name;
        _isSelected = isSelected;
    }

    protected void OnSelectionChanged()
    {
        SelectionChanged?.Invoke();
    }
}
