using System;
using System.Collections.ObjectModel;

namespace TaskApp.Models.Tasks;

public class TodoTask : TaskBase
{
    public DateTimeOffset? DueDate { get; internal set; }

    public ObservableCollection<ChecklistItem> Checklist { get; } = new();

    public override TaskType Type => TaskType.Todo;

    public void SetDueDate(DateTimeOffset? dueDate)
    {
        DueDate = dueDate;
    }
}
