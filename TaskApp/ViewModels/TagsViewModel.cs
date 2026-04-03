using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Tags;

namespace TaskApp.ViewModels;

public class TagsViewModel : ViewModelBase
{
    private readonly ObservableCollection<SelectableTag> _availableTags;
    private readonly MainWindowViewModel? _mainViewModel;
    private string _newTagValue = string.Empty;

    public string NewTagValue
    {
        get => _newTagValue;
        set => SetProperty(ref _newTagValue, value);
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

    public async Task RemoveTagAsync(SelectableTag tag)
    {
        if (_availableTags.Contains(tag))
        {
            _availableTags.Remove(tag);

            // Remove the tag from all tasks and rewards
            if (_mainViewModel != null)
            {
                await _mainViewModel.RemoveTagFromAllItemsAsync(tag.Tag.Id);
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
