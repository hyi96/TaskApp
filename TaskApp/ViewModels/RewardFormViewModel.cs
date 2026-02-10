using System;
using System.Collections.Generic;
using System.Linq;
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

    public override string FormTitle => "Edit Reward";

    public RewardFormViewModel(IEnumerable<SelectableTag> availableTags, Reward? reward = null)
        : base(availableTags, reward?.Tags)
    {
        _reward = reward;
        if (_reward != null)
        {
            Title = _reward.Title;
            Notes = _reward.Notes ?? string.Empty;
            GoldValue = _reward.GoldCost;
            IsRepeatable = _reward.IsRepeatable;
            LastCompletedDisplay = _reward.ClaimedAt?.ToLocalTime().ToString("g") ?? "Never";
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
            _reward.Tags.Clear();
            _reward.Tags.AddRange(TaskTags.Where(t => t.IsSelected).Select(t => t.Tag));
        }
    }

    public override Guid? GetTaskId() => null;
    public override Guid? GetRewardId() => _reward?.Id;
}

