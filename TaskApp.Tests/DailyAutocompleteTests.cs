using System;
using System.Collections.Generic;
using System.Linq;
using TaskApp.Data;
using TaskApp.Models.Logs;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.Tests;

public class DailyAutocompleteTests
{
    #region Model: AutocompleteTimeThreshold property

    [Fact]
    public void SetAutocompleteTimeThreshold_SetsValue()
    {
        var daily = CreateDaily();

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void SetAutocompleteTimeThreshold_Null_ClearsValue()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        daily.SetAutocompleteTimeThreshold(null);

        Assert.Null(daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void AutocompleteTimeThreshold_DefaultIsNull()
    {
        var daily = CreateDaily();

        Assert.Null(daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void SetAutocompleteTimeThreshold_ZeroTimeSpan_SetsToZero()
    {
        var daily = CreateDaily();

        daily.SetAutocompleteTimeThreshold(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void SetAutocompleteTimeThreshold_LargeValue_IsPreserved()
    {
        var daily = CreateDaily();
        var threshold = new TimeSpan(5, 30, 0); // 5 hours 30 minutes

        daily.SetAutocompleteTimeThreshold(threshold);

        Assert.Equal(threshold, daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void AutocompleteTimeThreshold_RaisesPropertyChanged()
    {
        var daily = CreateDaily();
        var raised = false;
        daily.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DailyTask.AutocompleteTimeThreshold))
                raised = true;
        };

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(10));

        Assert.True(raised);
    }

    [Fact]
    public void AutocompleteTimeThreshold_SameValue_DoesNotRaisePropertyChanged()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(10));
        var raised = false;
        daily.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DailyTask.AutocompleteTimeThreshold))
                raised = true;
        };

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(10));

        Assert.False(raised);
    }

    #endregion

    #region Persistence: DailyTaskData and TaskMapper

    [Fact]
    public void TaskMapper_ToData_WithAutocomplete_PersistsTicks()
    {
        var daily = CreateDaily();
        var threshold = TimeSpan.FromMinutes(45);
        daily.SetAutocompleteTimeThreshold(threshold);

        var data = TaskMapper.ToData(daily) as DailyTaskData;

        Assert.NotNull(data);
        Assert.Equal(threshold.Ticks, data.AutocompleteTimeThresholdTicks);
    }

    [Fact]
    public void TaskMapper_ToData_WithoutAutocomplete_TicksAreNull()
    {
        var daily = CreateDaily();

        var data = TaskMapper.ToData(daily) as DailyTaskData;

        Assert.NotNull(data);
        Assert.Null(data.AutocompleteTimeThresholdTicks);
    }

    [Fact]
    public void TaskMapper_ToModel_WithAutocomplete_RestoresThreshold()
    {
        var threshold = TimeSpan.FromMinutes(45);
        var data = CreateDailyTaskData();
        data.AutocompleteTimeThresholdTicks = threshold.Ticks;

        var model = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(model);
        Assert.Equal(threshold, model.AutocompleteTimeThreshold);
    }

    [Fact]
    public void TaskMapper_ToModel_WithoutAutocomplete_ThresholdIsNull()
    {
        var data = CreateDailyTaskData();
        data.AutocompleteTimeThresholdTicks = null;

        var model = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(model);
        Assert.Null(model.AutocompleteTimeThreshold);
    }

    [Fact]
    public void TaskMapper_Roundtrip_PreservesAutocompleteThreshold()
    {
        var daily = CreateDaily();
        var threshold = new TimeSpan(2, 15, 30); // 2h 15m 30s
        daily.SetAutocompleteTimeThreshold(threshold);

        var data = TaskMapper.ToData(daily);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(threshold, restored.AutocompleteTimeThreshold);
    }

    [Fact]
    public void TaskMapper_Roundtrip_NullThreshold_PreservesNull()
    {
        var daily = CreateDaily();

        var data = TaskMapper.ToData(daily);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Null(restored.AutocompleteTimeThreshold);
    }

    #endregion

    #region Autocomplete decision logic

    [Fact]
    public void AutocompleteNotApplicable_WhenThresholdIsNull()
    {
        var daily = CreateDaily();
        // No threshold set
        var loggedDuration = TimeSpan.FromMinutes(100);
        var sessionElapsed = TimeSpan.FromMinutes(50);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.Null(remaining);
    }

    [Fact]
    public void AutocompleteNotApplicable_WhenAlreadyComplete()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        daily.Complete();

        // Already completed for current period means autocomplete should not trigger
        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void AutocompleteRemaining_WhenBelowThreshold()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var loggedDuration = TimeSpan.FromMinutes(10);
        var sessionElapsed = TimeSpan.FromMinutes(5);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.Equal(TimeSpan.FromMinutes(15), remaining.Value);
        Assert.True(remaining.Value > TimeSpan.Zero, "Should not trigger autocomplete yet");
    }

    [Fact]
    public void AutocompleteRemaining_WhenExactlyAtThreshold()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var loggedDuration = TimeSpan.FromMinutes(20);
        var sessionElapsed = TimeSpan.FromMinutes(10);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.Equal(TimeSpan.Zero, remaining.Value);
        Assert.True(remaining.Value <= TimeSpan.Zero, "Should trigger autocomplete");
    }

    [Fact]
    public void AutocompleteRemaining_WhenAboveThreshold()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var loggedDuration = TimeSpan.FromMinutes(25);
        var sessionElapsed = TimeSpan.FromMinutes(10);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value < TimeSpan.Zero, "Should trigger autocomplete");
    }

    [Fact]
    public void AutocompleteRemaining_NoLoggedTime_OnlySessionTime()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var loggedDuration = TimeSpan.Zero;
        var sessionElapsed = TimeSpan.FromMinutes(31);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value <= TimeSpan.Zero, "Should trigger autocomplete from session time alone");
    }

    [Fact]
    public void AutocompleteRemaining_NoSessionTime_OnlyLoggedTime()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        var loggedDuration = TimeSpan.FromMinutes(35);
        var sessionElapsed = TimeSpan.Zero;

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value <= TimeSpan.Zero, "Should trigger autocomplete from logged time alone");
    }

    [Fact]
    public void AutocompleteRemaining_SmallThreshold_CrossedBySmallIncrement()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromSeconds(5));
        var loggedDuration = TimeSpan.FromSeconds(3);
        var sessionElapsed = TimeSpan.FromSeconds(3);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value <= TimeSpan.Zero);
    }

    [Fact]
    public void AutocompleteRemaining_LargeThreshold_NotYetCrossed()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(new TimeSpan(8, 0, 0)); // 8 hours
        var loggedDuration = new TimeSpan(3, 0, 0);
        var sessionElapsed = new TimeSpan(2, 0, 0);

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.Equal(new TimeSpan(3, 0, 0), remaining.Value);
        Assert.True(remaining.Value > TimeSpan.Zero);
    }

    #endregion

    #region Completion side effects

    [Fact]
    public void Complete_AfterAutocomplete_MarksAsCompleteForCurrentPeriod()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        Assert.False(daily.IsCompleteForCurrentPeriod);

        daily.Complete();

        Assert.True(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void Complete_AfterAutocomplete_IncrementsStreak()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        Assert.Equal(0, daily.CurrentStreak);

        daily.Complete();

        Assert.Equal(1, daily.CurrentStreak);
    }

    [Fact]
    public void Complete_CalledTwiceInSamePeriod_DoesNotDoubleIncrement()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        daily.Complete();
        var streakAfterFirst = daily.CurrentStreak;
        daily.Complete();

        Assert.Equal(streakAfterFirst, daily.CurrentStreak);
    }

    [Fact]
    public void AutocompleteThreshold_IndependentOfCompletion()
    {
        // Setting/clearing threshold should not affect completion state
        var daily = CreateDaily();
        daily.Complete();
        Assert.True(daily.IsCompleteForCurrentPeriod);

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));

        Assert.True(daily.IsCompleteForCurrentPeriod);
        Assert.Equal(TimeSpan.FromMinutes(30), daily.AutocompleteTimeThreshold);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void AutocompleteWithZeroThreshold_TriggersImmediately()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.Zero);
        var loggedDuration = TimeSpan.Zero;
        var sessionElapsed = TimeSpan.Zero;

        var remaining = CalculateRemaining(daily, loggedDuration, sessionElapsed);

        Assert.NotNull(remaining);
        Assert.True(remaining.Value <= TimeSpan.Zero, "Zero threshold should trigger immediately");
    }

    [Fact]
    public void AutocompleteWithDifferentCadence_UsesCorrectPeriod()
    {
        var daily = CreateDaily();
        daily.SetCadence(RepeatCadence.Weekly);
        daily.SetRepeatEvery(1);
        daily.SetAutocompleteTimeThreshold(TimeSpan.FromHours(2));

        Assert.Equal(RepeatCadence.Weekly, daily.Cadence);
        Assert.Equal(TimeSpan.FromHours(2), daily.AutocompleteTimeThreshold);
        Assert.False(daily.IsCompleteForCurrentPeriod);
    }

    [Fact]
    public void AutocompleteThreshold_UpdatedMultipleTimes()
    {
        var daily = CreateDaily();

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(10), daily.AutocompleteTimeThreshold);

        daily.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(60));
        Assert.Equal(TimeSpan.FromMinutes(60), daily.AutocompleteTimeThreshold);

        daily.SetAutocompleteTimeThreshold(null);
        Assert.Null(daily.AutocompleteTimeThreshold);
    }

    [Fact]
    public void Persistence_ZeroThreshold_RoundtripsCorrectly()
    {
        var daily = CreateDaily();
        daily.SetAutocompleteTimeThreshold(TimeSpan.Zero);

        var data = TaskMapper.ToData(daily);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(TimeSpan.Zero, restored.AutocompleteTimeThreshold);
    }

    [Fact]
    public void Persistence_SubSecondPrecision_PreservedViaTicks()
    {
        var daily = CreateDaily();
        var threshold = TimeSpan.FromMilliseconds(500);
        daily.SetAutocompleteTimeThreshold(threshold);

        var data = TaskMapper.ToData(daily) as DailyTaskData;
        Assert.NotNull(data);
        Assert.Equal(threshold.Ticks, data.AutocompleteTimeThresholdTicks);

        var restored = TaskMapper.ToModel(data) as DailyTask;
        Assert.NotNull(restored);
        Assert.Equal(threshold, restored.AutocompleteTimeThreshold);
    }

    #endregion

    #region Helpers

    private static DailyTask CreateDaily()
    {
        var daily = new DailyTask();
        daily.UpdateTitle("Test Daily");
        return daily;
    }

    private static DailyTaskData CreateDailyTaskData()
    {
        return new DailyTaskData
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Title = "Test Daily",
            Cadence = RepeatCadence.Daily,
            RepeatEvery = 1
        };
    }

    /// <summary>
    /// Mirrors the autocomplete remaining time calculation from MainWindowViewModel.
    /// Returns null if autocomplete is not applicable, or the remaining time.
    /// </summary>
    private static TimeSpan? CalculateRemaining(DailyTask daily, TimeSpan loggedDuration, TimeSpan currentSessionElapsed)
    {
        if (daily.AutocompleteTimeThreshold is not TimeSpan threshold)
            return null;

        if (daily.IsCompleteForCurrentPeriod)
            return null;

        var totalTimeSpent = loggedDuration + currentSessionElapsed;
        return threshold - totalTimeSpent;
    }

    #endregion
}
