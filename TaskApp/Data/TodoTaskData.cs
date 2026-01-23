using System;
using System.Collections.Generic;
using TaskApp.Models.Tasks;

namespace TaskApp.Data;

public class TodoTaskData : TaskData
{
    public DateTimeOffset? DueDate { get; set; }
    public List<ChecklistItemData> Checklist { get; set; } = new();
}
