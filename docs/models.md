# Domain Models

All domain entities live under `TaskApp/Models/`. The base class `DomainEntity` provides shared identity, metadata, and tagging.

## Class Hierarchy

```
DomainEntity (abstract)
├── TaskBase (abstract)
│   ├── HabitTask
│   ├── DailyTask
│   └── TodoTask
└── Reward
```

Other model classes:

- `UserProfile` — per-user gold balance, sort preferences, last active date
- `User` — user identity (name, ID, creation timestamp)
- `Tag` — reusable label attached to tasks and rewards
- `ChecklistItem` — sub-item within a `TodoTask`
- `StreakBonusRule` — gold bonus rule based on streak milestones
- `LogEntry` — immutable audit record of every user action

---

## DomainEntity

**File:** `TaskApp/Models/DomainEntity.cs`

Abstract base class implementing `INotifyPropertyChanged`.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier (auto-generated) |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `Title` | `string` | Display name |
| `Notes` | `string?` | Optional description |
| `Tags` | `List<Tag>` | Associated tags |
| `IsHidden` | `bool` | Whether the item is hidden from default views |

**Methods:** `SetHidden(bool)`

---

## TaskBase

**File:** `TaskApp/Models/Tasks/TaskBase.cs`

Abstract base for all task types. Extends `DomainEntity`.

| Property | Type | Description |
|---|---|---|
| `GoldReward` | `double` | Gold earned on completion (minimum 0, default 0.1) |
| `LastCompletedDate` | `DateTimeOffset?` | When the task was last completed |
| `Type` | `TaskType` | Abstract — returns the task's type enum |
| `IsRewardGoalMet` | `bool` | Virtual — `true` if `LastCompletedDate` has a value |

**Methods:**

| Method | Description |
|---|---|
| `Complete(DateTimeOffset?)` | Marks the task as completed |
| `ResetRewardProgress()` | Virtual — override to reset progress (used by habits) |
| `SetGoldReward(double)` | Sets the gold reward (clamped to ≥ 0) |
| `UpdateTitle(string)` | Updates the task title |
| `UpdateNotes(string?)` | Updates the task notes |
| `UpdateTags(IEnumerable<Tag>)` | Replaces the tag list |

---

## HabitTask

**File:** `TaskApp/Models/Tasks/HabitTask.cs`

A counter-based task that can be incremented/decremented.

| Property | Type | Default | Description |
|---|---|---|---|
| `Count` | `double` | 0 | Current counter value |
| `IncrementAmount` | `double` | 1.0 | Amount added per increment |
| `IncrementEnabled` | `bool` | `true` | Whether the + button is shown |
| `DecrementEnabled` | `bool` | `false` | Whether the − button is shown |
| `ResetCadence` | `HabitResetCadence` | `Never` | Auto-reset schedule |
| `LastResetPeriod` | `DateOnly?` | `null` | Period of last auto-reset |

### HabitResetCadence Enum

| Value | Behavior |
|---|---|
| `Never` | Counter never resets |
| `Daily` | Resets at the start of each day |
| `Weekly` | Resets at the start of each week |
| `Monthly` | Resets at the start of each month |

---

## DailyTask

**File:** `TaskApp/Models/Tasks/DailyTask.cs`

A recurring task with streak tracking and configurable repeat schedule.

| Property | Type | Default | Description |
|---|---|---|---|
| `Cadence` | `RepeatCadence` | `Daily` | Repeat frequency |
| `RepeatEvery` | `int` | 1 | Interval multiplier (e.g., every 2 weeks) |
| `CurrentStreak` | `int` | 0 | Consecutive periods completed |
| `BestStreak` | `int` | 0 | All-time best streak |
| `LastCompletionPeriod` | `DateOnly?` | `null` | Period when last completed |
| `RewardGoalFulfilled` | `bool` | `false` | Whether the reward goal is met for the current period |
| `AutocompleteTimeThreshold` | `TimeSpan?` | `null` | If set, auto-completes when logged activity time exceeds this |

### RepeatCadence Enum

| Value | Period Length |
|---|---|
| `Daily` | 1 day × `RepeatEvery` |
| `Weekly` | 1 week × `RepeatEvery` |
| `Monthly` | 1 month × `RepeatEvery` |
| `Yearly` | 1 year × `RepeatEvery` |

### StreakBonusRule

**File:** `TaskApp/Models/Tasks/StreakBonusRule.cs`

Defines a bonus gold percentage awarded when a streak milestone is reached.

| Property | Type | Description |
|---|---|---|
| `StreakGoal` | `int` | Streak count threshold (minimum 1) |
| `BonusPercent` | `double` | Extra gold percentage (e.g., 10 = +10%) |

Default rules: `[7 → +10%, 14 → +20%, 30 → +30%]`

---

## TodoTask

**File:** `TaskApp/Models/Tasks/TodoTask.cs`

A one-off task with an optional due date and checklist.

| Property | Type | Description |
|---|---|---|
| `DueDate` | `DateTimeOffset?` | Optional deadline |
| `Checklist` | `ObservableCollection<ChecklistItem>` | Sub-items |

### ChecklistItem

**File:** `TaskApp/Models/Tasks/ChecklistItem.cs`

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Text` | `string` | Item text |
| `IsCompleted` | `bool` | Completion state |

---

## Reward

**File:** `TaskApp/Models/Rewards/Reward.cs`

A purchasable reward that costs gold. Extends `DomainEntity`.

| Property | Type | Description |
|---|---|---|
| `GoldCost` | `double` | Gold required to claim (minimum 0) |
| `IsClaimed` | `bool` | Whether claimed (always `false` for repeatable rewards) |
| `IsRepeatable` | `bool` | Whether the reward can be claimed multiple times |
| `ClaimCount` | `int` | Total number of claims |
| `ClaimedAt` | `DateTimeOffset?` | Timestamp of the most recent claim |

**Methods:**

| Method | Description |
|---|---|
| `CanClaim(double availableGold)` | Returns `true` if the user can afford it and it's claimable |
| `TryClaim(double availableGold, DateTimeOffset?)` | Attempts to claim; returns `true` on success |
| `SetGoldCost(double)` | Updates the gold cost |
| `SetRepeatable(bool)` | Toggles repeatability |

---

## Tag

**File:** `TaskApp/Models/Tags/Tag.cs`

A reusable label identified by GUID. Implements `IEquatable<Tag>` (equality by `Id`).

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Display name |

---

## UserProfile

**File:** `TaskApp/Models/UserProfile.cs`

Stores per-user state. Implements `INotifyPropertyChanged`.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Profile identifier |
| `Gold` | `double` | Current gold balance |
| `LastActiveDate` | `DateOnly?` | Last date data was saved (used for new-day detection) |
| `HabitsSortMode` | `string` | Persisted sort preference for habits |
| `DailiesSortMode` | `string` | Persisted sort preference for dailies |
| `TodosSortMode` | `string` | Persisted sort preference for todos |
| `RewardsSortMode` | `string` | Persisted sort preference for rewards |

---

## User

**File:** `TaskApp/Models/User.cs`

Identity record for a user profile.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Display name |
| `CreatedAt` | `DateTimeOffset` | Account creation timestamp |

### UserExportMetadata

Embedded in `.taskapp` export archives.

| Property | Type | Description |
|---|---|---|
| `ExportedAt` | `DateTimeOffset` | When the export was created |
| `AppVersion` | `string` | Application version at export time |
| `UserName` | `string` | Original user name |
| `OriginalUserId` | `Guid` | Original user GUID |

---

## LogEntry

**File:** `TaskApp/Models/Logs/LogEntry.cs`

Immutable audit record stored in SQLite.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Timestamp` | `DateTimeOffset` | When the action occurred |
| `Type` | `LogType` | Category of the action |
| `TaskId` | `Guid?` | Associated task (if applicable) |
| `RewardId` | `Guid?` | Associated reward (if applicable) |
| `GoldDelta` | `double` | Gold change (positive = earned, negative = spent) |
| `UserGold` | `double` | User's total gold after the delta was applied |
| `CountDelta` | `double?` | Counter change for habit increments |
| `Duration` | `TimeSpan?` | Logged activity duration |
| `TitleSnapshot` | `string` | Title at the time of the action |

### LogType Enum

| Value | Description |
|---|---|
| `DailyCompleted` | A daily task was completed |
| `HabitIncremented` | A habit counter was incremented |
| `TodoCompleted` | A todo task was completed |
| `RewardClaimed` | A reward was claimed |
| `ActivityDuration` | An activity timer session was logged |

---

## TaskType Enum

**File:** `TaskApp/Models/Tasks/TaskBase.cs`

| Value | Description |
|---|---|
| `Todo` | One-off task |
| `Daily` | Recurring task |
| `Habit` | Counter-based task |
