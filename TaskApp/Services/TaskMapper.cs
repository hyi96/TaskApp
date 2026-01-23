using System;
using System.Linq;
using TaskApp.Data;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public static class TaskMapper
{
    public static TaskBase ToModel(TaskData data)
    {
        TaskBase task;
        switch (data)
        {
            case TodoTaskData todoData:
                var todoTask = new TodoTask
                {
                    Id = todoData.Id,
                    CreatedAt = todoData.CreatedAt,
                    DueDate = todoData.DueDate
                };
                
                // Load checklist items
                if (todoData.Checklist != null)
                {
                    foreach (var itemData in todoData.Checklist)
                    {
                        var checklistItem = new ChecklistItem(itemData.Text)
                        {
                            Id = itemData.Id,
                            IsCompleted = itemData.IsCompleted
                        };
                        todoTask.Checklist.Add(checklistItem);
                    }
                }
                
                task = todoTask;
                break;
            case DailyTaskData dailyData:
                task = new DailyTask
                {
                    Id = dailyData.Id,
                    CreatedAt = dailyData.CreatedAt,
                    Cadence = dailyData.Cadence,
                    RepeatEvery = dailyData.RepeatEvery == 0 ? 1 : dailyData.RepeatEvery,
                    CurrentStreak = dailyData.CurrentStreak,
                    BestStreak = dailyData.BestStreak,
                    StreakGoal = dailyData.StreakGoal,
                    LastCompletionPeriod = dailyData.LastCompletionPeriod,
                    RewardGoalFulfilled = dailyData.RewardGoalFulfilled
                };
                break;
            case HabitTaskData habitData:
                task = new HabitTask
                {
                    Id = habitData.Id,
                    CreatedAt = habitData.CreatedAt,
                    Count = habitData.Count,
                    IncrementAmount = habitData.IncrementAmount,
                    IncrementEnabled = habitData.IncrementEnabled,
                    DecrementEnabled = habitData.DecrementEnabled,
                    ResetCadence = habitData.ResetCadence,
                    LastResetPeriod = habitData.LastResetPeriod
                };
                break;
            default:
                throw new ArgumentException($"Unknown task data type: {data?.GetType().Name}");
        }

        task.Title = data.Title;
        task.Notes = data.Notes;
        if (data.Tags != null)
        {
            task.Tags = data.Tags;
        }
        task.LastCompletedDate = data.LastCompletedDate;
        task.GoldReward = data.GoldReward;

        return task;
    }

    public static TaskData ToData(TaskBase model)
    {
        TaskData data;
        switch (model)
        {
            case TodoTask todo:
                var todoData = new TodoTaskData
                {
                    DueDate = todo.DueDate
                };
                
                // Save checklist items
                todoData.Checklist = todo.Checklist.Select(item => new ChecklistItemData
                {
                    Id = item.Id,
                    Text = item.Text,
                    IsCompleted = item.IsCompleted
                }).ToList();
                
                data = todoData;
                break;
            case DailyTask daily:
                data = new DailyTaskData
                {
                    Cadence = daily.Cadence,
                    RepeatEvery = daily.RepeatEvery,
                    CurrentStreak = daily.CurrentStreak,
                    BestStreak = daily.BestStreak,
                    StreakGoal = daily.StreakGoal,
                    LastCompletionPeriod = daily.LastCompletionPeriod,
                    RewardGoalFulfilled = daily.RewardGoalFulfilled
                };
                break;
            case HabitTask habit:
                data = new HabitTaskData
                {
                    Count = habit.Count,
                    IncrementAmount = habit.IncrementAmount,
                    IncrementEnabled = habit.IncrementEnabled,
                    DecrementEnabled = habit.DecrementEnabled,
                    ResetCadence = habit.ResetCadence,
                    LastResetPeriod = habit.LastResetPeriod
                };
                break;
            default:
                throw new ArgumentException($"Unknown task model type: {model?.GetType().Name}");
        }

        data.Id = model.Id;
        data.CreatedAt = model.CreatedAt;
        data.Title = model.Title;
        data.Notes = model.Notes;
        data.Tags = model.Tags;
        data.LastCompletedDate = model.LastCompletedDate;
        data.GoldReward = model.GoldReward;

        return data;
    }
}
