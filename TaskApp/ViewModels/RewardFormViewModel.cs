using System;
using System.Collections.Generic;
using TaskApp.Models.Rewards;

namespace TaskApp.ViewModels;

public class RewardFormViewModel : TaskFormViewModel
{
    private readonly Reward? _reward;
    private bool _isRepeatable;

    public bool IsRepeatable
    {
        get => _isRepeatable;
        set => SetProperty(ref _isRepeatable, value);
    }

    // Placeholder for Linked Tasks
    public string LinkedTasksSummary => "0 Linked Tasks";

    public RewardFormViewModel(IEnumerable<SelectableTag> availableTags, Reward? reward = null)
        : base(availableTags, reward?.Tags)
    {
        // Type property in base is TaskType, but Reward isn't a TaskType enum value exactly (it's separate). 
        // We can ignore Type or add a value if needed, but for now we'll just not set it or treat as special.
        
        _reward = reward;
        if (_reward != null)
        {
            Title = _reward.Title;
            Notes = _reward.Notes ?? string.Empty;
            GoldValue = _reward.GoldCost;
            IsRepeatable = _reward.IsRepeatable;
        }
    }

    public override void Save()
    {
        if (_reward != null)
        {
            _reward.UpdateTitle(Title);
            _reward.UpdateNotes(Notes);
            _reward.SetGoldCost(GoldValue);
            _reward.SetRepeatable(IsRepeatable);
            
            // Save tags
            _reward.Tags = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Where(TaskTags, t => t.IsSelected), 
                t => t.Name));
        }
    }
}
