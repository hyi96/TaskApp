using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaskApp.Data;

[JsonDerivedType(typeof(TodoTaskData), typeDiscriminator: "Todo")]
[JsonDerivedType(typeof(DailyTaskData), typeDiscriminator: "Daily")]
[JsonDerivedType(typeof(HabitTaskData), typeDiscriminator: "Habit")]
public abstract class TaskData
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<TagData> Tags { get; set; } = new();
    public DateTimeOffset? LastCompletedDate { get; set; }
    public double GoldReward { get; set; }
}
