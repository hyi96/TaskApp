using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Models.Rewards;
using TaskApp.Models.Tasks;
using TaskApp.Services;

namespace TaskApp.AndroidClient;

[Activity(Label = "TaskApp", MainLauncher = true, Exported = true, Theme = "@style/AppTheme")]
public sealed class MainActivity : Activity
{
    private const string DefaultApiUrl = "https://taskapp-api.hyi96.dev";
    private const string PreferencesName = "taskapp-cloud";
    private const string ApiUrlKey = "api-url";
    private const string AccountSecretKey = "account-secret";
    private const string AccountIdKey = "account-id";
    private const string ProfileIdKey = "profile-id";
    private const string ProfileNameKey = "profile-name";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly List<ProfileSummaryResponse> _profiles = new();
    private readonly List<Button> _tabButtons = new();
    private readonly List<Button> _commandButtons = new();
    private ISharedPreferences? _preferences;
    private EditText? _apiUrl;
    private EditText? _accountSecret;
    private EditText? _accountId;
    private Spinner? _profileSpinner;
    private TextView? _loginStatus;
    private TextView? _profileStatus;
    private TextView? _goldText;
    private TextView? _status;
    private LinearLayout? _contentList;
    private TaskAppDataSnapshot? _snapshot;
    private ProfileSummaryResponse? _selectedProfile;
    private string _selectedTab = "Dailies";
    private bool _suppressProfileSelection;
    private bool _isBusy;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        BuildUi();
        LoadLastSnapshot();
        UpdateLoginStatus();
        RenderCurrentSnapshot();
    }

    private void BuildUi()
    {
        var root = new ScrollView(this)
        {
            FillViewport = true
        };
        root.SetBackgroundColor(Color.ParseColor("#F5F7FA"));

        var content = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        content.SetPadding(Dp(16), Dp(14), Dp(16), Dp(24));
        root.AddView(content);

        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        header.SetGravity(GravityFlags.CenterVertical);
        header.LayoutParameters = BlockLayout(0, 0, 0, 12);

        var title = Text("TaskApp", 26, "#111827", bold: true);
        title.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        header.AddView(title);

        _goldText = Text("0.0 G", 18, "#A16207", bold: true);
        _goldText.Gravity = GravityFlags.Right;
        header.AddView(_goldText);
        content.AddView(header);

        content.AddView(BuildCloudPanel());
        content.AddView(BuildProfilePanel());
        content.AddView(BuildTabs());

        _contentList = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        content.AddView(_contentList);

        _status = Text("Ready.", 13, "#4B5563");
        _status.SetPadding(0, Dp(10), 0, 0);
        content.AddView(_status);

        SetContentView(root);
    }

    private View BuildCloudPanel()
    {
        var panel = Panel();
        panel.AddView(SectionTitle("Cloud"));

        _apiUrl = Input("API URL", _preferences?.GetString(ApiUrlKey, DefaultApiUrl) ?? DefaultApiUrl);
        _apiUrl.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        panel.AddView(_apiUrl);

        _accountId = Input("Account ID", _preferences?.GetString(AccountIdKey, string.Empty) ?? string.Empty);
        _accountId.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
        panel.AddView(_accountId);

        _accountSecret = Input("Account secret from desktop", _preferences?.GetString(AccountSecretKey, string.Empty) ?? string.Empty);
        _accountSecret.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        panel.AddView(_accountSecret);

        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        row.LayoutParameters = BlockLayout(0, 8, 0, 2);
        row.AddView(CommandButton("Login", async () => await LoginAsync(), primary: true, weight: 1));
        row.AddView(CommandButton("Refresh", async () => await RefreshProfilesAsync(autoDownload: true), primary: false, weight: 1));
        panel.AddView(row);

        var syncRow = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        syncRow.LayoutParameters = BlockLayout(0, 0, 0, 2);
        syncRow.AddView(CommandButton("Download", async () => await DownloadSelectedProfileAsync(), primary: false, weight: 1));
        syncRow.AddView(CommandButton("Upload", async () => await UploadCurrentSnapshotAsync(), primary: false, weight: 1));
        panel.AddView(syncRow);

        _loginStatus = Text(string.Empty, 13, "#374151", bold: true);
        _loginStatus.SetPadding(0, Dp(6), 0, 0);
        panel.AddView(_loginStatus);

        return panel;
    }

    private View BuildProfilePanel()
    {
        var panel = Panel();
        panel.AddView(SectionTitle("Profile"));

        _profileSpinner = new Spinner(this);
        _profileSpinner.LayoutParameters = BlockLayout();
        _profileSpinner.ItemSelected += (_, e) =>
        {
            if (_suppressProfileSelection || e.Position < 0 || e.Position >= _profiles.Count)
            {
                return;
            }

            _ = SelectProfileAsync(e.Position);
        };
        panel.AddView(_profileSpinner);

        _profileStatus = Text("Login to load cloud profiles.", 13, "#4B5563");
        _profileStatus.SetPadding(0, Dp(2), 0, 0);
        panel.AddView(_profileStatus);

        SetProfileSpinnerItems(new[] { "No profiles loaded" });
        return panel;
    }

    private View BuildTabs()
    {
        var scroller = new HorizontalScrollView(this)
        {
            HorizontalScrollBarEnabled = false
        };
        scroller.LayoutParameters = BlockLayout(0, 2, 0, 12);

        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        scroller.AddView(row);

        foreach (var tab in new[] { "Dailies", "Habits", "Todos", "Rewards" })
        {
            var button = Button(tab, () =>
            {
                _selectedTab = tab;
                RenderCurrentSnapshot();
            });
            button.SetMinWidth(Dp(96));
            _tabButtons.Add(button);
            row.AddView(button);
        }

        return scroller;
    }

    private async Task LoginAsync()
    {
        await RunCloudActionAsync("Logging in...", async client =>
        {
            var account = await client.LoginAccountAsync(ReadAccountId(), ReadAccountSecret());
            SaveSettings();
            MarkLoginVerified(account);
            await LoadProfilesAsync(client, autoDownload: true);
            return $"Logged in to {account.DisplayName}.";
        });
    }

    private async Task RefreshProfilesAsync(bool autoDownload)
    {
        await RunCloudActionAsync("Loading profiles...", async client =>
        {
            SaveSettings();
            await LoadProfilesAsync(client, autoDownload);
            return _profiles.Count == 0
                ? "No profiles found for this account."
                : $"Loaded {_profiles.Count} profile(s).";
        });
    }

    private async Task LoadProfilesAsync(TaskAppCloudClient client, bool autoDownload)
    {
        var accountId = ReadAccountId();
        _ = ReadAccountSecret();
        var profiles = await client.ListProfilesAsync(accountId);
        _profiles.Clear();
        _profiles.AddRange(profiles.OrderBy(profile => profile.ProfileName, StringComparer.OrdinalIgnoreCase));

        if (_profiles.Count == 0)
        {
            _selectedProfile = null;
            _snapshot = null;
            SetProfileSpinnerItems(new[] { "No profiles found" });
            SetProfileStatus("No profiles found for this account.");
            RenderCurrentSnapshot();
            return;
        }

        var savedProfileId = ReadSavedProfileId();
        var selectedIndex = savedProfileId.HasValue
            ? _profiles.FindIndex(profile => profile.ProfileId == savedProfileId.Value)
            : 0;
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        _suppressProfileSelection = true;
        SetProfileSpinnerItems(_profiles.Select(FormatProfileLabel).ToArray());
        _profileSpinner!.SetSelection(selectedIndex);
        _suppressProfileSelection = false;

        _selectedProfile = _profiles[selectedIndex];
        SaveSelectedProfile(_selectedProfile);
        SetProfileStatus(FormatProfileStatus(_selectedProfile));

        if (autoDownload)
        {
            await DownloadSelectedProfileAsync(client);
        }
        else
        {
            LoadSnapshotFromDisk(_selectedProfile.ProfileId);
            RenderCurrentSnapshot();
        }
    }

    private async Task SelectProfileAsync(int index)
    {
        if (index < 0 || index >= _profiles.Count)
        {
            return;
        }

        _selectedProfile = _profiles[index];
        SaveSelectedProfile(_selectedProfile);
        SetProfileStatus(FormatProfileStatus(_selectedProfile));
        LoadSnapshotFromDisk(_selectedProfile.ProfileId);
        RenderCurrentSnapshot();
        await DownloadSelectedProfileAsync();
    }

    private async Task DownloadSelectedProfileAsync()
    {
        await RunCloudActionAsync("Downloading profile...", async client =>
        {
            await DownloadSelectedProfileAsync(client);
            return _selectedProfile == null
                ? "No profile selected."
                : $"Downloaded {_selectedProfile.ProfileName}.";
        });
    }

    private async Task DownloadSelectedProfileAsync(TaskAppCloudClient client)
    {
        var profile = RequireSelectedProfile();
        var accountId = ReadAccountId();
        _ = ReadAccountSecret();

        var response = await client.DownloadProfileSnapshotAsync(accountId, profile.ProfileId);
        MarkLoginVerified(accountId);
        if (response == null)
        {
            SetStatus($"Profile {profile.ProfileName} has no cloud snapshot.");
            return;
        }

        _selectedProfile = new ProfileSummaryResponse(
            response.AccountId,
            response.ProfileId,
            response.ProfileName,
            response.CreatedAt,
            response.UpdatedAt,
            response.Snapshot.SchemaVersion);
        _snapshot = response.Snapshot;
        SaveSelectedProfile(_selectedProfile);
        await SaveSnapshotToDiskAsync(response.ProfileId, response.Snapshot);
        SetProfileStatus(FormatProfileStatus(_selectedProfile));
        RenderCurrentSnapshot();
    }

    private async Task UploadCurrentSnapshotAsync()
    {
        await RunCloudActionAsync("Uploading profile...", async client =>
        {
            await UploadCurrentSnapshotAsync(client);
            return _selectedProfile == null
                ? "No profile selected."
                : $"Uploaded {_selectedProfile.ProfileName}.";
        });
    }

    private async Task UploadCurrentSnapshotAsync(TaskAppCloudClient client)
    {
        var profile = RequireSelectedProfile();
        var snapshot = RequireSnapshot();
        var accountId = ReadAccountId();
        _ = ReadAccountSecret();

        var updatedSnapshot = snapshot with
        {
            CapturedAt = DateTimeOffset.UtcNow,
            UserProfile = snapshot.UserProfile
        };
        updatedSnapshot.UserProfile.LastActiveDate = DateOnly.FromDateTime(DateTime.Now);

        var response = await client.UploadProfileSnapshotAsync(
            accountId,
            profile.ProfileId,
            profile.ProfileName,
            updatedSnapshot);

        _selectedProfile = new ProfileSummaryResponse(
            response.AccountId,
            response.ProfileId,
            response.ProfileName,
            response.CreatedAt,
            response.UpdatedAt,
            response.Snapshot.SchemaVersion);
        _snapshot = response.Snapshot;
        await SaveSnapshotToDiskAsync(response.ProfileId, response.Snapshot);
        SetProfileStatus(FormatProfileStatus(_selectedProfile));
        MarkLoginVerified(accountId);
        RenderCurrentSnapshot();
    }

    private async Task CompleteTodoAsync(TodoTaskData todo)
    {
        if (todo.LastCompletedDate.HasValue)
        {
            return;
        }

        var model = (TodoTask)TaskMapper.ToModel(todo);
        model.Complete();
        AddGold(model.GoldReward);
        ReplaceTask(TaskMapper.ToData(model));
        AddLog(LogType.TodoCompleted, taskId: model.Id, goldDelta: model.GoldReward, title: model.Title);
        await SaveAndUploadAfterMutationAsync($"Completed {model.Title}.");
    }

    private async Task CompleteDailyAsync(DailyTaskData daily)
    {
        var model = (DailyTask)TaskMapper.ToModel(daily);
        if (model.IsCompleteForCurrentPeriod)
        {
            return;
        }

        model.Complete();
        var rewardAmount = model.GetGoldRewardWithBonus();
        AddGold(rewardAmount);
        ReplaceTask(TaskMapper.ToData(model));
        AddLog(LogType.DailyCompleted, taskId: model.Id, goldDelta: rewardAmount, title: model.Title);
        await SaveAndUploadAfterMutationAsync($"Completed {model.Title}.");
    }

    private async Task IncrementHabitAsync(HabitTaskData habit)
    {
        var model = (HabitTask)TaskMapper.ToModel(habit);
        var previousCount = model.Count;
        model.Increment();
        if (Math.Abs(model.Count - previousCount) < 0.001)
        {
            SetStatus($"{model.Title} cannot be incremented.");
            return;
        }

        AddGold(model.GoldReward);
        ReplaceTask(TaskMapper.ToData(model));
        AddLog(
            LogType.HabitIncremented,
            taskId: model.Id,
            goldDelta: model.GoldReward,
            title: model.Title,
            countDelta: model.IncrementAmount);
        await SaveAndUploadAfterMutationAsync($"Incremented {model.Title}.");
    }

    private async Task DecrementHabitAsync(HabitTaskData habit)
    {
        var model = (HabitTask)TaskMapper.ToModel(habit);
        var previousCount = model.Count;
        model.Decrement();
        if (Math.Abs(model.Count - previousCount) < 0.001)
        {
            SetStatus($"{model.Title} cannot be decremented.");
            return;
        }

        ReplaceTask(TaskMapper.ToData(model));
        await SaveAndUploadAfterMutationAsync($"Decremented {model.Title}.");
    }

    private async Task ClaimRewardAsync(RewardData reward)
    {
        var model = RewardMapper.ToModel(reward);
        var snapshot = RequireSnapshot();
        if (!model.TryClaim(snapshot.UserProfile.Gold))
        {
            SetStatus(model.IsClaimed && !model.IsRepeatable
                ? $"{model.Title} is already claimed."
                : $"Not enough gold for {model.Title}.");
            return;
        }

        snapshot.UserProfile.Gold -= model.GoldCost;
        ReplaceReward(RewardMapper.ToData(model));
        AddLog(LogType.RewardClaimed, rewardId: model.Id, goldDelta: -Math.Abs(model.GoldCost), title: model.Title);
        await SaveAndUploadAfterMutationAsync($"Claimed {model.Title}.");
    }

    private async Task SaveAndUploadAfterMutationAsync(string localMessage)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        var profile = RequireSelectedProfile();
        var snapshot = RequireSnapshot();
        snapshot.UserProfile.LastActiveDate = DateOnly.FromDateTime(DateTime.Now);
        _snapshot = snapshot with { CapturedAt = DateTimeOffset.UtcNow };
        await SaveSnapshotToDiskAsync(profile.ProfileId, _snapshot);
        RenderCurrentSnapshot();
        SetStatus($"{localMessage} Uploading...");

        try
        {
            using var httpClient = CreateHttpClient();
            var client = new TaskAppCloudClient(httpClient, accountSecret: ReadAccountSecret());
            await UploadCurrentSnapshotAsync(client);
            SetStatus($"{localMessage} Synced.");
        }
        catch (Exception ex)
        {
            SetStatus($"{localMessage} Saved locally. Upload failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceTask(TaskData replacement)
    {
        var snapshot = RequireSnapshot();
        var tasks = snapshot.Tasks.ToList();
        var index = tasks.FindIndex(task => task.Id == replacement.Id);
        if (index >= 0)
        {
            tasks[index] = replacement;
            _snapshot = snapshot with { Tasks = tasks };
        }
    }

    private void ReplaceReward(RewardData replacement)
    {
        var snapshot = RequireSnapshot();
        var rewards = snapshot.Rewards.ToList();
        var index = rewards.FindIndex(reward => reward.Id == replacement.Id);
        if (index >= 0)
        {
            rewards[index] = replacement;
            _snapshot = snapshot with { Rewards = rewards };
        }
    }

    private void AddGold(double amount)
    {
        RequireSnapshot().UserProfile.Gold += amount;
    }

    private void AddLog(
        LogType type,
        Guid? taskId = null,
        Guid? rewardId = null,
        double goldDelta = 0,
        string title = "",
        double? countDelta = null)
    {
        var snapshot = RequireSnapshot();
        var logs = snapshot.LogEntries.ToList();
        logs.Add(new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            TaskId = taskId,
            RewardId = rewardId,
            GoldDelta = goldDelta,
            UserGold = snapshot.UserProfile.Gold,
            CountDelta = countDelta,
            TitleSnapshot = title
        });
        _snapshot = snapshot with { LogEntries = logs };
    }

    private void RenderCurrentSnapshot()
    {
        if (_contentList == null)
        {
            return;
        }

        UpdateTabButtons();
        _contentList.RemoveAllViews();
        _goldText!.Text = $"{(_snapshot?.UserProfile.Gold ?? 0):0.#} G";

        if (_snapshot == null)
        {
            _contentList.AddView(EmptyState("Login and select a profile to download tasks."));
            return;
        }

        switch (_selectedTab)
        {
            case "Habits":
                RenderHabits();
                break;
            case "Todos":
                RenderTodos();
                break;
            case "Rewards":
                RenderRewards();
                break;
            default:
                RenderDailies();
                break;
        }
    }

    private void RenderDailies()
    {
        var dailies = RequireSnapshot().Tasks
            .OfType<DailyTaskData>()
            .Where(task => !task.IsHidden)
            .OrderBy(task => IsDailyCompleteForCurrentPeriod(task))
            .ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dailies.Count == 0)
        {
            _contentList!.AddView(EmptyState("No visible dailies."));
            return;
        }

        foreach (var daily in dailies)
        {
            var complete = IsDailyCompleteForCurrentPeriod(daily);
            var subtitle = complete
                ? $"Complete - streak {daily.CurrentStreak}"
                : $"Due {GetDailyPeriodEndDate(daily):MMM d} - streak {daily.CurrentStreak} - +{GetDailyRewardPreview(daily):0.#}G";
            _contentList!.AddView(TaskCard(
                daily.Title,
                subtitle,
                complete ? "Done" : "Complete",
                enabled: !complete,
                async () => await CompleteDailyAsync(daily)));
        }
    }

    private void RenderHabits()
    {
        var habits = RequireSnapshot().Tasks
            .OfType<HabitTaskData>()
            .Where(task => !task.IsHidden)
            .OrderBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (habits.Count == 0)
        {
            _contentList!.AddView(EmptyState("No visible habits."));
            return;
        }

        foreach (var habit in habits)
        {
            var card = Card();
            card.AddView(Text(habit.Title, 16, "#111827", bold: true));
            card.AddView(Text($"Count {habit.Count:0.#} - +{habit.GoldReward:0.#}G - resets {habit.ResetCadence}", 13, "#4B5563"));

            var row = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            row.LayoutParameters = BlockLayout(0, 8, 0, 0);
            row.AddView(CommandButton("+", async () => await IncrementHabitAsync(habit), primary: true, weight: 1, trackBusy: false));
            row.AddView(CommandButton("-", async () => await DecrementHabitAsync(habit), primary: false, weight: 1, enabled: habit.DecrementEnabled, trackBusy: false));
            card.AddView(row);
            _contentList!.AddView(card);
        }
    }

    private void RenderTodos()
    {
        var todos = RequireSnapshot().Tasks
            .OfType<TodoTaskData>()
            .Where(task => !task.IsHidden)
            .OrderBy(task => task.LastCompletedDate.HasValue)
            .ThenBy(task => task.DueDate ?? DateTimeOffset.MaxValue)
            .ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (todos.Count == 0)
        {
            _contentList!.AddView(EmptyState("No visible todos."));
            return;
        }

        foreach (var todo in todos)
        {
            var complete = todo.LastCompletedDate.HasValue;
            var checklist = todo.Checklist.Count == 0
                ? string.Empty
                : $" - {todo.Checklist.Count(item => item.IsCompleted)}/{todo.Checklist.Count} checklist";
            var due = todo.DueDate.HasValue ? $"Due {todo.DueDate.Value.LocalDateTime:MMM d}" : "No due date";
            _contentList!.AddView(TaskCard(
                todo.Title,
                complete ? $"Complete{checklist}" : $"{due}{checklist} - +{todo.GoldReward:0.#}G",
                complete ? "Done" : "Complete",
                enabled: !complete,
                async () => await CompleteTodoAsync(todo)));
        }
    }

    private void RenderRewards()
    {
        var rewards = RequireSnapshot().Rewards
            .Where(reward => !reward.IsHidden)
            .Where(reward => reward.IsRepeatable || !reward.IsClaimed)
            .OrderBy(reward => reward.GoldCost)
            .ThenBy(reward => reward.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rewards.Count == 0)
        {
            _contentList!.AddView(EmptyState("No available rewards."));
            return;
        }

        var gold = RequireSnapshot().UserProfile.Gold;
        foreach (var reward in rewards)
        {
            var enabled = gold >= reward.GoldCost && (reward.IsRepeatable || !reward.IsClaimed);
            var repeats = reward.IsRepeatable ? "repeatable" : "one-time";
            _contentList!.AddView(TaskCard(
                reward.Title,
                $"{reward.GoldCost:0.#}G - {repeats} - claimed {reward.ClaimCount}",
                "Claim",
                enabled,
                async () => await ClaimRewardAsync(reward)));
        }
    }

    private View TaskCard(string title, string subtitle, string actionText, bool enabled, Func<Task> action)
    {
        var card = Card();
        card.AddView(Text(title, 16, enabled ? "#111827" : "#6B7280", bold: true));
        card.AddView(Text(subtitle, 13, "#4B5563"));
        card.AddView(CommandButton(actionText, action, primary: enabled, weight: 0, enabled: enabled, trackBusy: false));
        return card;
    }

    private bool IsDailyCompleteForCurrentPeriod(DailyTaskData daily)
    {
        return daily.LastCompletionPeriod is DateOnly period && period == GetDailyPeriodStart(daily, DateTimeOffset.Now);
    }

    private double GetDailyRewardPreview(DailyTaskData daily)
    {
        var currentStreak = daily.CurrentStreak;
        if (daily.LastCompletionPeriod is DateOnly lastPeriod)
        {
            var currentPeriod = GetDailyPeriodStart(daily, DateTimeOffset.Now);
            var previousPeriod = GetPreviousPeriodStart(currentPeriod, daily.Cadence, daily.RepeatEvery);
            currentStreak = lastPeriod == previousPeriod ? currentStreak + 1 : 1;
        }
        else
        {
            currentStreak = 1;
        }

        var bonusPercent = daily.StreakBonusRules
            .Where(rule => currentStreak >= rule.StreakGoal)
            .Select(rule => rule.BonusPercent)
            .DefaultIfEmpty(0)
            .Max();
        return daily.GoldReward * (1 + bonusPercent / 100.0);
    }

    private DateOnly GetDailyPeriodStart(DailyTaskData daily, DateTimeOffset localTime)
    {
        var date = DateOnly.FromDateTime(localTime.ToLocalTime().DateTime);
        var anchor = DateOnly.FromDateTime(daily.CreatedAt.ToLocalTime().DateTime);
        var interval = daily.RepeatEvery < 1 ? 1 : daily.RepeatEvery;

        return daily.Cadence switch
        {
            RepeatCadence.Daily => anchor.AddDays(((date.DayNumber - anchor.DayNumber) / interval) * interval),
            RepeatCadence.Weekly => GetWeeklyPeriodStart(date, anchor, interval),
            RepeatCadence.Monthly => GetMonthlyPeriodStart(date, anchor, interval),
            RepeatCadence.Yearly => new DateOnly(anchor.Year + ((date.Year - anchor.Year) / interval) * interval, 1, 1),
            _ => date
        };
    }

    private DateOnly GetDailyPeriodEndDate(DailyTaskData daily)
    {
        var periodStart = GetDailyPeriodStart(daily, DateTimeOffset.Now);
        var interval = daily.RepeatEvery < 1 ? 1 : daily.RepeatEvery;
        return daily.Cadence switch
        {
            RepeatCadence.Daily => periodStart.AddDays(interval - 1),
            RepeatCadence.Weekly => periodStart.AddDays(interval * 7 - 1),
            RepeatCadence.Monthly => periodStart.AddMonths(interval).AddDays(-1),
            RepeatCadence.Yearly => periodStart.AddYears(interval).AddDays(-1),
            _ => periodStart
        };
    }

    private static DateOnly GetPreviousPeriodStart(DateOnly currentPeriodStart, RepeatCadence cadence, int repeatEvery)
    {
        var interval = repeatEvery < 1 ? 1 : repeatEvery;
        return cadence switch
        {
            RepeatCadence.Daily => currentPeriodStart.AddDays(-interval),
            RepeatCadence.Weekly => currentPeriodStart.AddDays(-7 * interval),
            RepeatCadence.Monthly => currentPeriodStart.AddMonths(-interval),
            RepeatCadence.Yearly => currentPeriodStart.AddYears(-interval),
            _ => currentPeriodStart
        };
    }

    private static DateOnly GetWeeklyPeriodStart(DateOnly currentDate, DateOnly anchor, int interval)
    {
        var currentStart = currentDate.AddDays(-GetDaysSinceWeekStart(currentDate.DayOfWeek));
        var anchorStart = anchor.AddDays(-GetDaysSinceWeekStart(anchor.DayOfWeek));
        var weeks = (currentStart.DayNumber - anchorStart.DayNumber) / 7;
        var periodIndex = weeks / interval * interval;
        return anchorStart.AddDays(periodIndex * 7);
    }

    private static DateOnly GetMonthlyPeriodStart(DateOnly currentDate, DateOnly anchor, int interval)
    {
        var anchorMonthIndex = anchor.Year * 12 + anchor.Month - 1;
        var currentMonthIndex = currentDate.Year * 12 + currentDate.Month - 1;
        var monthsDiff = currentMonthIndex - anchorMonthIndex;
        var periodIndex = monthsDiff / interval * interval;
        var targetMonthIndex = anchorMonthIndex + periodIndex;
        var year = targetMonthIndex / 12;
        var month = targetMonthIndex % 12 + 1;
        return new DateOnly(year, month, 1);
    }

    private static int GetDaysSinceWeekStart(DayOfWeek dayOfWeek)
    {
        return ((int)dayOfWeek + 6) % 7;
    }

    private async Task RunCloudActionAsync(string pendingMessage, Func<TaskAppCloudClient, Task<string>> action)
    {
        if (_isBusy)
        {
            return;
        }

        SaveSettings(setStatus: false);
        HideKeyboard();
        SetBusy(true);
        SetStatus(pendingMessage);

        try
        {
            using var httpClient = CreateHttpClient();
            var client = new TaskAppCloudClient(httpClient, accountSecret: ReadAccountSecret());
            var result = await action(client);
            SetStatus(result);
        }
        catch (Exception ex)
        {
            UpdateLoginStatus();
            SetStatus(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(NormalizeApiUrl(_apiUrl?.Text), UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private Guid ReadAccountId()
    {
        if (Guid.TryParse(_accountId?.Text, out var accountId))
        {
            return accountId;
        }

        throw new InvalidOperationException("Enter an account ID from desktop cloud settings.");
    }

    private string ReadAccountSecret()
    {
        var accountSecret = _accountSecret?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(accountSecret))
        {
            return accountSecret;
        }

        throw new InvalidOperationException("Enter the account secret from desktop cloud settings.");
    }

    private Guid? ReadSavedProfileId()
    {
        return Guid.TryParse(_preferences?.GetString(ProfileIdKey, string.Empty), out var profileId)
            ? profileId
            : null;
    }

    private ProfileSummaryResponse RequireSelectedProfile()
    {
        return _selectedProfile ?? throw new InvalidOperationException("Select a profile first.");
    }

    private TaskAppDataSnapshot RequireSnapshot()
    {
        return _snapshot ?? throw new InvalidOperationException("Download a profile first.");
    }

    private void SaveSettings(bool setStatus = true)
    {
        _preferences?.Edit()
            ?.PutString(ApiUrlKey, NormalizeApiUrl(_apiUrl?.Text))
            ?.PutString(AccountSecretKey, _accountSecret?.Text?.Trim() ?? string.Empty)
            ?.PutString(AccountIdKey, _accountId?.Text?.Trim() ?? string.Empty)
            ?.Apply();

        UpdateLoginStatus();
        if (setStatus)
        {
            SetStatus("Settings saved.");
        }
    }

    private void SaveSelectedProfile(ProfileSummaryResponse profile)
    {
        _preferences?.Edit()
            ?.PutString(ProfileIdKey, profile.ProfileId.ToString())
            ?.PutString(ProfileNameKey, profile.ProfileName)
            ?.Apply();
    }

    private void MarkLoginVerified(AccountResponse account)
    {
        MarkLoginVerified(account.Id, account.DisplayName);
    }

    private void MarkLoginVerified(Guid accountId, string displayName = "")
    {
        if (_loginStatus == null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? "cloud account" : displayName.Trim();
        _loginStatus.Text = $"Cloud login: verified as {name} ({ShortAccountId(accountId)}).";
        _loginStatus.SetTextColor(Color.ParseColor("#166534"));
    }

    private void UpdateLoginStatus()
    {
        if (_loginStatus == null)
        {
            return;
        }

        if (HasSavedAccountCredentials())
        {
            _loginStatus.Text = "Cloud login: saved credentials are not verified. Tap Login.";
            _loginStatus.SetTextColor(Color.ParseColor("#92400E"));
            return;
        }

        _loginStatus.Text = "Cloud login: not logged in.";
        _loginStatus.SetTextColor(Color.ParseColor("#4B5563"));
    }

    private bool HasSavedAccountCredentials()
    {
        return Uri.TryCreate(NormalizeApiUrl(_apiUrl?.Text), UriKind.Absolute, out _) &&
            Guid.TryParse(_accountId?.Text, out _) &&
            !string.IsNullOrWhiteSpace(_accountSecret?.Text);
    }

    private void LoadLastSnapshot()
    {
        var profileId = ReadSavedProfileId();
        if (profileId.HasValue)
        {
            LoadSnapshotFromDisk(profileId.Value);
        }
    }

    private void LoadSnapshotFromDisk(Guid profileId)
    {
        var path = GetSnapshotPath(profileId);
        if (!System.IO.File.Exists(path))
        {
            _snapshot = null;
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(path);
            _snapshot = JsonSerializer.Deserialize<TaskAppDataSnapshot>(json, JsonOptions);
            if (_snapshot != null && _selectedProfile == null)
            {
                var savedName = _preferences?.GetString(ProfileNameKey, string.Empty);
                _selectedProfile = new ProfileSummaryResponse(
                    ReadAccountIdOrEmpty(),
                    profileId,
                    string.IsNullOrWhiteSpace(savedName) ? "Current profile" : savedName,
                    _snapshot.CapturedAt,
                    _snapshot.CapturedAt,
                    _snapshot.SchemaVersion);
            }
        }
        catch
        {
            _snapshot = null;
        }
    }

    private async Task SaveSnapshotToDiskAsync(Guid profileId, TaskAppDataSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await System.IO.File.WriteAllTextAsync(GetSnapshotPath(profileId), json);
    }

    private string GetSnapshotPath(Guid profileId)
    {
        return System.IO.Path.Combine(FilesDir!.AbsolutePath, $"taskapp-profile-{profileId:N}.json");
    }

    private Guid ReadAccountIdOrEmpty()
    {
        return Guid.TryParse(_accountId?.Text, out var accountId) ? accountId : Guid.Empty;
    }

    private void SetProfileSpinnerItems(string[] labels)
    {
        if (_profileSpinner == null)
        {
            return;
        }

        var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, labels);
        adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        _profileSpinner.Adapter = adapter;
    }

    private static string FormatProfileLabel(ProfileSummaryResponse profile)
    {
        return $"{profile.ProfileName} ({ShortAccountId(profile.ProfileId)})";
    }

    private static string FormatProfileStatus(ProfileSummaryResponse profile)
    {
        return $"Updated {profile.UpdatedAt.LocalDateTime:g} - schema {profile.SchemaVersion}";
    }

    private void SetProfileStatus(string message)
    {
        if (_profileStatus != null)
        {
            _profileStatus.Text = message;
        }
    }

    private static string NormalizeApiUrl(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(trimmed) ? DefaultApiUrl : trimmed;
    }

    private static string ShortAccountId(Guid accountId)
    {
        var value = accountId.ToString();
        return value[..8];
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        foreach (var button in _commandButtons)
        {
            button.Enabled = !isBusy && button.Tag?.ToString() != "disabled";
        }
    }

    private void SetStatus(string message)
    {
        if (_status != null)
        {
            _status.Text = message;
        }
    }

    private void UpdateTabButtons()
    {
        foreach (var button in _tabButtons)
        {
            var selected = string.Equals(button.Text, _selectedTab, StringComparison.Ordinal);
            button.SetTextColor(Color.ParseColor(selected ? "#FFFFFF" : "#1F2937"));
            button.Background = Rounded(selected ? "#2563EB" : "#FFFFFF", selected ? "#2563EB" : "#D1D5DB", 18);
        }
    }

    private LinearLayout Panel()
    {
        var panel = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        panel.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        panel.Background = Rounded("#FFFFFF", "#E5E7EB", 10);
        panel.LayoutParameters = BlockLayout(0, 0, 0, 12);
        return panel;
    }

    private LinearLayout Card()
    {
        var card = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        card.Background = Rounded("#FFFFFF", "#E5E7EB", 8);
        card.LayoutParameters = BlockLayout(0, 0, 0, 10);
        return card;
    }

    private TextView SectionTitle(string text)
    {
        var title = Text(text, 18, "#111827", bold: true);
        title.LayoutParameters = BlockLayout(0, 0, 0, 6);
        return title;
    }

    private TextView EmptyState(string message)
    {
        var empty = Text(message, 15, "#6B7280");
        empty.Gravity = GravityFlags.Center;
        empty.SetPadding(Dp(12), Dp(28), Dp(12), Dp(28));
        empty.LayoutParameters = BlockLayout();
        return empty;
    }

    private TextView Text(string text, int size, string color, bool bold = false)
    {
        var view = new TextView(this)
        {
            Text = text,
            TextSize = size
        };
        view.SetTextColor(Color.ParseColor(color));
        if (bold)
        {
            view.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        }

        return view;
    }

    private EditText Input(string hint, string value)
    {
        var input = new EditText(this)
        {
            Hint = hint,
            Text = value,
            TextSize = 14
        };
        input.SetSingleLine(true);
        input.SetSelectAllOnFocus(true);
        input.LayoutParameters = BlockLayout(0, 2, 0, 4);
        return input;
    }

    private Button CommandButton(string text, Func<Task> action, bool primary, float weight, bool enabled = true, bool trackBusy = true)
    {
        var button = Button(text, () => _ = RunButtonActionAsync(action));
        button.Enabled = enabled;
        if (!enabled)
        {
            button.Tag = "disabled";
        }

        button.SetTextColor(Color.ParseColor(primary ? "#FFFFFF" : "#1F2937"));
        button.Background = Rounded(primary ? "#2563EB" : "#FFFFFF", primary ? "#2563EB" : "#D1D5DB", 8);
        button.LayoutParameters = weight > 0
            ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, weight)
            : BlockLayout(0, 8, 0, 0);
        if (trackBusy)
        {
            _commandButtons.Add(button);
        }

        return button;
    }

    private async Task RunButtonActionAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private Button Button(string text, Action action)
    {
        var button = new Button(this)
        {
            Text = text,
            TextSize = 14
        };
        button.SetAllCaps(false);
        button.SetPadding(Dp(10), Dp(4), Dp(10), Dp(4));
        button.Click += (_, _) => action();
        return button;
    }

    private GradientDrawable Rounded(string fill, string stroke, int radius)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(Color.ParseColor(fill));
        drawable.SetStroke(Dp(1), Color.ParseColor(stroke));
        drawable.SetCornerRadius(Dp(radius));
        return drawable;
    }

    private LinearLayout.LayoutParams BlockLayout(int left = 0, int top = 0, int right = 0, int bottom = 0)
    {
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(Dp(left), Dp(top), Dp(right), Dp(bottom));
        return layout;
    }

    private void HideKeyboard()
    {
        var inputMethodManager = (InputMethodManager?)GetSystemService(InputMethodService);
        inputMethodManager?.HideSoftInputFromWindow(CurrentFocus?.WindowToken, HideSoftInputFlags.None);
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
}
