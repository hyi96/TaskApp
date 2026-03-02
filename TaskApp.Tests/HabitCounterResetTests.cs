using System;
using TaskApp.Models.Tasks;

namespace TaskApp.Tests;

public class HabitCounterResetTests
{
    #region Never cadence (default)

    [Fact]
    public void CounterReset_Never_DoesNotResetOnIncrement()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Never);
        
        habit.Increment();
        habit.Increment();
        var countAfterFirstDay = habit.Count;
        Assert.Equal(2.0, countAfterFirstDay);
    }

    [Fact]
    public void CounterReset_Never_LastResetPeriodIsNull()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Never);
        
        habit.Increment();
        
        Assert.Null(habit.LastResetPeriod);
    }

    #endregion

    #region Daily cadence

    [Fact]
    public void CounterReset_Daily_ResetsEachDay()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var today = DateTimeOffset.UtcNow;

        // Increment on day 1
        habit.Complete(today);
        habit.Complete(today);
        Assert.Equal(2.0, habit.Count);

        // Move to day 2
        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        
        // Count should reset to 1.0 on day 2
        Assert.Equal(1.0, habit.Count);
    }

    [Fact]
    public void CounterReset_Daily_TracksPeriodStartCorrectly()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today);
        var periodDay1 = habit.LastResetPeriod;

        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        var periodDay2 = habit.LastResetPeriod;

        Assert.NotEqual(periodDay1, periodDay2);
    }

    [Fact]
    public void CounterReset_Daily_DecrementAlsoTriggersReset()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        habit.SetDecrementEnabled(true);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today);
        habit.Complete(today);
        Assert.Equal(2.0, habit.Count);

        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow); // Reset happens
        habit.Decrement(); // Decrement on tomorrow
        
        // Count resets to 0, decrement applies: 0 - 1 = 0 (clamped)
        Assert.Equal(0.0, habit.Count);
    }

    #endregion

    #region Weekly cadence

    [Fact]
    public void CounterReset_Weekly_ResetsOnWeekBoundary()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Weekly);
        
        // Use a known Monday date (2026-02-02 is a Monday)
        var monday = new DateTimeOffset(2026, 2, 2, 12, 0, 0, TimeSpan.Zero);
        
        habit.Complete(monday);
        habit.Complete(monday);
        Assert.Equal(2.0, habit.Count);

        // Same week (Wednesday)
        var wednesday = monday.AddDays(2);
        habit.Complete(wednesday);
        
        // Should not reset within same week
        Assert.Equal(3.0, habit.Count);

        // Next Monday
        var nextMonday = monday.AddDays(7);
        habit.Complete(nextMonday);
        
        // Should reset and increment to 1.0
        Assert.Equal(1.0, habit.Count);
    }

    [Fact]
    public void CounterReset_Weekly_UsesMonday()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Weekly);
        
        // Friday
        var friday = new DateTimeOffset(2026, 2, 6, 12, 0, 0, TimeSpan.Zero);
        habit.Complete(friday);
        var fridayPeriod = habit.LastResetPeriod;

        // Saturday (same week)
        var saturday = friday.AddDays(1);
        habit.Complete(saturday);
        var saturdayPeriod = habit.LastResetPeriod;

        // Both should be Monday 2026-02-02
        var monday = new DateOnly(2026, 2, 2);
        Assert.Equal(monday, fridayPeriod);
        Assert.Equal(monday, saturdayPeriod);
    }

    [Fact]
    public void CounterReset_Weekly_ResetsOnMonday()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Weekly);
        
        // Sunday 2026-02-01
        var sunday = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        habit.Complete(sunday);
        habit.Complete(sunday);
        Assert.Equal(2.0, habit.Count);

        // Monday 2026-02-02 (new week)
        var monday = sunday.AddDays(1);
        habit.Complete(monday);
        
        // Should reset
        Assert.Equal(1.0, habit.Count);
        Assert.Equal(new DateOnly(2026, 2, 2), habit.LastResetPeriod);
    }

    #endregion

    #region Monthly cadence

    [Fact]
    public void CounterReset_Monthly_ResetsOnMonthBoundary()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Monthly);
        
        var feb1 = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        
        habit.Complete(feb1);
        habit.Complete(feb1);
        Assert.Equal(2.0, habit.Count);

        // Same month (Feb 15)
        var feb15 = feb1.AddDays(14);
        habit.Complete(feb15);
        
        // Should not reset
        Assert.Equal(3.0, habit.Count);

        // Next month (Mar 1)
        var mar1 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        habit.Complete(mar1);
        
        // Should reset
        Assert.Equal(1.0, habit.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), habit.LastResetPeriod);
    }

    [Fact]
    public void CounterReset_Monthly_AlwaysResetsToDayOne()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Monthly);
        
        // Feb 28 (last day of month in non-leap year)
        var feb28 = new DateTimeOffset(2026, 2, 28, 12, 0, 0, TimeSpan.Zero);
        habit.Complete(feb28);
        
        Assert.Equal(new DateOnly(2026, 2, 1), habit.LastResetPeriod);

        // Mar 15 (mid-month)
        var mar15 = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        habit.Complete(mar15);
        
        Assert.Equal(new DateOnly(2026, 3, 1), habit.LastResetPeriod);
    }

    #endregion

    #region Custom increment amount

    [Fact]
    public void CounterReset_WithCustomIncrementAmount_IncrementsCorrectly()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        habit.SetIncrementAmount(2.5);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today);
        habit.Complete(today);
        Assert.Equal(5.0, habit.Count);

        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        
        // Resets to 0, then increments by 2.5
        Assert.Equal(2.5, habit.Count);
    }

    #endregion

    #region RefreshForCurrentPeriod

    [Fact]
    public void RefreshForCurrentPeriod_TriggersResetIfNeeded()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var now = DateTimeOffset.UtcNow;

        // Set period to yesterday to force reset on refresh
        var yesterday = now.AddDays(-1);
        habit.Complete(yesterday);
        habit.Complete(yesterday);
        var countYesterday = habit.Count;
        Assert.Equal(2.0, countYesterday);

        // Now refresh with current time (different period)
        habit.RefreshForCurrentPeriod();
        
        // Count should reset to 0 (no increment on refresh, only reset)
        Assert.Equal(0.0, habit.Count);
    }

    [Fact]
    public void RefreshForCurrentPeriod_DoesNotResetIfSamePeriod()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var now = DateTimeOffset.UtcNow;

        habit.Complete(now);
        var originalCount = habit.Count;
        var originalPeriod = habit.LastResetPeriod;

        // Refresh in same period
        habit.RefreshForCurrentPeriod();
        
        // Period and count should not change
        Assert.Equal(originalPeriod, habit.LastResetPeriod);
        Assert.Equal(originalCount, habit.Count);
    }

    #endregion

    #region Complete() method with reset

    [Fact]
    public void Complete_WithResetCadence_TriggersResetAndIncrement()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today);
        habit.Complete(today);
        Assert.Equal(2.0, habit.Count);

        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        
        // Should reset and increment
        Assert.Equal(1.0, habit.Count);
        Assert.Equal(tomorrow, habit.LastCompletedDate);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void CounterReset_IncrementDisabled_ResetStillHappens()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        habit.SetIncrementEnabled(false);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today); // Reset happens, but increment doesn't
        Assert.Equal(0.0, habit.Count);

        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        
        // Reset happens, but increment doesn't apply
        Assert.Equal(0.0, habit.Count);
    }

    [Fact]
    public void CounterReset_ChangingCadence_UsesNewCadence()
    {
        var habit = CreateHabit();
        habit.SetResetCadence(HabitResetCadence.Daily);
        var today = DateTimeOffset.UtcNow;

        habit.Complete(today);
        habit.Complete(today);
        Assert.Equal(2.0, habit.Count);

        // Change to weekly
        habit.SetResetCadence(HabitResetCadence.Weekly);
        
        var tomorrow = today.AddDays(1);
        habit.Complete(tomorrow);
        
        // Should not reset (same week)
        Assert.Equal(3.0, habit.Count);
    }

    [Fact]
    public void CounterReset_DefaultResetCadenceIsNever()
    {
        var habit = CreateHabit();
        
        Assert.Equal(HabitResetCadence.Never, habit.ResetCadence);
    }

    [Fact]
    public void CounterReset_DefaultCountIsZero()
    {
        var habit = CreateHabit();
        
        Assert.Equal(0.0, habit.Count);
    }

    #endregion

    #region Test helpers

    private static HabitTask CreateHabit()
    {
        var habit = new HabitTask();
        habit.UpdateTitle("Test Habit");
        return habit;
    }

    #endregion
}
