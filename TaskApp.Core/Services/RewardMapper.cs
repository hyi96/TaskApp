using System;
using System.Linq;
using TaskApp.Data;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;

namespace TaskApp.Services;

public static class RewardMapper
{
    public static Reward ToModel(RewardData data)
    {
        var reward = new Reward(data.Title, data.Notes, data.IsRepeatable, data.GoldCost)
        {
            Id = data.Id,
            CreatedAt = data.CreatedAt
        };

        // Properties with internal setters
        reward.IsClaimed = data.IsClaimed;
        reward.ClaimCount = data.ClaimCount;
        reward.ClaimedAt = data.ClaimedAt;
        reward.IsHidden = data.IsHidden;
        if (data.Tags != null)
        {
            reward.Tags.AddRange(data.Tags.Select(t => new Tag(t.Name, t.Id)));
        }

        return reward;
    }

    public static RewardData ToData(Reward model)
    {
        return new RewardData
        {
            Id = model.Id,
            CreatedAt = model.CreatedAt,
            Title = model.Title,
            Notes = model.Notes,
            IsClaimed = model.IsClaimed,
            IsRepeatable = model.IsRepeatable,
            ClaimCount = model.ClaimCount,
            ClaimedAt = model.ClaimedAt,
            GoldCost = model.GoldCost,
            Tags = model.Tags.Select(t => new TagData { Id = t.Id, Name = t.Name }).ToList(),
            IsHidden = model.IsHidden
        };
    }
}
