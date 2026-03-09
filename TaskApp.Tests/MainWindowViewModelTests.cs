using System;
using System.IO;
using TaskApp.Services;
using TaskApp.ViewModels;
using Xunit;

namespace TaskApp.Tests;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public MainWindowViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void TodosFilter_ScheduledShowsOnlyActiveWithDueDate()
    {
        var vm = CreateViewModel();

        vm.NewTodoTitle = "Scheduled";
        vm.AddTodo();
        var scheduled = Assert.Single(vm.Todos, t => t.Title == "Scheduled");
        scheduled.SetDueDate(DateTimeOffset.UtcNow.AddDays(1));

        vm.NewTodoTitle = "Completed";
        vm.AddTodo();
        var completed = Assert.Single(vm.Todos, t => t.Title == "Completed");
        completed.Complete();

        vm.NewTodoTitle = "Unscheduled";
        vm.AddTodo();
        var unscheduled = Assert.Single(vm.Todos, t => t.Title == "Unscheduled");
        unscheduled.SetDueDate(null);

        vm.TodosFilter = "scheduled";

        Assert.Single(vm.Todos);
        Assert.Same(scheduled, vm.Todos[0]);
        Assert.DoesNotContain(completed, vm.Todos);
        Assert.DoesNotContain(unscheduled, vm.Todos);
    }

    [Fact]
    public void TodosSortMode_EarliestDueDateFirst()
    {
        var vm = CreateViewModel();

        vm.NewTodoTitle = "Later";
        vm.AddTodo();
        var later = Assert.Single(vm.Todos, t => t.Title == "Later");
        later.SetDueDate(DateTimeOffset.UtcNow.AddDays(3));

        vm.NewTodoTitle = "Sooner";
        vm.AddTodo();
        var sooner = Assert.Single(vm.Todos, t => t.Title == "Sooner");
        sooner.SetDueDate(DateTimeOffset.UtcNow.AddDays(1));

        vm.TodosSortMode = "Due date (earliest to latest)";

        Assert.Equal(2, vm.Todos.Count);
        Assert.Same(sooner, vm.Todos[0]);
        Assert.Same(later, vm.Todos[1]);
    }

    [Fact]
    public void TodosSortMode_LatestDueDateFirst_StillKeepsUndatedLast()
    {
        var vm = CreateViewModel();

        vm.NewTodoTitle = "Undated";
        vm.AddTodo();
        var undated = Assert.Single(vm.Todos, t => t.Title == "Undated");

        vm.NewTodoTitle = "Sooner";
        vm.AddTodo();
        var sooner = Assert.Single(vm.Todos, t => t.Title == "Sooner");
        sooner.SetDueDate(DateTimeOffset.UtcNow.AddDays(1));

        vm.NewTodoTitle = "Later";
        vm.AddTodo();
        var later = Assert.Single(vm.Todos, t => t.Title == "Later");
        later.SetDueDate(DateTimeOffset.UtcNow.AddDays(3));

        vm.TodosSortMode = "Due date (latest to earliest)";

        Assert.Equal(3, vm.Todos.Count);
        Assert.Same(later, vm.Todos[0]);
        Assert.Same(sooner, vm.Todos[1]);
        Assert.Same(undated, vm.Todos[2]);
    }

    [Fact]
    public void TodosSortMode_SameDueDate_FallsBackToTitleOrder()
    {
        var vm = CreateViewModel();
        var sharedDueDate = DateTimeOffset.UtcNow.AddDays(2);

        vm.NewTodoTitle = "Beta";
        vm.AddTodo();
        var beta = Assert.Single(vm.Todos, t => t.Title == "Beta");
        beta.SetDueDate(sharedDueDate);

        vm.NewTodoTitle = "Alpha";
        vm.AddTodo();
        var alpha = Assert.Single(vm.Todos, t => t.Title == "Alpha");
        alpha.SetDueDate(sharedDueDate);

        vm.TodosSortMode = "Due date (earliest to latest)";

        Assert.Equal(2, vm.Todos.Count);
        Assert.Same(alpha, vm.Todos[0]);
        Assert.Same(beta, vm.Todos[1]);
    }

    private MainWindowViewModel CreateViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }
}
