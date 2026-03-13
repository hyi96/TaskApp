using System;
using System.Linq;
using TaskApp.Models.Tasks;

namespace TaskApp.Tests;

public class DailyTaskTests
{
    #region Period calculation — Daily cadence

    [Fact]
    public void GetPeriodStart_Daily_SameDayReturnsSameStart()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
        var dateLater = new DateTimeOffset(2026, 3, 10, 22, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(date, RepeatCadence.Daily, 1, daily.CreatedAt);
        var p2 = daily.GetPeriodStart(dateLater, RepeatCadence.Daily, 1, daily.CreatedAt);

        Assert.Equal(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Daily_NextDayReturnsDifferentStart()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var day1 = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(day1, RepeatCadence.Daily, 1, daily.CreatedAt);
        var p2 = daily.GetPeriodStart(day2, RepeatCadence.Daily, 1, daily.CreatedAt);

        Assert.NotEqual(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Daily_RepeatEvery2_GroupsTwoDays()
    {
        var anchor = MakeLocal(2026, 3, 10, 0);
        var daily = CreateDailyWithAnchor(RepeatCadence.Daily, 2, anchor);

        var day1 = MakeLocal(2026, 3, 10, 12);
        var day2 = MakeLocal(2026, 3, 11, 12);
        var day3 = MakeLocal(2026, 3, 12, 12);

        var p1 = daily.GetPeriodStart(day1, RepeatCadence.Daily, 2, anchor);
        var p2 = daily.GetPeriodStart(day2, RepeatCadence.Daily, 2, anchor);
        var p3 = daily.GetPeriodStart(day3, RepeatCadence.Daily, 2, anchor);

        Assert.Equal(p1, p2);
        Assert.NotEqual(p1, p3);
    }

    #endregion

    #region Period calculation — Weekly cadence

    [Fact]
    public void GetPeriodStart_Weekly_SameWeekReturnsSameStart()
    {
        var anchor = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero); // Monday
        var daily = CreateDailyWithAnchor(RepeatCadence.Weekly, 1, anchor);

        var wednesday = new DateTimeOffset(2026, 2, 4, 12, 0, 0, TimeSpan.Zero);
        var friday = new DateTimeOffset(2026, 2, 6, 12, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(wednesday, RepeatCadence.Weekly, 1, anchor);
        var p2 = daily.GetPeriodStart(friday, RepeatCadence.Weekly, 1, anchor);

        Assert.Equal(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Weekly_DifferentWeekReturnsDifferentStart()
    {
        var anchor = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
        var daily = CreateDailyWithAnchor(RepeatCadence.Weekly, 1, anchor);

        var week1 = new DateTimeOffset(2026, 2, 4, 12, 0, 0, TimeSpan.Zero);
        var week2 = new DateTimeOffset(2026, 2, 11, 12, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(week1, RepeatCadence.Weekly, 1, anchor);
        var p2 = daily.GetPeriodStart(week2, RepeatCadence.Weekly, 1, anchor);

        Assert.NotEqual(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Weekly_StartsOnMonday()
    {
        var anchor = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
        var daily = CreateDailyWithAnchor(RepeatCadence.Weekly, 1, anchor);

        // Sunday Feb 8, 2026
        var sunday = new DateTimeOffset(2026, 2, 8, 12, 0, 0, TimeSpan.Zero);
        var periodStart = daily.GetPeriodStart(sunday, RepeatCadence.Weekly, 1, anchor);

        Assert.Equal(DayOfWeek.Monday, periodStart.DayOfWeek);
    }

    #endregion

    #region Period calculation — Monthly cadence

    [Fact]
    public void GetPeriodStart_Monthly_AlwaysReturnsFirstOfMonth()
    {
        var anchor = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var daily = CreateDailyWithAnchor(RepeatCadence.Monthly, 1, anchor);

        var mid = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(mid, RepeatCadence.Monthly, 1, anchor);
        var p2 = daily.GetPeriodStart(end, RepeatCadence.Monthly, 1, anchor);

        Assert.Equal(new DateOnly(2026, 3, 1), p1);
        Assert.Equal(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Monthly_RepeatEvery2_GroupsTwoMonths()
    {
        var anchor = MakeLocal(2026, 1, 1, 0);
        var daily = CreateDailyWithAnchor(RepeatCadence.Monthly, 2, anchor);

        var jan = MakeLocal(2026, 1, 15, 12);
        var feb = MakeLocal(2026, 2, 15, 12);
        var mar = MakeLocal(2026, 3, 15, 12);

        var p1 = daily.GetPeriodStart(jan, RepeatCadence.Monthly, 2, anchor);
        var p2 = daily.GetPeriodStart(feb, RepeatCadence.Monthly, 2, anchor);
        var p3 = daily.GetPeriodStart(mar, RepeatCadence.Monthly, 2, anchor);

        Assert.Equal(p1, p2);
        Assert.NotEqual(p1, p3);
    }

    #endregion

    #region Period calculation — Yearly cadence

    [Fact]
    public void GetPeriodStart_Yearly_ReturnsJanFirst()
    {
        var anchor = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var daily = CreateDailyWithAnchor(RepeatCadence.Yearly, 1, anchor);

        var date = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var periodStart = daily.GetPeriodStart(date, RepeatCadence.Yearly, 1, anchor);

        Assert.Equal(1, periodStart.Month);
        Assert.Equal(1, periodStart.Day);
    }

    [Fact]
    public void GetPeriodStart_Yearly_DifferentYearsReturnDifferentPeriods()
    {
        var anchor = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var daily = CreateDailyWithAnchor(RepeatCadence.Yearly, 1, anchor);

        var y1 = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var y2 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var p1 = daily.GetPeriodStart(y1, RepeatCadence.Yearly, 1, anchor);
        var p2 = daily.GetPeriodStart(y2, RepeatCadence.Yearly, 1, anchor);

        Assert.NotEqual(p1, p2);
    }

    [Fact]
    public void GetPeriodStart_Yearly_RepeatEvery2_GroupsTwoYears()
    {
        var anchor = MakeLocal(2024, 1, 1, 0);
        var daily = CreateDailyWithAnchor(RepeatCadence.Yearly, 2, anchor);

        var y1 = MakeLocal(2024, 6, 1, 12);
        var y2 = MakeLocal(2025, 6, 1, 12);
        var y3 = MakeLocal(2026, 6, 1, 12);

        var p1 = daily.GetPeriodStart(y1, RepeatCadence.Yearly, 2, anchor);
        var p2 = daily.GetPeriodStart(y2, RepeatCadence.Yearly, 2, anchor);
        var p3 = daily.GetPeriodStart(y3, RepeatCadence.Yearly, 2, anchor);

        Assert.Equal(p1, p2);
        Assert.NotEqual(p1, p3);
    }

    #endregion

    #region Streak tracking

    [Fact]
    public void Complete_FirstCompletion_SetsStreakToOne()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        daily.Complete(date);

        Assert.Equal(1, daily.CurrentStreak);
        Assert.Equal(1, daily.BestStreak);
    }

    [Fact]
    public void Complete_ConsecutiveDays_IncrementsStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.Complete(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        daily.Complete(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        daily.Complete(new DateTimeOffset(2026, 3, 12, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, daily.CurrentStreak);
        Assert.Equal(3, daily.BestStreak);
    }

    [Fact]
    public void Complete_SkippedDay_ResetsStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.Complete(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        daily.Complete(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        // Skip Mar 12
        daily.Complete(new DateTimeOffset(2026, 3, 13, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void Complete_SkippedDay_PreservesBestStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.Complete(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        daily.Complete(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        daily.Complete(new DateTimeOffset(2026, 3, 12, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(3, daily.BestStreak);

        // Skip, then resume
        daily.Complete(new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, daily.CurrentStreak);
        Assert.Equal(3, daily.BestStreak);
    }

    [Fact]
    public void Complete_SamePeriodTwice_IsIdempotent()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var dateLater = new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

        daily.Complete(date);
        daily.Complete(dateLater);

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void SetCurrentStreak_UpdatesBestStreak_WhenHigher()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetCurrentStreak(10);

        Assert.Equal(10, daily.CurrentStreak);
        Assert.Equal(10, daily.BestStreak);
    }

    [Fact]
    public void SetCurrentStreak_NegativeValue_ClampsToZero()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetCurrentStreak(-5);

        Assert.Equal(0, daily.CurrentStreak);
    }

    [Fact]
    public void SetCurrentStreak_LowerThanBest_DoesNotReduceBestStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetCurrentStreak(10);
        Assert.Equal(10, daily.BestStreak);

        daily.SetCurrentStreak(3);

        Assert.Equal(3, daily.CurrentStreak);
        Assert.Equal(10, daily.BestStreak);
    }

    [Fact]
    public void DecrementStreak_DoesNotGoBelowZero()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.DecrementStreak();

        Assert.Equal(0, daily.CurrentStreak);
    }

    [Fact]
    public void ResetStreak_SetsToZero()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetCurrentStreak(5);
        daily.ResetStreak();

        Assert.Equal(0, daily.CurrentStreak);
    }

    #endregion

    #region CompleteForPeriod

    [Fact]
    public void CompleteForPeriod_SetsLastCompletionPeriod()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var period = new DateOnly(2026, 3, 10);

        daily.CompleteForPeriod(period);

        Assert.Equal(period, daily.LastCompletionPeriod);
        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void CompleteForPeriod_ConsecutivePeriods_IncrementsStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.CompleteForPeriod(new DateOnly(2026, 3, 10));
        daily.CompleteForPeriod(new DateOnly(2026, 3, 11));

        Assert.Equal(2, daily.CurrentStreak);
    }

    [Fact]
    public void CompleteForPeriod_SamePeriodTwice_IsIdempotent()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var period = new DateOnly(2026, 3, 10);

        daily.CompleteForPeriod(period);
        daily.CompleteForPeriod(period);

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void CompleteForPeriod_GapInPeriods_ResetsStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.CompleteForPeriod(new DateOnly(2026, 3, 10));
        daily.CompleteForPeriod(new DateOnly(2026, 3, 11));
        // Gap
        daily.CompleteForPeriod(new DateOnly(2026, 3, 13));

        Assert.Equal(1, daily.CurrentStreak);
        Assert.Equal(2, daily.BestStreak);
    }

    #endregion

    #region RefreshForCurrentPeriod

    [Fact]
    public void RefreshForCurrentPeriod_GapFromLastPeriod_ResetsStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var pastDate = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        daily.Complete(pastDate);
        daily.Complete(pastDate.AddDays(1));
        Assert.Equal(2, daily.CurrentStreak);

        // Refresh at a much later date
        daily.RefreshForCurrentPeriod(pastDate.AddDays(30));

        Assert.Equal(0, daily.CurrentStreak);
    }

    [Fact]
    public void RefreshForCurrentPeriod_CompletedInPreviousPeriod_PreservesStreak()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var yesterday = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var today = yesterday.AddDays(1);

        daily.Complete(yesterday);
        daily.RefreshForCurrentPeriod(today);

        Assert.Equal(1, daily.CurrentStreak);
    }

    #endregion

    #region IsCompleteForPeriod

    [Fact]
    public void IsCompleteForPeriod_ReturnsTrueWhenCompletedInSamePeriod()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        daily.Complete(date);

        Assert.True(daily.IsCompleteForPeriod(date));
    }

    [Fact]
    public void IsCompleteForPeriod_ReturnsFalseForDifferentPeriod()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date1 = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero);

        daily.Complete(date1);

        Assert.False(daily.IsCompleteForPeriod(date2));
    }

    [Fact]
    public void IsCompleteForPeriod_ReturnsFalseWhenNeverCompleted()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        var date = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        Assert.False(daily.IsCompleteForPeriod(date));
    }

    #endregion

    #region Streak bonus

    [Fact]
    public void GetGoldRewardWithBonus_NoStreak_ReturnsBaseReward()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetGoldReward(10);

        Assert.Equal(10.0, daily.GetGoldRewardWithBonus());
    }

    [Fact]
    public void GetGoldRewardWithBonus_MeetsStreakGoal_AppliesBonus()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetGoldReward(10);
        daily.SetCurrentStreak(7);

        var reward = daily.GetGoldRewardWithBonus();

        // Default rule: 7 streak → 10% bonus → 10 * 1.10 = 11
        Assert.Equal(11.0, reward);
    }

    [Fact]
    public void GetGoldRewardWithBonus_MultipleRulesMet_AppliesHighestBonus()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetGoldReward(10);
        daily.SetCurrentStreak(30);

        var reward = daily.GetGoldRewardWithBonus();

        // Default rules: 7→10%, 14→20%, 30→30% → highest is 30% → 10 * 1.30 = 13
        Assert.Equal(13.0, reward);
    }

    [Fact]
    public void SetStreakBonusRules_ReplacesDefaults()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetStreakBonusRules(new[] { new StreakBonusRule(5, 50) });

        Assert.Single(daily.StreakBonusRules);
        Assert.Equal(5, daily.StreakBonusRules[0].StreakGoal);
    }

    [Fact]
    public void SetStreakBonusRules_IgnoresDuplicateGoals()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetStreakBonusRules(new[]
        {
            new StreakBonusRule(5, 10),
            new StreakBonusRule(5, 20)
        });

        Assert.Single(daily.StreakBonusRules);
    }

    #endregion

    #region Cadence/RepeatEvery setters

    [Fact]
    public void SetRepeatEvery_ZeroValue_ClampsToOne()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetRepeatEvery(0);

        Assert.Equal(1, daily.RepeatEvery);
    }

    [Fact]
    public void SetRepeatEvery_NegativeValue_ClampsToOne()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetRepeatEvery(-3);

        Assert.Equal(1, daily.RepeatEvery);
    }

    #endregion

    #region Autocomplete threshold

    [Fact]
    public void SetAutocompleteTimeThreshold_StoresValue()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void SetAutocompleteTimeThreshold_Null_ClearsValue()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        daily.SetAutocompleteTimeThreshold(null);

        Assert.Null(daily.AutocompleteTimeThreshold);
    }

    #endregion

    #region Period change preserves completion

    [Fact]
    public void SetCadence_PreservesCompletion_WhenCompletedInCurrentPeriod()
    {
        // Weekly every 1 → complete → change to every 2 weeks → still complete
        var daily = CreateDaily(RepeatCadence.Weekly, 1);

        daily.Complete();
        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.Equal(1, daily.CurrentStreak);

        daily.SetRepeatEvery(2);

        // Should still be complete for the current period under new settings
        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void SetCadence_PreservesCompletion_WhenChangingCadenceType()
    {
        // Daily every 1 → complete today → change to Weekly → still complete
        var daily = CreateDaily(RepeatCadence.Daily, 1);

        daily.Complete();
        Assert.True(daily.IsCompleteForCurrentPeriod);

        daily.SetCadence(RepeatCadence.Weekly);

        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void SetRepeatEvery_PreservesStreak_WhenReCompleting()
    {
        // Complete current weekly period → change to every 2 weeks → re-complete → streak preserved
        var daily = CreateDaily(RepeatCadence.Weekly, 1);

        daily.Complete();
        Assert.Equal(1, daily.CurrentStreak);

        // Change to every 2 weeks
        daily.SetRepeatEvery(2);

        // Already complete for current period, re-complete is a no-op
        daily.Complete();
        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void SetCadence_DoesNotMarkComplete_WhenNotPreviouslyCompleted()
    {
        var daily = CreateDaily(RepeatCadence.Daily, 1);
        Assert.Null(daily.LastCompletedDate);

        daily.SetCadence(RepeatCadence.Weekly);

        Assert.Null(daily.LastCompletedDate);
        Assert.Null(daily.LastCompletionPeriod);
        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void SetRepeatEvery_PreservesCompletionState()
    {
        var daily = CreateDaily(RepeatCadence.Weekly, 1);

        daily.Complete();
        Assert.True(daily.IsCompleteForCurrentPeriod);
        var completedDate = daily.LastCompletedDate;

        daily.SetRepeatEvery(2);

        // Completion state preserved, LastCompletedDate untouched
        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.Equal(completedDate, daily.LastCompletedDate);
    }

    [Fact]
    public void Reproduce_UserBug_WeeklyToEvery2Weeks()
    {
        // Bug: streak built up, complete for this week, change to every 2 weeks,
        // task uncompletes, re-complete → streak resets to 1
        var daily = CreateDaily(RepeatCadence.Weekly, 1);

        // Complete previous period to build streak foundation
        var prevPeriod = daily.GetPreviousPeriodStart();
        daily.CompleteForPeriod(prevPeriod);
        daily.SetCurrentStreak(5);

        // Complete for current week → streak 6
        daily.Complete();
        Assert.Equal(6, daily.CurrentStreak);
        Assert.True(daily.IsCompleteForCurrentPeriod);

        // User changes period to every 2 weeks
        daily.SetRepeatEvery(2);

        // Task should still be done
        Assert.True(daily.IsCompleteForCurrentPeriod);
        // Streak should NOT have been wiped
        Assert.Equal(6, daily.CurrentStreak);

        // Re-complete is a no-op
        daily.Complete();
        Assert.Equal(6, daily.CurrentStreak);
    }

    [Fact]
    public void SetCadence_DailyToWeekly_IncompleteStaysIncomplete()
    {
        // Completed yesterday (daily cadence) → not complete today.
        // Switch to weekly → yesterday is in the same week → but should NOT become complete.
        var yesterday = DateTimeOffset.UtcNow.ToLocalTime().AddDays(-1);
        var anchor = yesterday.AddDays(-7);
        var daily = CreateDailyWithAnchor(RepeatCadence.Daily, 1, anchor);

        daily.Complete(yesterday);
        Assert.False(daily.IsCompleteForCurrentPeriod); // not complete for today
        var savedCompletedDate = daily.LastCompletedDate;

        daily.SetCadence(RepeatCadence.Weekly);

        // Must remain NOT complete — user didn't complete it for the new weekly period
        Assert.False(daily.IsCompleteForCurrentPeriod);
        // LastCompletedDate must be untouched
        Assert.Equal(savedCompletedDate, daily.LastCompletedDate);
    }

    [Fact]
    public void SetRepeatEvery_IncompleteStaysIncomplete()
    {
        // Weekly every 1 → completed last week → not complete this week.
        // Change to every 2 weeks — if last week falls in same 2-week period, should still NOT be complete.
        var now = DateTimeOffset.UtcNow.ToLocalTime();
        var anchor = now.AddDays(-21);
        var daily = CreateDailyWithAnchor(RepeatCadence.Weekly, 1, anchor);

        var lastWeek = now.AddDays(-7);
        daily.Complete(lastWeek);
        Assert.False(daily.IsCompleteForCurrentPeriod);

        daily.SetRepeatEvery(2);

        // Must remain NOT complete
        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void SetCadence_LastCompletedDate_NeverChanges()
    {
        var anchor = MakeLocal(2026, 3, 1, 12);
        var daily = CreateDailyWithAnchor(RepeatCadence.Daily, 1, anchor);
        var completionTime = MakeLocal(2026, 3, 10, 14);

        daily.Complete(completionTime);
        var original = daily.LastCompletedDate;

        daily.SetCadence(RepeatCadence.Weekly);
        Assert.Equal(original, daily.LastCompletedDate);

        daily.SetCadence(RepeatCadence.Monthly);
        Assert.Equal(original, daily.LastCompletedDate);

        daily.SetRepeatEvery(3);
        Assert.Equal(original, daily.LastCompletedDate);
    }

    #endregion

    #region Test helpers

    private static DailyTask CreateDaily(RepeatCadence cadence, int repeatEvery)
    {
        var daily = new DailyTask();
        daily.UpdateTitle("Test Daily");
        daily.SetCadence(cadence);
        daily.SetRepeatEvery(repeatEvery);
        return daily;
    }

    private static DailyTask CreateDailyWithAnchor(RepeatCadence cadence, int repeatEvery, DateTimeOffset anchor)
    {
        var daily = new DailyTask
        {
            CreatedAt = anchor
        };
        daily.UpdateTitle("Test Daily");
        daily.SetCadence(cadence);
        daily.SetRepeatEvery(repeatEvery);
        return daily;
    }

    /// <summary>
    /// Creates a DateTimeOffset in local time so that ToLocalTime() is a no-op,
    /// avoiding date shifts on non-UTC machines.
    /// </summary>
    private static DateTimeOffset MakeLocal(int year, int month, int day, int hour)
    {
        var dt = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local);
        return new DateTimeOffset(dt);
    }

    #endregion
}
