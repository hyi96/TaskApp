# Data Storage

TaskApp uses a hybrid persistence strategy: JSON files for structured domain data and SQLite for append-heavy log entries. All writes are designed to be crash-safe.

---

## Storage Location

User data is stored under the OS-specific local application data folder:

| OS | Base Path |
|---|---|
| Windows | `%LOCALAPPDATA%\TaskApp\` |
| macOS | `~/.local/share/TaskApp/` |
| Linux | `~/.local/share/TaskApp/` |

### Directory Structure

```
TaskApp/
├── users.json              # List of all user profiles
├── current_user.json       # GUID of the active user
├── settings.json           # App-wide settings (theme)
└── Users/
    ├── {userId-1}/
    │   ├── tasks.json      # All tasks (habits, dailies, todos)
    │   ├── rewards.json    # All rewards
    │   ├── tags.json       # All tags
    │   ├── user.json       # User profile (gold, sort prefs, last active date)
    │   └── logs.db         # SQLite database for log entries
    └── {userId-2}/
        └── ...
```

---

## JSON Files

### Schema Overview

#### tasks.json

An array of task data objects. Each object includes a discriminator type that maps to the correct subclass:

- `TodoTaskData` — includes `DueDate`, `Checklist`
- `DailyTaskData` — includes `Cadence`, `RepeatEvery`, `CurrentStreak`, `BestStreak`, `LastCompletionPeriod`, `StreakBonusRules`, `AutocompleteTimeThresholdTicks`, `StreakProtectionCost`
- `HabitTaskData` — includes `Count`, `IncrementAmount`, `IncrementEnabled`, `DecrementEnabled`, `ResetCadence`, `LastResetPeriod`

Common fields: `Id`, `CreatedAt`, `Title`, `Notes`, `Tags`, `LastCompletedDate`, `GoldReward`, `IsHidden`

#### rewards.json

An array of `RewardData` objects with fields: `Id`, `CreatedAt`, `Title`, `Notes`, `IsClaimed`, `IsRepeatable`, `ClaimCount`, `ClaimedAt`, `GoldCost`, `Tags`, `IsHidden`

#### tags.json

An array of `TagData` objects with fields: `Id`, `Name`

#### user.json

A `UserProfile` object with fields: `Id`, `Gold`, `IsVacationMode`, `LastActiveDate`, `HabitsSortMode`, `DailiesSortMode`, `TodosSortMode`, `RewardsSortMode`

### Serialization

- All JSON is serialized with `System.Text.Json` using `WriteIndented = true` for human readability.
- Domain models are mapped to/from DTOs via `TaskMapper` and `RewardMapper` (no JSON attributes on domain classes).

---

## SQLite Database

### logs.db

Stores log entries for completions, reward claims, and activity durations.

#### Table: `LogEntries`

| Column | Type | Description |
|---|---|---|
| `Id` | TEXT | Primary key (GUID) |
| `Timestamp` | TEXT | ISO 8601 timestamp |
| `Type` | INTEGER | `LogType` enum value |
| `TaskId` | TEXT | Associated task GUID (nullable) |
| `RewardId` | TEXT | Associated reward GUID (nullable) |
| `GoldDelta` | REAL | Gold change |
| `UserGold` | REAL | User's gold balance after the change |
| `CountDelta` | REAL | Habit counter change (nullable) |
| `DurationTicks` | INTEGER | Activity duration in ticks (nullable) |
| `TitleSnapshot` | TEXT | Entity title at the time of the action |
| `PreviousLastCompletionPeriod` | TEXT | Previous `LastCompletionPeriod` before streak protection (nullable, for undo rollback) |

### Schema Evolution

The table schema is managed via:

1. `CREATE TABLE IF NOT EXISTS` on first access.
2. `PRAGMA table_info` to check existing columns.
3. `ALTER TABLE ADD COLUMN` for any missing columns.

This approach handles forward migration without a formal migration framework.

---

## Write Safety

### Atomic Writes

All JSON file writes follow a three-step pattern to prevent data loss:

```
1. Copy current file → {file}.bak       (backup rotation)
2. Write new content → {file}.tmp        (temporary file)
3. Rename {file}.tmp → {file}            (atomic move)
```

If a crash occurs during step 2, the original file is untouched. If a crash occurs during step 3, the `.bak` file holds the previous state.

### Corruption Detection

On read, files are checked for corruption:

- Empty or whitespace-only content → corrupted
- Content starting with `\0` (null byte) → corrupted (common after unclean shutdown)

If the primary file is corrupted, the service automatically falls back to the `.bak` copy.

### Emergency Save

If the graceful shutdown path doesn't fire (crash, process kill), `AppDomain.ProcessExit` triggers `EmergencySaveSync()`:

1. Stops the activity timer and logs the session duration synchronously.
2. Saves all data (tasks, rewards, tags, user profile) using synchronous I/O.
3. Best-effort — exceptions are caught to avoid crashing the exit sequence.

---

## Backup Files

Each data file has a corresponding `.bak` file that holds the previous version:

```
tasks.json      → tasks.json.bak
rewards.json    → rewards.json.bak
tags.json       → tags.json.bak
user.json       → user.json.bak
users.json      → users.json.bak
current_user.json → current_user.json.bak
```

Backup rotation is one-deep: only the most recent previous version is kept.

---

## Import & Export

### Export Format

User data is exported as a `.taskapp` file (ZIP archive):

```
archive.taskapp
├── metadata.json           # Export metadata
└── data/
    ├── tasks.json
    ├── rewards.json
    ├── tags.json
    ├── user.json
    └── logs.db
```

#### metadata.json

```json
{
  "ExportedAt": "2025-07-11T12:00:00+00:00",
  "AppVersion": "1.0.0",
  "UserName": "Alice",
  "OriginalUserId": "a1b2c3d4-..."
}
```

### Export Process

1. Collect all files from the user's data directory.
2. If a file is locked (e.g., `logs.db` held by SQLite), copy it to a temp file first.
3. Package everything into a ZIP archive.

### Import Process

1. Open the `.taskapp` ZIP archive.
2. Read `metadata.json` for the original user name.
3. Create a new user with a fresh GUID (the original ID is never reused).
4. Auto-deduplicate the user name if it already exists.
5. Extract data files into the new user's directory.
6. Path traversal protection: entries that would escape the target directory are skipped.

---

## Legacy Migration

When the app starts, `UserService` checks for legacy data files in the root app data folder (from before multi-user support was added). If found and non-empty, they are copied into the current user's directory:

- Only files above a minimum size threshold (5 bytes) are considered.
- If the user directory already has a non-empty version, the legacy file is not copied.
- Migration is automatic and transparent to the user.
