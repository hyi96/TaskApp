using System.Collections.ObjectModel;
using System.Linq;

namespace TaskApp.ViewModels;

public class TagsViewModel : ViewModelBase
{
    private readonly ObservableCollection<SelectableTag> _availableTags;
    private readonly MainWindowViewModel? _mainViewModel;
    private string _newTagValues = string.Empty;

    public string NewTagValue
    {
        get => _newTagValues;
        set => SetProperty(ref _newTagValues, value);
    }

    public ObservableCollection<SelectableTag> Tags => _availableTags;

    public TagsViewModel(ObservableCollection<SelectableTag> availableTags, MainWindowViewModel? mainViewModel = null)
    {
        _availableTags = availableTags;
        _mainViewModel = mainViewModel;
    }

    public void AddTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagValue)) return;
        
        var val = NewTagValue.Trim();
        if (_availableTags.All(t => t.Name != val))
        {
            _availableTags.Add(new SelectableTag(val));
        }
        NewTagValue = string.Empty;
    }

    public void RemoveTag(SelectableTag tag)
    {
        if (_availableTags.Contains(tag))
        {
            _availableTags.Remove(tag);
            
            // Remove the tag from all tasks
            if (_mainViewModel != null)
            {
                _mainViewModel.RemoveTagFromAllTasks(tag.Name);
            }
        }
    }
}
