using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Services;

namespace TaskApp.AndroidClient;

[Activity(Label = "TaskApp", MainLauncher = true, Exported = true, Theme = "@style/AppTheme")]
public sealed class MainActivity : Activity
{
    private const string DefaultApiUrl = "https://taskapp-api.hyi96.dev";
    private const string PreferencesName = "taskapp-cloud";
    private const string ApiUrlKey = "api-url";
    private const string ApiKeyKey = "api-key";
    private const string AccountIdKey = "account-id";
    private const string ProfileIdKey = "profile-id";

    private ISharedPreferences? _preferences;
    private EditText? _apiUrl;
    private EditText? _apiKey;
    private EditText? _accountId;
    private TextView? _profileId;
    private TextView? _status;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private);

        var root = new ScrollView(this)
        {
            FillViewport = true
        };

        var content = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        content.SetPadding(Dp(20), Dp(18), Dp(20), Dp(18));
        root.AddView(content);

        content.AddView(Header("TaskApp Cloud"));
        content.AddView(Body("Android bootstrap client"));

        _apiUrl = Input("API URL", _preferences?.GetString(ApiUrlKey, DefaultApiUrl) ?? DefaultApiUrl);
        _apiUrl.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        content.AddView(_apiUrl);

        _apiKey = Input("API key", _preferences?.GetString(ApiKeyKey, string.Empty) ?? string.Empty);
        _apiKey.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        content.AddView(_apiKey);

        _accountId = Input("Account ID", _preferences?.GetString(AccountIdKey, string.Empty) ?? string.Empty);
        _accountId.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
        content.AddView(_accountId);

        _profileId = Body(ProfileIdText());
        content.AddView(_profileId);

        content.AddView(Button("Save settings", SaveSettings));
        content.AddView(Button("Create account", async () => await CreateAccountAsync()));
        content.AddView(Button("Upload starter profile", async () => await UploadStarterProfileAsync()));
        content.AddView(Button("List profiles", async () => await ListProfilesAsync()));
        content.AddView(Button("Download profile", async () => await DownloadProfileAsync()));

        _status = Body("Ready.");
        _status.SetPadding(0, Dp(18), 0, 0);
        content.AddView(_status);

        SetContentView(root);
    }

    private async Task CreateAccountAsync()
    {
        await RunCloudActionAsync("Creating account...", async client =>
        {
            var account = await client.CreateAccountAsync("Android bootstrap");
            _accountId!.Text = account.Id.ToString();
            SaveSettings();
            return $"Created account {account.Id}.";
        });
    }

    private async Task UploadStarterProfileAsync()
    {
        await RunCloudActionAsync("Uploading starter profile...", async client =>
        {
            var accountId = ReadAccountId();
            var profileId = GetOrCreateProfileId();
            var response = await client.UploadProfileSnapshotAsync(
                accountId,
                profileId,
                "Android Profile",
                CreateStarterSnapshot());

            SaveSettings();
            return $"Uploaded profile {response.ProfileId}. Updated {response.UpdatedAt:O}.";
        });
    }

    private async Task ListProfilesAsync()
    {
        await RunCloudActionAsync("Loading profiles...", async client =>
        {
            var accountId = ReadAccountId();
            var profiles = await client.ListProfilesAsync(accountId);
            if (profiles.Count == 0)
            {
                return "No profiles found for this account.";
            }

            return string.Join(
                "\n",
                profiles.Select(profile =>
                    $"{profile.ProfileName}: {profile.ProfileId} ({profile.SchemaVersion})"));
        });
    }

    private async Task DownloadProfileAsync()
    {
        await RunCloudActionAsync("Downloading profile...", async client =>
        {
            var accountId = ReadAccountId();
            var profileId = GetOrCreateProfileId();
            var response = await client.DownloadProfileSnapshotAsync(accountId, profileId);
            if (response is null)
            {
                return $"Profile {profileId} does not exist yet.";
            }

            var snapshot = response.Snapshot;
            return $"Downloaded {response.ProfileName}: {snapshot.Tasks.Count} task(s), {snapshot.Rewards.Count} reward(s), {snapshot.Tags.Count} tag(s).";
        });
    }

    private async Task RunCloudActionAsync(string pendingMessage, Func<TaskAppCloudClient, Task<string>> action)
    {
        SaveSettings();
        HideKeyboard();
        SetStatus(pendingMessage);

        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(NormalizeApiUrl(_apiUrl?.Text), UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(20)
            };

            var client = new TaskAppCloudClient(httpClient, _apiKey?.Text);
            var result = await action(client);
            SetStatus(result);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private Guid ReadAccountId()
    {
        if (Guid.TryParse(_accountId?.Text, out var accountId))
        {
            return accountId;
        }

        throw new InvalidOperationException("Enter an account ID or create a new account first.");
    }

    private Guid GetOrCreateProfileId()
    {
        var stored = _preferences?.GetString(ProfileIdKey, string.Empty);
        if (Guid.TryParse(stored, out var profileId))
        {
            return profileId;
        }

        profileId = Guid.NewGuid();
        _preferences?.Edit()?.PutString(ProfileIdKey, profileId.ToString())?.Apply();
        _profileId!.Text = ProfileIdText(profileId);
        return profileId;
    }

    private TaskAppDataSnapshot CreateStarterSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var tag = new TagData
        {
            Id = Guid.NewGuid(),
            Name = "Android"
        };

        var task = new TodoTaskData
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Title = "Confirm Android cloud sync",
            Notes = "Created by the Android bootstrap client.",
            Tags = [tag],
            DueDate = now.AddDays(1),
            GoldReward = 1
        };

        var reward = new RewardData
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Title = "Ship the first APK",
            Notes = "Starter reward from Android.",
            GoldCost = 1,
            IsRepeatable = false,
            Tags = [tag]
        };

        var profile = new UserProfile
        {
            Gold = 0,
            LastActiveDate = DateOnly.FromDateTime(DateTime.Now)
        };

        return new TaskAppDataSnapshot(
            TaskAppDataSnapshot.CurrentSchemaVersion,
            now,
            new List<TaskData> { task },
            new List<RewardData> { reward },
            new List<TagData> { tag },
            profile,
            new List<LogEntry>());
    }

    private void SaveSettings()
    {
        _preferences?.Edit()
            ?.PutString(ApiUrlKey, NormalizeApiUrl(_apiUrl?.Text))
            ?.PutString(ApiKeyKey, _apiKey?.Text ?? string.Empty)
            ?.PutString(AccountIdKey, _accountId?.Text?.Trim() ?? string.Empty)
            ?.Apply();

        SetStatus("Settings saved.");
    }

    private Button Button(string text, Func<Task> action)
    {
        var button = new Button(this)
        {
            Text = text
        };
        button.SetAllCaps(false);
        button.Click += async (_, _) => await action();
        button.LayoutParameters = BlockLayout();
        return button;
    }

    private Button Button(string text, Action action)
    {
        var button = new Button(this)
        {
            Text = text
        };
        button.SetAllCaps(false);
        button.Click += (_, _) => action();
        button.LayoutParameters = BlockLayout();
        return button;
    }

    private EditText Input(string hint, string value)
    {
        var input = new EditText(this)
        {
            Hint = hint,
            Text = value
        };
        input.SetSingleLine(true);
        input.SetSelectAllOnFocus(true);
        input.LayoutParameters = BlockLayout();
        return input;
    }

    private TextView Header(string text)
    {
        var title = new TextView(this)
        {
            Text = text,
            TextSize = 24
        };
        title.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        title.LayoutParameters = BlockLayout();
        return title;
    }

    private TextView Body(string text)
    {
        var body = new TextView(this)
        {
            Text = text,
            TextSize = 15
        };
        body.LayoutParameters = BlockLayout();
        return body;
    }

    private LinearLayout.LayoutParams BlockLayout()
    {
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, Dp(5), 0, Dp(5));
        return layout;
    }

    private string ProfileIdText()
    {
        var stored = _preferences?.GetString(ProfileIdKey, string.Empty);
        return Guid.TryParse(stored, out var profileId)
            ? ProfileIdText(profileId)
            : "Profile ID will be generated on first upload.";
    }

    private static string ProfileIdText(Guid profileId) => $"Profile ID: {profileId}";

    private static string NormalizeApiUrl(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(trimmed) ? DefaultApiUrl : trimmed;
    }

    private void SetStatus(string message)
    {
        if (_status is not null)
        {
            _status.Text = message;
        }
    }

    private void HideKeyboard()
    {
        var inputMethodManager = (InputMethodManager?)GetSystemService(InputMethodService);
        inputMethodManager?.HideSoftInputFromWindow(CurrentFocus?.WindowToken, HideSoftInputFlags.None);
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
}
