using System.Collections.ObjectModel;
using System.Linq;
using TaskApp.Models.Tags;

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
            _availableTags.Add(new SelectableTag(new Tag(val)));
        }
        NewTagValue = string.Empty;
    }

    public void RemoveTag(SelectableTag tag)
    {
        if (_availableTags.Contains(tag))
        {
            _availableTags.Remove(tag);
            
            // Remove the tag from all tasks and rewards
            if (_mainViewModel != null)
            {
                _mainViewModel.RemoveTagFromAllItems(tag.Tag.Id);
            }
        }
    }

    public void UpdateTagName(SelectableTag tag, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        var trimmedName = newName.Trim();
        
        // Check if name already exists (excluding the current tag)
        if (_availableTags.Any(t => t.Tag.Id != tag.Tag.Id && t.Name == trimmedName))
        {
            return;
        }

        tag.Tag.Name = trimmedName;
    }
}
