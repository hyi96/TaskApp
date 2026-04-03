using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tags;
using TaskApp.Models.Tasks;
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

    #region Todo filter / sort (existing)

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

    [Fact]
    public void TodosFilter_Active_HidesCompleted()
    {
        var vm = CreateViewModel();

        vm.NewTodoTitle = "Active";
        vm.AddTodo();
        vm.NewTodoTitle = "Done";
        vm.AddTodo();
        var done = vm.Todos.First(t => t.Title == "Done");
        done.Complete();

        vm.TodosFilter = "active";

        Assert.Single(vm.Todos);
        Assert.Equal("Active", vm.Todos[0].Title);
    }

    [Fact]
    public void TodosFilter_Completed_ShowsOnlyCompleted()
    {
        var vm = CreateViewModel();

        vm.NewTodoTitle = "Active";
        vm.AddTodo();
        vm.NewTodoTitle = "Done";
        vm.AddTodo();
        var done = vm.Todos.First(t => t.Title == "Done");
        done.Complete();

        vm.TodosFilter = "completed";

        Assert.Single(vm.Todos);
        Assert.Equal("Done", vm.Todos[0].Title);
    }

    #endregion

    #region Habit add / delete / filter / sort

    [Fact]
    public void AddHabit_AddsToCollection()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();

        Assert.Single(vm.Habits);
        Assert.Equal("Exercise", vm.Habits[0].Title);
        Assert.Equal(string.Empty, vm.NewHabitTitle);
    }

    [Fact]
    public void AddHabit_IgnoresEmptyTitle()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "";
        vm.AddHabit();

        Assert.Empty(vm.Habits);
    }

    [Fact]
    public void AddHabit_IgnoresWhitespaceTitle()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "   ";
        vm.AddHabit();

        Assert.Empty(vm.Habits);
    }

    [Fact]
    public void DeleteHabit_RemovesFromCollection()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "ToDelete";
        vm.AddHabit();
        var habit = vm.Habits[0];

        vm.DeleteHabit(habit);

        Assert.Empty(vm.Habits);
    }

    [Fact]
    public void HabitsFilter_Hidden_ShowsOnlyHidden()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Visible";
        vm.AddHabit();
        vm.NewHabitTitle = "Hidden";
        vm.AddHabit();
        var hidden = vm.Habits.First(h => h.Title == "Hidden");
        hidden.SetHidden(true);

        vm.HabitsFilter = "hidden";

        Assert.Single(vm.Habits);
        Assert.Equal("Hidden", vm.Habits[0].Title);
    }

    [Fact]
    public void HabitsSort_CountHighToLow()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Low";
        vm.AddHabit();
        vm.NewHabitTitle = "High";
        vm.AddHabit();
        var low = vm.Habits.First(h => h.Title == "Low");
        var high = vm.Habits.First(h => h.Title == "High");
        low.Increment(); // count = 1
        high.Increment();
        high.Increment();
        high.Increment(); // count = 3

        vm.HabitsSortMode = "Count (high to low)";

        Assert.Equal("High", vm.Habits[0].Title);
        Assert.Equal("Low", vm.Habits[1].Title);
    }

    [Fact]
    public void HabitsSort_NameAZ()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Zebra";
        vm.AddHabit();
        vm.NewHabitTitle = "Apple";
        vm.AddHabit();

        vm.HabitsSortMode = "Name (A-Z)";

        Assert.Equal("Apple", vm.Habits[0].Title);
        Assert.Equal("Zebra", vm.Habits[1].Title);
    }

    #endregion

    #region Daily add / delete / filter

    [Fact]
    public void AddDaily_AddsToCollection()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Meditate";
        vm.AddDaily();

        Assert.Single(vm.Dailies);
        Assert.Equal("Meditate", vm.Dailies[0].Title);
        Assert.Equal(string.Empty, vm.NewDailyTitle);
    }

    [Fact]
    public void AddDaily_IgnoresEmptyTitle()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "";
        vm.AddDaily();

        Assert.Empty(vm.Dailies);
    }

    [Fact]
    public void DeleteDaily_RemovesFromCollection()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "ToDelete";
        vm.AddDaily();
        var daily = vm.Dailies[0];

        vm.DeleteDaily(daily);

        Assert.Empty(vm.Dailies);
    }

    [Fact]
    public void DailiesFilter_Due_ShowsOnlyIncomplete()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Due";
        vm.AddDaily();
        vm.NewDailyTitle = "Done";
        vm.AddDaily();
        var done = vm.Dailies.First(d => d.Title == "Done");
        done.Complete();

        vm.DailiesFilter = "due";

        Assert.Single(vm.Dailies);
        Assert.Equal("Due", vm.Dailies[0].Title);
    }

    [Fact]
    public void DailiesFilter_NotDue_ShowsOnlyComplete()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "Due";
        vm.AddDaily();
        vm.NewDailyTitle = "Done";
        vm.AddDaily();
        var done = vm.Dailies.First(d => d.Title == "Done");
        done.Complete();

        vm.DailiesFilter = "not due";

        Assert.Single(vm.Dailies);
        Assert.Equal("Done", vm.Dailies[0].Title);
    }

    #endregion

    #region Reward add / delete / filter / claim

    [Fact]
    public void AddReward_AddsToCollection()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Pizza";
        vm.AddReward();

        Assert.Single(vm.Rewards);
        Assert.Equal("Pizza", vm.Rewards[0].Title);
        Assert.Equal(string.Empty, vm.NewRewardTitle);
    }

    [Fact]
    public void AddReward_IgnoresEmptyTitle()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "";
        vm.AddReward();

        Assert.Empty(vm.Rewards);
    }

    [Fact]
    public void DeleteReward_RemovesFromCollection()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "ToDelete";
        vm.AddReward();
        var reward = vm.Rewards[0];

        vm.DeleteReward(reward);

        Assert.Empty(vm.Rewards);
    }

    [Fact]
    public void RewardsFilter_OneTime_ExcludesRepeatable()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Once";
        vm.AddReward();
        vm.NewRewardTitle = "Repeat";
        vm.AddReward();
        var repeat = vm.Rewards.First(r => r.Title == "Repeat");
        repeat.SetRepeatable(true);

        vm.RewardsFilter = "one-time";

        Assert.Single(vm.Rewards);
        Assert.Equal("Once", vm.Rewards[0].Title);
    }

    [Fact]
    public void RewardsFilter_Repeatable_ExcludesOneTime()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Once";
        vm.AddReward();
        vm.NewRewardTitle = "Repeat";
        vm.AddReward();
        var repeat = vm.Rewards.First(r => r.Title == "Repeat");
        repeat.SetRepeatable(true);

        vm.RewardsFilter = "repeatable";

        Assert.Single(vm.Rewards);
        Assert.Equal("Repeat", vm.Rewards[0].Title);
    }

    [Fact]
    public async Task ClaimReward_DeductsGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10;
        vm.NewRewardTitle = "Snack";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(3);

        await vm.ClaimRewardAsync(reward);

        Assert.Equal(7, vm.User.Gold, precision: 2);
        Assert.True(reward.IsClaimed);
    }

    [Fact]
    public async Task ClaimReward_DoesNotDeductWhenInsufficientGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 1;
        vm.NewRewardTitle = "Expensive";
        vm.AddReward();
        var reward = vm.Rewards[0];
        reward.SetGoldCost(100);

        await vm.ClaimRewardAsync(reward);

        Assert.Equal(1, vm.User.Gold, precision: 2);
        Assert.False(reward.IsClaimed);
    }

    #endregion

    #region Gold management

    [Fact]
    public void AddGold_IncreasesGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 5;

        vm.AddGold(3.5);

        Assert.Equal(8.5, vm.User.Gold, precision: 2);
    }

    [Fact]
    public void RemoveGold_DecreasesGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 10;

        vm.RemoveGold(4);

        Assert.Equal(6, vm.User.Gold, precision: 2);
    }

    [Fact]
    public void RemoveGold_DoesNothing_WhenInsufficientGold()
    {
        var vm = CreateViewModel();
        vm.User.Gold = 2;

        vm.RemoveGold(5);

        Assert.Equal(2, vm.User.Gold, precision: 2);
    }

    #endregion

    #region Search query

    [Fact]
    public void SearchQuery_FiltersTodosAndHabits()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Morning run";
        vm.AddHabit();
        vm.NewHabitTitle = "Evening yoga";
        vm.AddHabit();
        vm.NewTodoTitle = "Buy running shoes";
        vm.AddTodo();

        vm.SearchQuery = "run";

        Assert.Single(vm.Habits);
        Assert.Equal("Morning run", vm.Habits[0].Title);
        Assert.Single(vm.Todos);
        Assert.Equal("Buy running shoes", vm.Todos[0].Title);
    }

    [Fact]
    public void SearchQuery_IsCaseInsensitive()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();

        vm.SearchQuery = "EXERCISE";

        Assert.Single(vm.Habits);
    }

    [Fact]
    public void SearchQuery_ClearingRestoresAll()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "A";
        vm.AddHabit();
        vm.NewHabitTitle = "B";
        vm.AddHabit();
        vm.SearchQuery = "A";
        Assert.Single(vm.Habits);

        vm.SearchQuery = "";

        Assert.Equal(2, vm.Habits.Count);
    }

    #endregion

    #region Tag-based filtering

    [Fact]
    public void TagFilter_ShowsOnlyMatchingItems()
    {
        var vm = CreateViewModel();
        var healthTag = new Tag("Health");
        var workTag = new Tag("Work");
        var selHealth = new SelectableTag(healthTag);
        var selWork = new SelectableTag(workTag);
        vm.AvailableTags.Add(selHealth);
        vm.AvailableTags.Add(selWork);

        vm.NewHabitTitle = "Exercise";
        vm.AddHabit();
        vm.Habits[0].UpdateTags(new[] { healthTag });

        vm.NewHabitTitle = "Code review";
        vm.AddHabit();
        vm.Habits.First(h => h.Title == "Code review").UpdateTags(new[] { workTag });

        selHealth.IsSelected = true;
        vm.RefreshFilter();

        Assert.Single(vm.Habits);
        Assert.Equal("Exercise", vm.Habits[0].Title);
    }

    #endregion

    #region Hidden items

    [Fact]
    public void HiddenItems_ExcludedFromAllFilter()
    {
        var vm = CreateViewModel();
        vm.NewTodoTitle = "Visible";
        vm.AddTodo();
        vm.NewTodoTitle = "HiddenItem";
        vm.AddTodo();
        var hidden = vm.Todos.First(t => t.Title == "HiddenItem");
        hidden.SetHidden(true);

        vm.TodosFilter = "active";

        Assert.Single(vm.Todos);
        Assert.Equal("Visible", vm.Todos[0].Title);
    }

    [Fact]
    public void HiddenFilter_ShowsOnlyHidden()
    {
        var vm = CreateViewModel();
        vm.NewTodoTitle = "Visible";
        vm.AddTodo();
        vm.NewTodoTitle = "HiddenItem";
        vm.AddTodo();
        var hidden = vm.Todos.First(t => t.Title == "HiddenItem");
        hidden.SetHidden(true);

        vm.TodosFilter = "hidden";

        Assert.Single(vm.Todos);
        Assert.Equal("HiddenItem", vm.Todos[0].Title);
    }

    #endregion

    #region Sort modes (habits, dailies, rewards)

    [Fact]
    public void DailiesSort_CurrentStreakHighToLow()
    {
        var vm = CreateViewModel();
        vm.NewDailyTitle = "NoStreak";
        vm.AddDaily();
        vm.NewDailyTitle = "Streaker";
        vm.AddDaily();
        var streaker = vm.Dailies.First(d => d.Title == "Streaker");
        streaker.Complete(); // Gives a streak of 1

        vm.DailiesSortMode = "Current streak (high to low)";

        Assert.Equal("Streaker", vm.Dailies[0].Title);
        Assert.Equal("NoStreak", vm.Dailies[1].Title);
    }

    [Fact]
    public void RewardsSort_GoldHighToLow()
    {
        var vm = CreateViewModel();
        vm.NewRewardTitle = "Cheap";
        vm.AddReward();
        vm.NewRewardTitle = "Expensive";
        vm.AddReward();
        vm.Rewards.First(r => r.Title == "Cheap").SetGoldCost(1);
        vm.Rewards.First(r => r.Title == "Expensive").SetGoldCost(100);

        vm.RewardsSortMode = "Gold value (high to low)";

        Assert.Equal("Expensive", vm.Rewards[0].Title);
        Assert.Equal("Cheap", vm.Rewards[1].Title);
    }

    [Fact]
    public void HabitsSort_GoldLowToHigh()
    {
        var vm = CreateViewModel();
        vm.NewHabitTitle = "Expensive";
        vm.AddHabit();
        vm.NewHabitTitle = "Cheap";
        vm.AddHabit();
        vm.Habits.First(h => h.Title == "Expensive").SetGoldReward(10);
        vm.Habits.First(h => h.Title == "Cheap").SetGoldReward(1);

        vm.HabitsSortMode = "Gold value (low to high)";

        Assert.Equal("Cheap", vm.Habits[0].Title);
        Assert.Equal("Expensive", vm.Habits[1].Title);
    }

    #endregion

    private MainWindowViewModel CreateViewModel()
    {
        var userService = new UserService(_tempDir);
        userService.LoadSync();
        var storageService = new StorageService(userService);
        return new MainWindowViewModel(storageService, userService);
    }
}
