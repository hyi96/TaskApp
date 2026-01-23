using System;
using System.Linq;
using TaskApp.Data;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public static class RewardMapper
{
    public static Reward ToModel(RewardData data, Func<Guid, TaskBase?> taskResolver)
    {
        var reward = new Reward(data.Title, data.Notes, data.IsRepeatable, data.GoldCost)
        {
            Id = data.Id
        };

        // Properties with internal setters
        reward.IsClaimed = data.IsClaimed;
        reward.ClaimCount = data.ClaimCount;
        reward.ClaimedAt = data.ClaimedAt;
        if (data.Tags != null)
        {
            reward.Tags.AddRange(data.Tags);
        }
        
        // Linked Tasks
        foreach (var taskId in data.LinkedTaskIds)
        {
            var task = taskResolver(taskId);
            if (task != null)
            {
                reward.LinkTask(task);
            }
        }

        return reward;
    }

    public static RewardData ToData(Reward model)
    {
        return new RewardData
        {
            Id = model.Id,
            Title = model.Title,
            Notes = model.Notes,
            IsClaimed = model.IsClaimed,
            IsRepeatable = model.IsRepeatable,
            ClaimCount = model.ClaimCount,
            ClaimedAt = model.ClaimedAt,
            GoldCost = model.GoldCost,
            Tags = model.Tags,
            LinkedTaskIds = model.LinkedTasks.Select(t => t.Id).ToList()
        };
    }
}
