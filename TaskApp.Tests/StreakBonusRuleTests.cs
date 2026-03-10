using TaskApp.Models.Tasks;
using Xunit;

namespace TaskApp.Tests;

public class StreakBonusRuleTests
{
    [Fact]
    public void Constructor_SetsValues()
    {
        var rule = new StreakBonusRule(7, 0.5);

        Assert.Equal(7, rule.StreakGoal);
        Assert.Equal(0.5, rule.BonusPercent);
    }

    [Fact]
    public void Constructor_ClampsGoalToMin1()
    {
        var rule = new StreakBonusRule(0, 0.5);
        Assert.Equal(1, rule.StreakGoal);

        var rule2 = new StreakBonusRule(-5, 0.5);
        Assert.Equal(1, rule2.StreakGoal);
    }

    [Fact]
    public void Constructor_ClampsNegativeBonusToZero()
    {
        var rule = new StreakBonusRule(5, -0.3);
        Assert.Equal(0, rule.BonusPercent);
    }

    [Fact]
    public void SetStreakGoal_ClampsToMin1()
    {
        var rule = new StreakBonusRule(5, 0.1);
        rule.SetStreakGoal(0);
        Assert.Equal(1, rule.StreakGoal);

        rule.SetStreakGoal(-10);
        Assert.Equal(1, rule.StreakGoal);
    }

    [Fact]
    public void SetStreakGoal_AcceptsPositiveValues()
    {
        var rule = new StreakBonusRule(5, 0.1);
        rule.SetStreakGoal(10);
        Assert.Equal(10, rule.StreakGoal);
    }

    [Fact]
    public void SetBonusPercent_ClampsNegativeToZero()
    {
        var rule = new StreakBonusRule(5, 0.5);
        rule.SetBonusPercent(-1);
        Assert.Equal(0, rule.BonusPercent);
    }

    [Fact]
    public void SetBonusPercent_AcceptsZero()
    {
        var rule = new StreakBonusRule(5, 0.5);
        rule.SetBonusPercent(0);
        Assert.Equal(0, rule.BonusPercent);
    }

    [Fact]
    public void SetBonusPercent_AcceptsPositiveValues()
    {
        var rule = new StreakBonusRule(5, 0.1);
        rule.SetBonusPercent(2.5);
        Assert.Equal(2.5, rule.BonusPercent);
    }
}
