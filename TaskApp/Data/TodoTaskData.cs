using System;
using System.Collections.Generic;

namespace TaskApp.Data;

public class TodoTaskData : TaskData
{
    public DateTimeOffset? DueDate { get; set; }
    public List<ChecklistItemData> Checklist { get; set; } = new();
}
