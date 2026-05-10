using System;

namespace TaskApp.Data;

public class ChecklistItemData
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
