using System;

namespace TaskApp.Models.Tasks;

public class StreakBonusRule
{
    public int StreakGoal { get; private set; }
    public double BonusPercent { get; private set; }

    public StreakBonusRule(int streakGoal, double bonusPercent)
    {
        SetStreakGoal(streakGoal);
        SetBonusPercent(bonusPercent);
    }

    public void SetStreakGoal(int streakGoal)
    {
        StreakGoal = Math.Max(1, streakGoal);
    }

    public void SetBonusPercent(double bonusPercent)
    {
        BonusPercent = bonusPercent < 0 ? 0 : bonusPercent;
    }
}
