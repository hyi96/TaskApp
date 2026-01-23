using System;
using System.Text.Json.Serialization;
using TaskApp.Models.Tasks;

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
    public System.Collections.Generic.List<string> Tags { get; set; } = new();
    public DateTimeOffset? LastCompletedDate { get; set; }
    public double GoldReward { get; set; }
    
    // Using property to ensure serialization of Type if needed, but the derived type handles the discriminator.
    // We can keep it for read-only via helper if necessary or just ignore it in serialization if derived types handle it.
}
