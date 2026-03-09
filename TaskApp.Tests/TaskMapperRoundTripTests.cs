using System;
using System.Linq;
using TaskApp.Models.Tasks;
using TaskApp.Models.Tags;
using TaskApp.Services;

namespace TaskApp.Tests;

public class TaskMapperRoundTripTests
{
    #region HabitTask round-trip

    [Fact]
    public void HabitTask_RoundTrip_PreservesAllProperties()
    {
        var original = new HabitTask
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero)
        };
        original.UpdateTitle("Exercise");
        original.UpdateNotes("Daily workout");
        original.SetGoldReward(5.5);
        original.SetIncrementAmount(2.0);
        original.SetIncrementEnabled(true);
        original.SetDecrementEnabled(true);
        original.SetResetCadence(HabitResetCadence.Weekly);
        original.SetHidden(true);
        original.UpdateTags(new[] { new Tag("Health", Guid.NewGuid()) });
        original.Complete(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        original.Complete(new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.Zero));

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as HabitTask;

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Notes, restored.Notes);
        Assert.Equal(original.GoldReward, restored.GoldReward);
        Assert.Equal(original.Count, restored.Count);
        Assert.Equal(original.IncrementAmount, restored.IncrementAmount);
        Assert.Equal(original.IncrementEnabled, restored.IncrementEnabled);
        Assert.Equal(original.DecrementEnabled, restored.DecrementEnabled);
        Assert.Equal(original.ResetCadence, restored.ResetCadence);
        Assert.Equal(original.LastResetPeriod, restored.LastResetPeriod);
        Assert.Equal(original.IsHidden, restored.IsHidden);
        Assert.Equal(original.LastCompletedDate, restored.LastCompletedDate);
        Assert.Equal(original.Tags.Count, restored.Tags.Count);
        Assert.Equal(original.Tags[0].Id, restored.Tags[0].Id);
        Assert.Equal(original.Tags[0].Name, restored.Tags[0].Name);
    }

    [Fact]
    public void HabitTask_RoundTrip_DefaultValues()
    {
        var original = new HabitTask();
        original.UpdateTitle("Minimal");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as HabitTask;

        Assert.NotNull(restored);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(0, restored.Count);
        Assert.Equal(HabitResetCadence.Never, restored.ResetCadence);
        Assert.Null(restored.LastResetPeriod);
        Assert.False(restored.IsHidden);
    }

    #endregion

    #region DailyTask round-trip

    [Fact]
    public void DailyTask_RoundTrip_PreservesAllProperties()
    {
        var original = new DailyTask
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        original.UpdateTitle("Meditation");
        original.UpdateNotes("10 min");
        original.SetGoldReward(3.0);
        original.SetCadence(RepeatCadence.Weekly);
        original.SetRepeatEvery(2);
        original.SetCurrentStreak(5);
        original.SetAutocompleteTimeThreshold(TimeSpan.FromMinutes(30));
        original.SetHidden(false);
        original.UpdateTags(new[] { new Tag("Health", Guid.NewGuid()), new Tag("Focus", Guid.NewGuid()) });

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Notes, restored.Notes);
        Assert.Equal(original.GoldReward, restored.GoldReward);
        Assert.Equal(original.Cadence, restored.Cadence);
        Assert.Equal(original.RepeatEvery, restored.RepeatEvery);
        Assert.Equal(original.CurrentStreak, restored.CurrentStreak);
        Assert.Equal(original.BestStreak, restored.BestStreak);
        Assert.Equal(original.AutocompleteTimeThreshold, restored.AutocompleteTimeThreshold);
        Assert.Equal(original.IsHidden, restored.IsHidden);
        Assert.Equal(2, restored.Tags.Count);
    }

    [Fact]
    public void DailyTask_RoundTrip_PreservesStreakBonusRules()
    {
        var original = new DailyTask();
        original.UpdateTitle("Test");
        original.SetStreakBonusRules(new[]
        {
            new StreakBonusRule(5, 10),
            new StreakBonusRule(10, 25),
            new StreakBonusRule(20, 50)
        });

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(3, restored.StreakBonusRules.Count);
        Assert.Equal(5, restored.StreakBonusRules[0].StreakGoal);
        Assert.Equal(10, restored.StreakBonusRules[0].BonusPercent);
        Assert.Equal(20, restored.StreakBonusRules[2].StreakGoal);
    }

    [Fact]
    public void DailyTask_RoundTrip_NullAutocompleteThreshold()
    {
        var original = new DailyTask();
        original.UpdateTitle("Test");
        original.SetAutocompleteTimeThreshold(null);

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Null(restored.AutocompleteTimeThreshold);
    }

    [Fact]
    public void DailyTask_RoundTrip_RepeatEveryZeroInData_DefaultsToOne()
    {
        var original = new DailyTask();
        original.UpdateTitle("Test");

        var data = TaskMapper.ToData(original);

        // Simulate corrupted/legacy data with RepeatEvery = 0
        var dailyData = (TaskApp.Data.DailyTaskData)data;
        dailyData.RepeatEvery = 0;

        var restored = TaskMapper.ToModel(data) as DailyTask;

        Assert.NotNull(restored);
        Assert.Equal(1, restored.RepeatEvery);
    }

    #endregion

    #region TodoTask round-trip

    [Fact]
    public void TodoTask_RoundTrip_PreservesAllProperties()
    {
        var original = new TodoTask
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
        };
        original.UpdateTitle("Buy groceries");
        original.UpdateNotes("From the market");
        original.SetGoldReward(1.0);
        original.SetDueDate(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero));
        original.Complete(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as TodoTask;

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Notes, restored.Notes);
        Assert.Equal(original.GoldReward, restored.GoldReward);
        Assert.Equal(original.DueDate, restored.DueDate);
        Assert.Equal(original.LastCompletedDate, restored.LastCompletedDate);
    }

    [Fact]
    public void TodoTask_RoundTrip_PreservesChecklist()
    {
        var original = new TodoTask();
        original.UpdateTitle("Project");
        original.Checklist.Add(new ChecklistItem("Step 1") { IsCompleted = true });
        original.Checklist.Add(new ChecklistItem("Step 2") { IsCompleted = false });

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as TodoTask;

        Assert.NotNull(restored);
        Assert.Equal(2, restored.Checklist.Count);
        Assert.Equal("Step 1", restored.Checklist[0].Text);
        Assert.True(restored.Checklist[0].IsCompleted);
        Assert.Equal("Step 2", restored.Checklist[1].Text);
        Assert.False(restored.Checklist[1].IsCompleted);
    }

    [Fact]
    public void TodoTask_RoundTrip_PreservesChecklistItemIds()
    {
        var original = new TodoTask();
        original.UpdateTitle("Project");
        var item = new ChecklistItem("Step 1");
        original.Checklist.Add(item);

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as TodoTask;

        Assert.NotNull(restored);
        Assert.Equal(item.Id, restored.Checklist[0].Id);
    }

    [Fact]
    public void TodoTask_RoundTrip_EmptyChecklist()
    {
        var original = new TodoTask();
        original.UpdateTitle("Simple");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as TodoTask;

        Assert.NotNull(restored);
        Assert.Empty(restored.Checklist);
    }

    [Fact]
    public void TodoTask_RoundTrip_NullDueDate()
    {
        var original = new TodoTask();
        original.UpdateTitle("No due date");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data) as TodoTask;

        Assert.NotNull(restored);
        Assert.Null(restored.DueDate);
    }

    #endregion

    #region Tags round-trip

    [Fact]
    public void Tags_RoundTrip_PreservesIdAndName()
    {
        var tagId = Guid.NewGuid();
        var original = new HabitTask();
        original.UpdateTitle("Test");
        original.UpdateTags(new[] { new Tag("Urgent", tagId) });

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.Single(restored.Tags);
        Assert.Equal(tagId, restored.Tags[0].Id);
        Assert.Equal("Urgent", restored.Tags[0].Name);
    }

    [Fact]
    public void Tags_RoundTrip_EmptyTags()
    {
        var original = new HabitTask();
        original.UpdateTitle("Test");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.Empty(restored.Tags);
    }

    [Fact]
    public void Tags_RoundTrip_MultipleTags()
    {
        var original = new TodoTask();
        original.UpdateTitle("Test");
        original.UpdateTags(new[]
        {
            new Tag("A", Guid.NewGuid()),
            new Tag("B", Guid.NewGuid()),
            new Tag("C", Guid.NewGuid())
        });

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.Equal(3, restored.Tags.Count);
    }

    #endregion

    #region Type discriminator

    [Fact]
    public void ToModel_HabitData_ReturnsHabitTask()
    {
        var original = new HabitTask();
        original.UpdateTitle("Habit");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.IsType<HabitTask>(restored);
        Assert.Equal(TaskType.Habit, restored.Type);
    }

    [Fact]
    public void ToModel_DailyData_ReturnsDailyTask()
    {
        var original = new DailyTask();
        original.UpdateTitle("Daily");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.IsType<DailyTask>(restored);
        Assert.Equal(TaskType.Daily, restored.Type);
    }

    [Fact]
    public void ToModel_TodoData_ReturnsTodoTask()
    {
        var original = new TodoTask();
        original.UpdateTitle("Todo");

        var data = TaskMapper.ToData(original);
        var restored = TaskMapper.ToModel(data);

        Assert.IsType<TodoTask>(restored);
        Assert.Equal(TaskType.Todo, restored.Type);
    }

    #endregion
}
