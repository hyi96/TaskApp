using System;
using TaskApp.Models.Tasks;

namespace TaskApp.Tests;

public class TodoTaskTests
{
    #region Completion

    [Fact]
    public void Complete_SetsLastCompletedDate()
    {
        var todo = CreateTodo();
        var now = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        todo.Complete(now);

        Assert.Equal(now, todo.LastCompletedDate);
    }

    [Fact]
    public void Complete_MarksAsRewardGoalMet()
    {
        var todo = CreateTodo();

        Assert.False(todo.IsRewardGoalMet);

        todo.Complete();

        Assert.True(todo.IsRewardGoalMet);
    }

    [Fact]
    public void Complete_WithoutTimestamp_UsesCurrentTime()
    {
        var todo = CreateTodo();
        var before = DateTimeOffset.UtcNow;

        todo.Complete();

        Assert.NotNull(todo.LastCompletedDate);
        Assert.True(todo.LastCompletedDate >= before);
    }

    #endregion

    #region Due date

    [Fact]
    public void SetDueDate_StoresValue()
    {
        var todo = CreateTodo();
        var dueDate = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

        todo.SetDueDate(dueDate);

        Assert.Equal(dueDate, todo.DueDate);
    }

    [Fact]
    public void SetDueDate_Null_ClearsValue()
    {
        var todo = CreateTodo();
        todo.SetDueDate(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        todo.SetDueDate(null);

        Assert.Null(todo.DueDate);
    }

    [Fact]
    public void DueDate_DefaultIsNull()
    {
        var todo = CreateTodo();

        Assert.Null(todo.DueDate);
    }

    #endregion

    #region Checklist

    [Fact]
    public void Checklist_DefaultIsEmpty()
    {
        var todo = CreateTodo();

        Assert.Empty(todo.Checklist);
    }

    [Fact]
    public void Checklist_AddItem_Persists()
    {
        var todo = CreateTodo();
        var item = new ChecklistItem("Step 1");

        todo.Checklist.Add(item);

        Assert.Single(todo.Checklist);
        Assert.Equal("Step 1", todo.Checklist[0].Text);
    }

    [Fact]
    public void Checklist_ItemCompletion_IsTracked()
    {
        var todo = CreateTodo();
        var item = new ChecklistItem("Step 1");
        todo.Checklist.Add(item);

        Assert.False(item.IsCompleted);

        item.IsCompleted = true;

        Assert.True(item.IsCompleted);
    }

    [Fact]
    public void Checklist_MultipleItems_IndependentCompletion()
    {
        var todo = CreateTodo();
        var item1 = new ChecklistItem("Step 1");
        var item2 = new ChecklistItem("Step 2");
        todo.Checklist.Add(item1);
        todo.Checklist.Add(item2);

        item1.IsCompleted = true;

        Assert.True(item1.IsCompleted);
        Assert.False(item2.IsCompleted);
    }

    [Fact]
    public void Checklist_RemoveItem_Works()
    {
        var todo = CreateTodo();
        var item = new ChecklistItem("Step 1");
        todo.Checklist.Add(item);
        todo.Checklist.Remove(item);

        Assert.Empty(todo.Checklist);
    }

    [Fact]
    public void ChecklistItem_UpdateText_ChangesValue()
    {
        var item = new ChecklistItem("Original");
        item.Text = "Updated";

        Assert.Equal("Updated", item.Text);
    }

    [Fact]
    public void ChecklistItem_HasUniqueId()
    {
        var item1 = new ChecklistItem("A");
        var item2 = new ChecklistItem("B");

        Assert.NotEqual(item1.Id, item2.Id);
    }

    #endregion

    #region Task properties

    [Fact]
    public void Type_ReturnsTodo()
    {
        var todo = CreateTodo();

        Assert.Equal(TaskType.Todo, todo.Type);
    }

    [Fact]
    public void UpdateTitle_SetsTitle()
    {
        var todo = CreateTodo();
        todo.UpdateTitle("New Title");

        Assert.Equal("New Title", todo.Title);
    }

    [Fact]
    public void UpdateTitle_Null_SetsEmpty()
    {
        var todo = CreateTodo();
        todo.UpdateTitle(null!);

        Assert.Equal(string.Empty, todo.Title);
    }

    [Fact]
    public void UpdateNotes_SetsNotes()
    {
        var todo = CreateTodo();
        todo.UpdateNotes("Some notes");

        Assert.Equal("Some notes", todo.Notes);
    }

    [Fact]
    public void SetGoldReward_NegativeValue_ClampsToZero()
    {
        var todo = CreateTodo();
        todo.SetGoldReward(-5);

        Assert.Equal(0, todo.GoldReward);
    }

    [Fact]
    public void SetGoldReward_PositiveValue_Sets()
    {
        var todo = CreateTodo();
        todo.SetGoldReward(7.5);

        Assert.Equal(7.5, todo.GoldReward);
    }

    [Fact]
    public void IsHidden_DefaultIsFalse()
    {
        var todo = CreateTodo();

        Assert.False(todo.IsHidden);
    }

    [Fact]
    public void SetHidden_ChangesValue()
    {
        var todo = CreateTodo();
        todo.SetHidden(true);

        Assert.True(todo.IsHidden);
    }

    #endregion

    #region Tags

    [Fact]
    public void UpdateTags_ReplacesTags()
    {
        var todo = CreateTodo();
        var tag1 = new TaskApp.Models.Tags.Tag("Health");
        var tag2 = new TaskApp.Models.Tags.Tag("Work");

        todo.UpdateTags(new[] { tag1, tag2 });

        Assert.Equal(2, todo.Tags.Count);

        todo.UpdateTags(new[] { tag1 });

        Assert.Single(todo.Tags);
    }

    [Fact]
    public void UpdateTags_Null_ClearsTags()
    {
        var todo = CreateTodo();
        var tag = new TaskApp.Models.Tags.Tag("Health");
        todo.UpdateTags(new[] { tag });

        todo.UpdateTags(null!);

        Assert.Empty(todo.Tags);
    }

    #endregion

    #region Helpers

    private static TodoTask CreateTodo()
    {
        var todo = new TodoTask();
        todo.UpdateTitle("Test Todo");
        return todo;
    }

    #endregion
}
