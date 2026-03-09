using System;
using System.IO;
using System.Linq;
using TaskApp.Models.Rewards;
using TaskApp.Services;
using TaskApp.ViewModels;

namespace TaskApp.Tests;

public class RewardTests : IDisposable
{
    private readonly string _tempDir;

    public RewardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    #region CanClaim

    [Fact]
    public void CanClaim_SufficientGold_ReturnsTrue()
    {
        var reward = new Reward("Test", goldCost: 10);

        Assert.True(reward.CanClaim(10));
        Assert.True(reward.CanClaim(20));
    }

    [Fact]
    public void CanClaim_InsufficientGold_ReturnsFalse()
    {
        var reward = new Reward("Test", goldCost: 10);

        Assert.False(reward.CanClaim(9.99));
    }

    [Fact]
    public void CanClaim_ZeroCostZeroGold_ReturnsTrue()
    {
        var reward = new Reward("Test", goldCost: 0);

        Assert.True(reward.CanClaim(0));
    }

    [Fact]
    public void CanClaim_AlreadyClaimedNonRepeatable_ReturnsFalse()
    {
        var reward = new Reward("Test", isRepeatable: false, goldCost: 0);
        reward.TryClaim(100);

        Assert.False(reward.CanClaim(100));
    }

    [Fact]
    public void CanClaim_AlreadyClaimedRepeatable_ReturnsTrue()
    {
        var reward = new Reward("Test", isRepeatable: true, goldCost: 0);
        reward.TryClaim(100);

        Assert.True(reward.CanClaim(100));
    }

    #endregion

    #region TryClaim — one-time rewards

    [Fact]
    public void TryClaim_OneTime_SetsIsClaimed()
    {
        var reward = new Reward("Test", isRepeatable: false, goldCost: 5);

        var result = reward.TryClaim(10);

        Assert.True(result);
        Assert.True(reward.IsClaimed);
    }

    [Fact]
    public void TryClaim_OneTime_SecondCallReturnsFalse()
    {
        var reward = new Reward("Test", isRepeatable: false, goldCost: 5);
        reward.TryClaim(10);

        var result = reward.TryClaim(10);

        Assert.False(result);
    }

    [Fact]
    public void TryClaim_OneTime_IncrementsClaimCount()
    {
        var reward = new Reward("Test", isRepeatable: false, goldCost: 0);

        reward.TryClaim(0);

        Assert.Equal(1, reward.ClaimCount);
    }

    [Fact]
    public void TryClaim_OneTime_SetsClaimedAt()
    {
        var reward = new Reward("Test", isRepeatable: false, goldCost: 0);
        var claimTime = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        reward.TryClaim(0, claimTime);

        Assert.Equal(claimTime, reward.ClaimedAt);
    }

    #endregion

    #region TryClaim — repeatable rewards

    [Fact]
    public void TryClaim_Repeatable_DoesNotStayClaimed()
    {
        var reward = new Reward("Test", isRepeatable: true, goldCost: 0);

        reward.TryClaim(100);

        Assert.False(reward.IsClaimed);
    }

    [Fact]
    public void TryClaim_Repeatable_CanBeClaimedMultipleTimes()
    {
        var reward = new Reward("Test", isRepeatable: true, goldCost: 5);

        Assert.True(reward.TryClaim(10));
        Assert.True(reward.TryClaim(10));
        Assert.True(reward.TryClaim(10));

        Assert.Equal(3, reward.ClaimCount);
    }

    [Fact]
    public void TryClaim_Repeatable_TracksLastClaimedAt()
    {
        var reward = new Reward("Test", isRepeatable: true, goldCost: 0);
        var time1 = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero);

        reward.TryClaim(0, time1);
        reward.TryClaim(0, time2);

        Assert.Equal(time2, reward.ClaimedAt);
    }

    #endregion

    #region TryClaim — insufficient gold

    [Fact]
    public void TryClaim_InsufficientGold_ReturnsFalse()
    {
        var reward = new Reward("Test", goldCost: 100);

        Assert.False(reward.TryClaim(50));
    }

    [Fact]
    public void TryClaim_InsufficientGold_DoesNotIncrementClaimCount()
    {
        var reward = new Reward("Test", goldCost: 100);
        reward.TryClaim(50);

        Assert.Equal(0, reward.ClaimCount);
    }

    [Fact]
    public void TryClaim_InsufficientGold_DoesNotSetClaimed()
    {
        var reward = new Reward("Test", goldCost: 100);
        reward.TryClaim(50);

        Assert.False(reward.IsClaimed);
    }

    #endregion

    #region Gold cost validation

    [Fact]
    public void SetGoldCost_NegativeValue_ClampsToZero()
    {
        var reward = new Reward("Test");
        reward.SetGoldCost(-10);

        Assert.Equal(0, reward.GoldCost);
    }

    [Fact]
    public void SetGoldCost_PositiveValue_SetsCorrectly()
    {
        var reward = new Reward("Test");
        reward.SetGoldCost(25.5);

        Assert.Equal(25.5, reward.GoldCost);
    }

    [Fact]
    public void Constructor_NegativeGoldCost_ClampsToZero()
    {
        var reward = new Reward("Test", goldCost: -5);

        Assert.Equal(0, reward.GoldCost);
    }

    #endregion

    #region Reward filter — claimed non-repeatable bug regression

    [Fact]
    public void ClaimReward_NonRepeatable_StaysVisibleInAllFilter()
    {
        var vm = CreateViewModel();
        vm.AddGold(100);

        var reward = new Reward("One-Time Reward", isRepeatable: false, goldCost: 10);
        AddRewardToViewModel(vm, reward);

        Assert.Contains(reward, vm.Rewards);

        vm.ClaimReward(reward);
        vm.RefreshFilter();

        Assert.Contains(reward, vm.Rewards);
    }

    [Fact]
    public void ClaimReward_NonRepeatable_StaysVisibleAfterFilterChange()
    {
        var vm = CreateViewModel();
        vm.AddGold(100);

        var reward = new Reward("One-Time Reward", isRepeatable: false, goldCost: 10);
        AddRewardToViewModel(vm, reward);

        vm.ClaimReward(reward);

        vm.RewardsFilter = "one-time";
        Assert.Contains(reward, vm.Rewards);

        vm.RewardsFilter = "all";
        Assert.Contains(reward, vm.Rewards);
    }

    [Fact]
    public void ClaimReward_Repeatable_StaysInAllFilter()
    {
        var vm = CreateViewModel();
        vm.AddGold(100);

        var reward = new Reward("Repeatable Reward", isRepeatable: true, goldCost: 10);
        AddRewardToViewModel(vm, reward);

        vm.ClaimReward(reward);
        vm.RefreshFilter();

        Assert.Contains(reward, vm.Rewards);
    }

    #endregion

    #region Gold management

    [Fact]
    public void AddGold_IncreasesUserGold()
    {
        var vm = CreateViewModel();
        vm.AddGold(50);

        Assert.Equal(50, vm.User.Gold);
    }

    [Fact]
    public void RemoveGold_DecreasesUserGold()
    {
        var vm = CreateViewModel();
        vm.AddGold(100);
        vm.RemoveGold(30);

        Assert.Equal(70, vm.User.Gold);
    }

    [Fact]
    public void RemoveGold_InsufficientFunds_DoesNotDeduct()
    {
        var vm = CreateViewModel();
        vm.AddGold(10);
        vm.RemoveGold(20);

        Assert.Equal(10, vm.User.Gold);
    }

    [Fact]
    public void RemoveGold_ExactAmount_SetsToZero()
    {
        var vm = CreateViewModel();
        vm.AddGold(10);
        vm.RemoveGold(10);

        Assert.Equal(0, vm.User.Gold);
    }

    #endregion

    #region Reward setters

    [Fact]
    public void UpdateTitle_SetsTitle()
    {
        var reward = new Reward("Original");
        reward.UpdateTitle("Updated");

        Assert.Equal("Updated", reward.Title);
    }

    [Fact]
    public void UpdateTitle_Null_SetsEmpty()
    {
        var reward = new Reward("Original");
        reward.UpdateTitle(null!);

        Assert.Equal(string.Empty, reward.Title);
    }

    [Fact]
    public void SetRepeatable_ChangesValue()
    {
        var reward = new Reward("Test", isRepeatable: false);
        reward.SetRepeatable(true);

        Assert.True(reward.IsRepeatable);
    }

    #endregion

    #region Helpers

    private MainWindowViewModel CreateViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }

    private static void AddRewardToViewModel(MainWindowViewModel vm, Reward reward)
    {
        vm.NewRewardTitle = reward.Title;
        vm.AddReward();

        // Replace the auto-created reward with our specific one
        var created = vm.Rewards.First(r => r.Title == reward.Title);
        var rewardsField = typeof(MainWindowViewModel)
            .GetField("_allRewards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var allRewards = (System.Collections.Generic.List<Reward>)rewardsField.GetValue(vm)!;
        var idx = allRewards.IndexOf(created);
        allRewards[idx] = reward;
        vm.RefreshFilter();
    }

    #endregion
}
