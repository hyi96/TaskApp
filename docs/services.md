# Services

The service layer lives in `TaskApp/Services/` and handles persistence, user management, settings, day detection, notifications, and data mapping.

---

## StorageService

**File:** `TaskApp/Services/StorageService.cs`

Central persistence service. Reads and writes all per-user data.

### Data Files

All files are stored in the active user's data directory (`Users/{userId}/`).

| File | Format | Contents |
|---|---|---|
| `tasks.json` | JSON | All tasks (habits, dailies, todos) |
| `rewards.json` | JSON | All rewards |
| `tags.json` | JSON | All tags |
| `user.json` | JSON | User profile (gold, sort preferences, last active date) |
| `logs.db` | SQLite | Log entries (completions, claims, streak protections, activity durations) |

### Write Safety

All JSON writes use an atomic three-step process:

1. If the target file exists, copy it to `{file}.bak` (backup rotation).
2. Write new content to `{file}.tmp`.
3. Rename `{file}.tmp` → `{file}` (atomic on most filesystems).

If a crash occurs mid-write, the `.bak` file holds the last known-good state.

### Read Fallback

On read, if the primary file is missing or corrupted (empty, null-byte filled), the service falls back to the `.bak` copy automatically.

### Key Methods

| Method | Description |
|---|---|
| `LoadTasksAsync()` | Loads all tasks from `tasks.json`, maps via `TaskMapper` |
| `SaveTasksAsync(List<TaskBase>)` | Serializes tasks via `TaskMapper` and writes atomically |
| `LoadRewardsAsync()` | Loads rewards from `rewards.json` via `RewardMapper` |
| `SaveRewardsAsync(List<Reward>)` | Serializes rewards and writes atomically |
| `LoadTagsAsync()` | Loads tags from `tags.json` |
| `SaveTagsAsync(List<Tag>)` | Writes tags atomically |
| `LoadUserProfileAsync()` | Loads the user profile from `user.json` |
| `SaveUserProfileAsync(UserProfile)` | Writes the user profile atomically |
| `AddLogEntryAsync(LogEntry)` | Inserts a log entry into `logs.db` |
| `AddLogEntrySync(LogEntry)` | Synchronous variant for emergency shutdown saves |
| `LoadRecentLogEntriesAsync(int)` | Loads the N most recent log entries |
| `LoadFilteredLogEntriesAsync(int, DateTimeOffset, DateTimeOffset)` | Loads log entries within a date range |
| `LoadAllLogEntriesAsync()` | Loads all log entries ordered by timestamp |
| `FindPreviousLogEntryAsync(...)` | Finds the most recent log of a given type for a task/reward |
| `DeleteLogEntryAsync(Guid)` | Deletes a log entry by ID |
| `GetActivityDurationForTaskSinceAsync(Guid, DateTimeOffset)` | Sums logged activity duration for a task since a timestamp |
| `MergeActivityLogEntriesAsync(string, Guid?, Guid?)` | Reassigns orphaned log entries matching a title to a task/reward |
| `SaveAllSync(...)` | Synchronous save of all data (used during emergency shutdown) |
| `RefreshDataDirectory()` | Updates the data directory after a user switch |

### SQLite Schema

The `LogEntries` table is created on first use with `CREATE TABLE IF NOT EXISTS`. Columns are added via `ALTER TABLE` as the schema evolves (`EnsureColumnsExistAsync`). Table initialization is cached per database path — `EnsureLogsTableAsync` only runs the schema check once per session (or until `RefreshDataDirectory()` is called).

---

## UserService

**File:** `TaskApp/Services/UserService.cs`

Manages multi-user profiles, user switching, and import/export.

### Storage

- `users.json` — list of all `User` records.
- `current_user.json` — GUID of the active user.
- Both files use the same `.bak` backup pattern.

### Key Methods

| Method | Description |
|---|---|
| `LoadSync()` | Synchronous load for app startup (avoids async deadlocks) |
| `LoadAsync()` | Async load variant |
| `CreateUserAsync(string name)` | Creates a new user and their data directory |
| `DeleteUserAsync(Guid)` | Deletes a user and their data (prevents deleting the last user) |
| `SwitchUserAsync(Guid)` | Switches the active user and fires `CurrentUserChanged` |
| `RenameUserAsync(Guid, string)` | Renames a user |
| `ExportUserAsync(Guid, string)` | Exports user data to a `.taskapp` ZIP archive |
| `ImportUserAsync(string, string?)` | Imports a `.taskapp` archive as a new user |
| `GetUserDataDirectory(Guid)` | Returns the path to a user's data directory |
| `GetCurrentUserDataDirectory()` | Returns the active user's data directory path |

### Legacy Migration

On first load, if per-user data directories don't exist but legacy files exist in the root `AppData` folder, the service migrates them into the user's directory automatically.

### Import/Export Format

The `.taskapp` export is a ZIP archive containing:

```
metadata.json          # UserExportMetadata (export timestamp, app version, user name)
data/
  tasks.json
  rewards.json
  tags.json
  user.json
  logs.db
```

On import, a new user is created with a fresh GUID to avoid ID collisions. The user name is auto-deduplicated if it already exists.

### Events

| Event | Description |
|---|---|
| `CurrentUserChanged` | Fired after `SwitchUserAsync` completes; triggers data reload in the ViewModel |

---

## SettingsService

**File:** `TaskApp/Services/SettingsService.cs`

Singleton service for app-wide settings.

### Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `ThemeMode` | `ThemeMode` | `System` | UI theme (Light, Dark, System) |

### ThemeMode Enum

| Value | Behavior |
|---|---|
| `Light` | Forces light theme |
| `Dark` | Forces dark theme |
| `System` | Follows OS preference |

### Storage

Settings are stored in `settings.json` in the root app data folder (not per-user). Writes use the atomic `.tmp` → rename pattern.

### Events

| Event | Description |
|---|---|
| `ThemeChanged` | Fired when `ThemeMode` is set; `App.axaml.cs` subscribes to apply the theme |

---

## DayDetectionService

**File:** `TaskApp/Services/DayDetectionService.cs`

Timer-based service that detects midnight crossings. Implements `IDisposable`.

### Behavior

- Uses `System.Threading.Timer` polling every 60 seconds.
- Compares `DateTime.Now.Date` against the last checked date.
- When a new day is detected, invokes the `NewDayDetected` async event.

### Methods

| Method | Description |
|---|---|
| `Start()` | Starts the polling timer (no-op if already running) |
| `Stop()` | Stops and disposes the timer |
| `Dispose()` | Stops the timer if not already disposed |

### Events

| Event | Description |
|---|---|
| `NewDayDetected` | `Func<Task>` — async event fired when the date advances |

---

## NotificationService

**File:** `TaskApp/Services/NotificationService.cs`

Static service for showing native Windows toast notifications.

### Behavior

- **Windows only** — uses `Microsoft.Toolkit.Uwp.Notifications` (`ToastContentBuilder`) to show toast notifications. The notification code is conditionally compiled via `#if WINDOWS_NOTIFICATIONS` (defined only for the `net10.0-windows10.0.19041.0` TFM), so non-Windows builds compile without any UWP dependency.
- A runtime `OperatingSystem.IsWindows()` guard provides an additional safety check.
- Best-effort — failures are silently caught to never crash the app.
- Used for daily task autocomplete notifications.

### Methods

| Method | Description |
|---|---|
| `Show(string title, string body)` | Displays a toast notification (Windows TFM) or is an empty method (non-Windows TFM) |

---

## TaskMapper

**File:** `TaskApp/Services/TaskMapper.cs`

Static mapper that converts between `TaskData` DTOs and `TaskBase` domain models.

| Method | Description |
|---|---|
| `ToModel(TaskData)` | Converts a DTO to the correct `TaskBase` subclass |
| `ToData(TaskBase)` | Converts a domain model to the correct `TaskData` subclass |

Handles all three task types (`TodoTaskData`, `DailyTaskData`, `HabitTaskData`) with their type-specific properties.

---

## RewardMapper

**File:** `TaskApp/Services/RewardMapper.cs`

Static mapper that converts between `RewardData` DTOs and `Reward` domain models.

| Method | Description |
|---|---|
| `ToModel(RewardData)` | Converts a DTO to a `Reward` |
| `ToData(Reward)` | Converts a `Reward` to a DTO |
