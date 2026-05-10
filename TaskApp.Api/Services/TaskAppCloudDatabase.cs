using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TaskApp.Data;
using TaskApp.Models;
using TaskApp.Models.Logs;
using TaskApp.Services;

namespace TaskApp.Api.Services;

public sealed class TaskAppCloudDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    private TaskAppCloudDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static TaskAppCloudDatabase FromFile(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        };

        return new TaskAppCloudDatabase(builder.ToString());
    }

    public static TaskAppCloudDatabase FromConnectionString(string connectionString)
    {
        return new TaskAppCloudDatabase(connectionString);
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenConnectionAsync();

        var accountCommand = connection.CreateCommand();
        accountCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """;
        await accountCommand.ExecuteNonQueryAsync();

        var profileCommand = connection.CreateCommand();
        profileCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS Profiles (
                AccountId TEXT NOT NULL,
                ProfileId TEXT NOT NULL,
                ProfileName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                SnapshotSchemaVersion TEXT NOT NULL,
                SnapshotCapturedAt TEXT NOT NULL,
                UserProfileJson TEXT NOT NULL,
                TasksJson TEXT NOT NULL,
                RewardsJson TEXT NOT NULL,
                TagsJson TEXT NOT NULL,
                LogEntriesJson TEXT NOT NULL,
                PRIMARY KEY (AccountId, ProfileId),
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
            );
            """;
        await profileCommand.ExecuteNonQueryAsync();
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var connection = await OpenConnectionAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AccountResponse> CreateAccountAsync(string? displayName)
    {
        var account = new AccountResponse(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(displayName) ? "Desktop account" : displayName.Trim(),
            DateTimeOffset.UtcNow);

        await using var connection = await OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Accounts (Id, DisplayName, CreatedAt)
            VALUES ($id, $displayName, $createdAt);
            """;
        command.Parameters.AddWithValue("$id", account.Id.ToString());
        command.Parameters.AddWithValue("$displayName", account.DisplayName);
        command.Parameters.AddWithValue("$createdAt", ToStore(account.CreatedAt));
        await command.ExecuteNonQueryAsync();

        return account;
    }

    public async Task<bool> AccountExistsAsync(Guid accountId)
    {
        await using var connection = await OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM Accounts WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", accountId.ToString());
        return await command.ExecuteScalarAsync() != null;
    }

    public async Task<IReadOnlyList<ProfileSummaryResponse>> ListProfilesAsync(Guid accountId)
    {
        await using var connection = await OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT AccountId, ProfileId, ProfileName, CreatedAt, UpdatedAt, SnapshotSchemaVersion
            FROM Profiles
            WHERE AccountId = $accountId
            ORDER BY ProfileName COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$accountId", accountId.ToString());

        var profiles = new List<ProfileSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            profiles.Add(new ProfileSummaryResponse(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                ParseDateTimeOffset(reader.GetString(3)),
                ParseDateTimeOffset(reader.GetString(4)),
                reader.GetString(5)));
        }

        return profiles;
    }

    public async Task<ProfileSnapshotResponse?> UpsertProfileSnapshotAsync(
        Guid accountId,
        Guid profileId,
        UpsertProfileSnapshotRequest request)
    {
        if (!await AccountExistsAsync(accountId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var profileName = string.IsNullOrWhiteSpace(request.ProfileName)
            ? "Default"
            : request.ProfileName.Trim();

        await using var connection = await OpenConnectionAsync();
        var existingCreatedAt = await GetProfileCreatedAtAsync(connection, accountId, profileId);
        var createdAt = existingCreatedAt ?? now;

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Profiles (
                AccountId,
                ProfileId,
                ProfileName,
                CreatedAt,
                UpdatedAt,
                SnapshotSchemaVersion,
                SnapshotCapturedAt,
                UserProfileJson,
                TasksJson,
                RewardsJson,
                TagsJson,
                LogEntriesJson
            )
            VALUES (
                $accountId,
                $profileId,
                $profileName,
                $createdAt,
                $updatedAt,
                $schemaVersion,
                $capturedAt,
                $userProfileJson,
                $tasksJson,
                $rewardsJson,
                $tagsJson,
                $logEntriesJson
            )
            ON CONFLICT(AccountId, ProfileId) DO UPDATE SET
                ProfileName = excluded.ProfileName,
                UpdatedAt = excluded.UpdatedAt,
                SnapshotSchemaVersion = excluded.SnapshotSchemaVersion,
                SnapshotCapturedAt = excluded.SnapshotCapturedAt,
                UserProfileJson = excluded.UserProfileJson,
                TasksJson = excluded.TasksJson,
                RewardsJson = excluded.RewardsJson,
                TagsJson = excluded.TagsJson,
                LogEntriesJson = excluded.LogEntriesJson;
            """;
        BindSnapshotParameters(command, accountId, profileId, profileName, createdAt, now, request.Snapshot);
        await command.ExecuteNonQueryAsync();

        return new ProfileSnapshotResponse(accountId, profileId, profileName, createdAt, now, request.Snapshot);
    }

    public async Task<ProfileSnapshotResponse?> GetProfileSnapshotAsync(Guid accountId, Guid profileId)
    {
        await using var connection = await OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                AccountId,
                ProfileId,
                ProfileName,
                CreatedAt,
                UpdatedAt,
                SnapshotSchemaVersion,
                SnapshotCapturedAt,
                UserProfileJson,
                TasksJson,
                RewardsJson,
                TagsJson,
                LogEntriesJson
            FROM Profiles
            WHERE AccountId = $accountId AND ProfileId = $profileId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$accountId", accountId.ToString());
        command.Parameters.AddWithValue("$profileId", profileId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadProfileSnapshot(reader) : null;
    }

    private static void BindSnapshotParameters(
        SqliteCommand command,
        Guid accountId,
        Guid profileId,
        string profileName,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        TaskAppDataSnapshot snapshot)
    {
        command.Parameters.AddWithValue("$accountId", accountId.ToString());
        command.Parameters.AddWithValue("$profileId", profileId.ToString());
        command.Parameters.AddWithValue("$profileName", profileName);
        command.Parameters.AddWithValue("$createdAt", ToStore(createdAt));
        command.Parameters.AddWithValue("$updatedAt", ToStore(updatedAt));
        command.Parameters.AddWithValue("$schemaVersion", snapshot.SchemaVersion);
        command.Parameters.AddWithValue("$capturedAt", ToStore(snapshot.CapturedAt));
        command.Parameters.AddWithValue("$userProfileJson", JsonSerializer.Serialize(snapshot.UserProfile, JsonOptions));
        command.Parameters.AddWithValue("$tasksJson", JsonSerializer.Serialize(snapshot.Tasks, JsonOptions));
        command.Parameters.AddWithValue("$rewardsJson", JsonSerializer.Serialize(snapshot.Rewards, JsonOptions));
        command.Parameters.AddWithValue("$tagsJson", JsonSerializer.Serialize(snapshot.Tags, JsonOptions));
        command.Parameters.AddWithValue("$logEntriesJson", JsonSerializer.Serialize(snapshot.LogEntries, JsonOptions));
    }

    private static ProfileSnapshotResponse ReadProfileSnapshot(SqliteDataReader reader)
    {
        var accountId = Guid.Parse(reader.GetString(0));
        var profileId = Guid.Parse(reader.GetString(1));
        var profileName = reader.GetString(2);
        var createdAt = ParseDateTimeOffset(reader.GetString(3));
        var updatedAt = ParseDateTimeOffset(reader.GetString(4));
        var schemaVersion = reader.GetString(5);
        var capturedAt = ParseDateTimeOffset(reader.GetString(6));

        var snapshot = new TaskAppDataSnapshot(
            schemaVersion,
            capturedAt,
            JsonSerializer.Deserialize<List<TaskData>>(reader.GetString(8), JsonOptions) ?? new List<TaskData>(),
            JsonSerializer.Deserialize<List<RewardData>>(reader.GetString(9), JsonOptions) ?? new List<RewardData>(),
            JsonSerializer.Deserialize<List<TagData>>(reader.GetString(10), JsonOptions) ?? new List<TagData>(),
            JsonSerializer.Deserialize<UserProfile>(reader.GetString(7), JsonOptions) ?? new UserProfile(),
            JsonSerializer.Deserialize<List<LogEntry>>(reader.GetString(11), JsonOptions) ?? new List<LogEntry>());

        return new ProfileSnapshotResponse(accountId, profileId, profileName, createdAt, updatedAt, snapshot);
    }

    private static async Task<DateTimeOffset?> GetProfileCreatedAtAsync(SqliteConnection connection, Guid accountId, Guid profileId)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CreatedAt
            FROM Profiles
            WHERE AccountId = $accountId AND ProfileId = $profileId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$accountId", accountId.ToString());
        command.Parameters.AddWithValue("$profileId", profileId.ToString());
        var value = await command.ExecuteScalarAsync();
        return value is string createdAt ? ParseDateTimeOffset(createdAt) : null;
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string ToStore(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
