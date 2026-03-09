using System;
using System.Linq;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Services;

namespace TaskApp.Tests;

public class RewardMapperRoundTripTests
{
    [Fact]
    public void Reward_RoundTrip_PreservesAllProperties()
    {
        var tagId = Guid.NewGuid();
        var original = new Reward("Weekend Trip", "Fun trip", isRepeatable: false, goldCost: 50.0)
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        original.SetHidden(true);
        original.UpdateTags(new[] { new Tag("Personal", tagId) });
        original.TryClaim(100, new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Notes, restored.Notes);
        Assert.Equal(original.GoldCost, restored.GoldCost);
        Assert.Equal(original.IsRepeatable, restored.IsRepeatable);
        Assert.Equal(original.IsClaimed, restored.IsClaimed);
        Assert.Equal(original.ClaimCount, restored.ClaimCount);
        Assert.Equal(original.ClaimedAt, restored.ClaimedAt);
        Assert.Equal(original.IsHidden, restored.IsHidden);
        Assert.Single(restored.Tags);
        Assert.Equal(tagId, restored.Tags[0].Id);
        Assert.Equal("Personal", restored.Tags[0].Name);
    }

    [Fact]
    public void Reward_RoundTrip_RepeatableReward()
    {
        var original = new Reward("Coffee", isRepeatable: true, goldCost: 5);
        original.TryClaim(10);
        original.TryClaim(10);
        original.TryClaim(10);

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.True(restored.IsRepeatable);
        Assert.False(restored.IsClaimed);
        Assert.Equal(3, restored.ClaimCount);
    }

    [Fact]
    public void Reward_RoundTrip_DefaultCreatedAt_GetsCurrentTime()
    {
        var data = new TaskApp.Data.RewardData
        {
            Id = Guid.NewGuid(),
            Title = "Legacy Reward",
            CreatedAt = default
        };

        var before = DateTimeOffset.UtcNow;
        var restored = RewardMapper.ToModel(data);

        Assert.True(restored.CreatedAt >= before.AddSeconds(-1));
    }

    [Fact]
    public void Reward_RoundTrip_EmptyTags()
    {
        var original = new Reward("Simple");

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.Empty(restored.Tags);
    }

    [Fact]
    public void Reward_RoundTrip_MultipleTags()
    {
        var original = new Reward("Multi-tag");
        original.UpdateTags(new[]
        {
            new Tag("A", Guid.NewGuid()),
            new Tag("B", Guid.NewGuid())
        });

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.Equal(2, restored.Tags.Count);
    }

    [Fact]
    public void Reward_RoundTrip_ZeroCost()
    {
        var original = new Reward("Free", goldCost: 0);

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.Equal(0, restored.GoldCost);
    }

    [Fact]
    public void Reward_RoundTrip_NullNotes()
    {
        var original = new Reward("No notes");

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.Null(restored.Notes);
    }

    [Fact]
    public void Reward_RoundTrip_UnclaimedReward()
    {
        var original = new Reward("Unclaimed", goldCost: 100);

        var data = RewardMapper.ToData(original);
        var restored = RewardMapper.ToModel(data);

        Assert.False(restored.IsClaimed);
        Assert.Equal(0, restored.ClaimCount);
        Assert.Null(restored.ClaimedAt);
    }
}
