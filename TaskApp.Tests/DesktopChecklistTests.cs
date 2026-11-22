using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using TaskApp.Converters;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.Services;
using TaskApp.ViewModels;

namespace TaskApp.Tests;

public sealed class DesktopChecklistTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), $"TaskAppTests_{Guid.NewGuid():N}");

    [Fact]
    public async Task ClaimedOneTimeRewardCanBeTakenAgainAfterUndo()
    {
        var (vm, store, _) = CreateDesktop();
        vm.NewRewardTitle = "Coffee";
        vm.AddReward();
        var reward = Assert.Single(vm.Rewards);
        var updates = new List<bool>();
        reward.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Reward.IsClaimedOneTime))
                updates.Add(reward.IsClaimedOneTime);
        };

        await vm.ClaimRewardAsync(reward);
        Assert.True(reward.IsClaimedOneTime);
        await vm.ClaimRewardAsync(reward);
        var entry = Assert.Single(await store.LoadAllLogEntriesAsync());

        Assert.True(await vm.UndoLogEntryAsync(entry));
        Assert.False(reward.IsClaimedOneTime);
        Assert.Equal(new[] { true, false }, updates);

        reward.SetRepeatable(true);
        await vm.ClaimRewardAsync(reward);
        await vm.ClaimRewardAsync(reward);
        Assert.False(reward.IsClaimedOneTime);
        Assert.Equal(2, reward.ClaimCount);
    }

    [Fact]
    public async Task NewDayOmitsHiddenDailiesThatMissedAPeriod()
    {
        var (vm, store, _) = CreateDesktop();
        var visible = new DailyTask { CreatedAt = DateTimeOffset.Now.AddDays(-3) };
        var hidden = new DailyTask { CreatedAt = visible.CreatedAt };
        hidden.SetHidden(true);
        await store.SaveTasksAsync(new[] { visible, hidden });
        await vm.LoadDataAsync();

        var missed = vm.GetUncompletedDailiesSinceLastActive(DateOnly.FromDateTime(DateTime.Now.AddDays(-2)));

        Assert.Equal(visible.Id, Assert.Single(missed).Id);
    }

    [Fact]
    public void DeadlineColorChangesAsTimePassesAndClearsOnCompletion()
    {
        var due = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

        Assert.Null(TodoDeadlineForegroundMultiConverter.GetDeadlineBrushKey(due, false, due.AddDays(-3).AddSeconds(-1)));
        Assert.Equal("TodoDueSoonBrush", TodoDeadlineForegroundMultiConverter.GetDeadlineBrushKey(due, false, due.AddDays(-3)));
        Assert.Equal("TodoOverdueBrush", TodoDeadlineForegroundMultiConverter.GetDeadlineBrushKey(due, false, due.AddSeconds(1)));
        Assert.Null(TodoDeadlineForegroundMultiConverter.GetDeadlineBrushKey(due, true, due.AddSeconds(1)));
        Assert.Null(TodoDeadlineForegroundMultiConverter.GetDeadlineBrushKey(null, false, due));
    }

    [AvaloniaFact]
    public void DeadlineEditRestoresTheNormalBrushForTheActiveTheme()
    {
        var converter = new TodoDeadlineForegroundMultiConverter();
        var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        ISolidColorBrush ReadBrush(DateTimeOffset? due, ThemeVariant theme)
            => Assert.IsAssignableFrom<ISolidColorBrush>(converter.Convert(
                new object?[] { due, false, now, theme }, typeof(IBrush), null, CultureInfo.InvariantCulture));

        Assert.Equal(Colors.DarkGoldenrod, ReadBrush(now.AddDays(1), ThemeVariant.Dark).Color);
        Assert.Equal(Colors.WhiteSmoke, ReadBrush(now.AddDays(7), ThemeVariant.Dark).Color);
        Assert.Equal(Colors.WhiteSmoke, ReadBrush(null, ThemeVariant.Dark).Color);
        Assert.Equal(Colors.Black, ReadBrush(now.AddDays(7), ThemeVariant.Light).Color);
    }

    [Fact]
    public async Task VacationModeSurvivesOverlappingSavesAndUpdatesOnProfileReload()
    {
        var (vm, store, users) = CreateDesktop();
        var original = users.CurrentUser!.Id;
        var other = await users.CreateUserAsync("Other");
        var updates = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsVacationMode))
                updates.Add(vm.IsVacationMode);
        };

        await Task.WhenAll(vm.SaveDataAsync(), vm.SetVacationModeAsync(true));
        Assert.True((await store.LoadUserProfileAsync()).IsVacationMode);

        await users.SwitchUserAsync(other.Id);
        store.RefreshDataDirectory();
        await vm.LoadDataAsync();
        Assert.False(vm.IsVacationMode);

        await users.SwitchUserAsync(original);
        store.RefreshDataDirectory();
        await vm.LoadDataAsync();
        Assert.True(vm.IsVacationMode);
        Assert.Contains("disable", vm.VacationModeTooltip);
        Assert.Equal(new[] { true, false, true }, updates);

        await vm.SetVacationModeAsync(false);
        Assert.False((await new StorageService(users).LoadUserProfileAsync()).IsVacationMode);
    }

    [Fact]
    public async Task DailyLoggedDurationCountsOnlyTheCurrentPeriodAndUpdatesAfterLoggingAndUndo()
    {
        var (vm, store, _) = CreateDesktop();
        var daily = new DailyTask { CreatedAt = DateTimeOffset.Now.AddDays(-2) };
        daily.SetRepeatEvery(3);
        await store.SaveTasksAsync(new[] { daily });

        var localMidnight = daily.GetCurrentPeriodStart().ToDateTime(TimeOnly.MinValue);
        var start = new DateTimeOffset(localMidnight, TimeZoneInfo.Local.GetUtcOffset(localMidnight));
        await store.AddLogEntryAsync(new LogEntry
        {
            Id = Guid.NewGuid(), TaskId = daily.Id, Type = LogType.ActivityDuration,
            Timestamp = start.AddMinutes(-5), Duration = TimeSpan.FromMinutes(10)
        });
        await store.AddLogEntryAsync(new LogEntry
        {
            Id = Guid.NewGuid(), TaskId = daily.Id, Type = LogType.ActivityDuration,
            Timestamp = start.AddMinutes(5), Duration = TimeSpan.FromMinutes(10)
        });

        await vm.LoadDataAsync();
        var loaded = Assert.Single(vm.Dailies);
        Assert.Equal(TimeSpan.FromMinutes(5), loaded.LoggedDurationForCurrentPeriod);

        await vm.LogActivityDurationAsync(TimeSpan.FromMinutes(30), "Focus", loaded.Id);
        Assert.Equal(TimeSpan.FromMinutes(35), loaded.LoggedDurationForCurrentPeriod);
        var entry = Assert.Single(await store.LoadAllLogEntriesAsync(), log => log.TitleSnapshot == "Focus");

        Assert.True(await vm.UndoLogEntryAsync(entry));
        Assert.Equal(TimeSpan.FromMinutes(5), loaded.LoggedDurationForCurrentPeriod);

        loaded.SetRepeatEvery(1);
        await vm.RefreshDailyLoggedDurationAsync(loaded);
        Assert.False(loaded.HasLoggedDurationForCurrentPeriod);
    }

    private (MainWindowViewModel ViewModel, StorageService Store, UserService Users) CreateDesktop()
    {
        var users = new UserService(_dataDirectory);
        users.LoadSync();
        var store = new StorageService(users);
        return (new MainWindowViewModel(store, users), store, users);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDirectory, recursive: true); } catch { }
    }
}
