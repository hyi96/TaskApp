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
    private const string AccountSecretKey = "account-secret";
    private const string AccountIdKey = "account-id";
    private const string ProfileIdKey = "profile-id";

    private ISharedPreferences? _preferences;
    private EditText? _apiUrl;
    private EditText? _apiKey;
    private EditText? _accountSecret;
    private EditText? _accountId;
    private EditText? _profileId;
    private TextView? _loginStatus;
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

        _apiKey = Input("Server API key", _preferences?.GetString(ApiKeyKey, string.Empty) ?? string.Empty);
        _apiKey.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        content.AddView(_apiKey);

        _accountSecret = Input("Account secret", _preferences?.GetString(AccountSecretKey, string.Empty) ?? string.Empty);
        _accountSecret.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        content.AddView(_accountSecret);

        _accountId = Input("Account ID", _preferences?.GetString(AccountIdKey, string.Empty) ?? string.Empty);
        _accountId.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
        content.AddView(_accountId);

        _profileId = Input("Profile ID", _preferences?.GetString(ProfileIdKey, string.Empty) ?? string.Empty);
        _profileId.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
        content.AddView(_profileId);

        _loginStatus = Body(BuildLoginStatus());
        _loginStatus.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        content.AddView(_loginStatus);

        content.AddView(Button("Save settings", SaveSettings));
        content.AddView(Button("Create account", async () => await CreateAccountAsync()));
        content.AddView(Button("Login", async () => await LoginAsync()));
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
        await RunCloudActionAsync("Creating account...", includeServerApiKey: true, action: async client =>
        {
            var account = await client.CreateAccountAsync("Android bootstrap");
            _accountId!.Text = account.Id.ToString();
            if (!string.IsNullOrWhiteSpace(account.LoginSecret))
            {
                _accountSecret!.Text = account.LoginSecret;
                SaveSettings();
                MarkLoginVerified(account);
                return $"Created account {account.Id}. Account secret saved.";
            }

            SaveSettings();
            UpdateLoginStatus();
            return "Account was created, but the API did not return an account secret. Redeploy the latest API and create a new account.";
        });
    }

    private async Task LoginAsync()
    {
        await RunCloudActionAsync("Logging in...", includeServerApiKey: false, action: async client =>
        {
            var account = await client.LoginAccountAsync(ReadAccountId(), ReadAccountSecret());
            SaveSettings();
            MarkLoginVerified(account);
            return $"Logged in to {account.DisplayName}.";
        });
    }

    private async Task UploadStarterProfileAsync()
    {
        await RunCloudActionAsync("Uploading starter profile...", includeServerApiKey: false, action: async client =>
        {
            var accountId = ReadAccountId();
            _ = ReadAccountSecret();
            var profileId = GetOrCreateProfileId();
            var response = await client.UploadProfileSnapshotAsync(
                accountId,
                profileId,
                "Android Profile",
                CreateStarterSnapshot());

            SaveSettings();
            MarkLoginVerified(accountId);
            return $"Uploaded profile {response.ProfileId}. Updated {response.UpdatedAt:O}.";
        });
    }

    private async Task ListProfilesAsync()
    {
        await RunCloudActionAsync("Loading profiles...", includeServerApiKey: false, action: async client =>
        {
            var accountId = ReadAccountId();
            _ = ReadAccountSecret();
            var profiles = await client.ListProfilesAsync(accountId);
            if (profiles.Count == 0)
            {
                MarkLoginVerified(accountId);
                return "No profiles found for this account.";
            }

            if (!Guid.TryParse(_profileId?.Text, out _))
            {
                _profileId!.Text = profiles[0].ProfileId.ToString();
                SaveSettings();
            }

            MarkLoginVerified(accountId);
            return string.Join(
                "\n",
                profiles.Select(profile =>
                    $"{profile.ProfileName}: {profile.ProfileId} ({profile.SchemaVersion})"));
        });
    }

    private async Task DownloadProfileAsync()
    {
        await RunCloudActionAsync("Downloading profile...", includeServerApiKey: false, action: async client =>
        {
            var accountId = ReadAccountId();
            _ = ReadAccountSecret();
            var profileId = ReadProfileId();
            var response = await client.DownloadProfileSnapshotAsync(accountId, profileId);
            MarkLoginVerified(accountId);
            if (response is null)
            {
                return $"Profile {profileId} does not exist yet.";
            }

            var snapshot = response.Snapshot;
            return $"Downloaded {response.ProfileName}: {snapshot.Tasks.Count} task(s), {snapshot.Rewards.Count} reward(s), {snapshot.Tags.Count} tag(s).";
        });
    }

    private async Task RunCloudActionAsync(
        string pendingMessage,
        bool includeServerApiKey,
        Func<TaskAppCloudClient, Task<string>> action)
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

            var client = new TaskAppCloudClient(
                httpClient,
                includeServerApiKey ? _apiKey?.Text : null,
                includeServerApiKey ? null : _accountSecret?.Text);
            var result = await action(client);
            SetStatus(result);
        }
        catch (Exception ex)
        {
            UpdateLoginStatus();
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

    private string ReadAccountSecret()
    {
        var accountSecret = _accountSecret?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(accountSecret))
        {
            return accountSecret;
        }

        throw new InvalidOperationException("Enter the account secret from desktop cloud settings.");
    }

    private Guid ReadProfileId()
    {
        if (Guid.TryParse(_profileId?.Text, out var profileId))
        {
            return profileId;
        }

        throw new InvalidOperationException("Enter a profile ID or list profiles first.");
    }

    private Guid GetOrCreateProfileId()
    {
        if (Guid.TryParse(_profileId?.Text, out var profileId))
        {
            return profileId;
        }

        profileId = Guid.NewGuid();
        _profileId!.Text = profileId.ToString();
        SaveSettings();
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
            ?.PutString(AccountSecretKey, _accountSecret?.Text?.Trim() ?? string.Empty)
            ?.PutString(AccountIdKey, _accountId?.Text?.Trim() ?? string.Empty)
            ?.PutString(ProfileIdKey, _profileId?.Text?.Trim() ?? string.Empty)
            ?.Apply();

        UpdateLoginStatus();
        SetStatus("Settings saved.");
    }

    private void MarkLoginVerified(AccountResponse account)
    {
        MarkLoginVerified(account.Id, account.DisplayName);
    }

    private void MarkLoginVerified(Guid accountId, string displayName = "")
    {
        UpdateLoginStatus(true, accountId, displayName);
    }

    private void UpdateLoginStatus(bool verified = false, Guid? accountId = null, string displayName = "")
    {
        if (_loginStatus is not null)
        {
            _loginStatus.Text = BuildLoginStatus(verified, accountId, displayName);
        }
    }

    private string BuildLoginStatus(bool verified = false, Guid? accountId = null, string displayName = "")
    {
        if (verified && accountId is Guid verifiedAccountId)
        {
            var name = string.IsNullOrWhiteSpace(displayName)
                ? "cloud account"
                : displayName.Trim();
            return $"Cloud login: verified as {name} ({ShortAccountId(verifiedAccountId)}).";
        }

        if (HasSavedAccountCredentials())
        {
            return "Cloud login: saved credentials are not verified. Tap Login.";
        }

        return "Cloud login: not logged in.";
    }

    private bool HasSavedAccountCredentials()
    {
        return Uri.TryCreate(NormalizeApiUrl(_apiUrl?.Text), UriKind.Absolute, out _) &&
            Guid.TryParse(_accountId?.Text, out _) &&
            !string.IsNullOrWhiteSpace(_accountSecret?.Text);
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
