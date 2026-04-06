# Architecture

TaskApp follows the **MVVM (Model-View-ViewModel)** pattern using Avalonia UI. The solution consists of two projects that communicate through clearly defined layers.

## Solution Layout

```
TaskApp.sln
├── TaskApp/                  # WinExe — the desktop application
│   ├── App.axaml(.cs)        # Application entry, lifecycle, day detection
│   ├── Program.cs            # Avalonia bootstrapper
│   ├── Models/               # Domain entities (pure C#, no UI dependencies)
│   │   ├── Tasks/            # TaskBase, HabitTask, DailyTask, TodoTask, ChecklistItem, StreakBonusRule
│   │   ├── Rewards/          # Reward
│   │   ├── Tags/             # Tag
│   │   ├── Logs/             # LogEntry
│   │   ├── DomainEntity.cs   # Abstract base for all titled/tagged entities
│   │   ├── UserProfile.cs    # Per-user gold, vacation mode, sort preferences, last active date
│   │   └── User.cs           # User identity + export metadata
│   ├── ViewModels/           # Presentation logic (binds Models ↔ Views)
│   │   ├── MainWindowViewModel.cs        # Central orchestrator
│   │   ├── CurrentActivityViewModel.cs   # Stopwatch / activity timer
│   │   ├── GraphViewModel.cs             # Chart data and filtering
│   │   ├── *FormViewModel.cs             # Task/reward editing forms
│   │   ├── NewDayViewModel.cs            # Missed-daily review
│   │   ├── SettingsViewModel.cs          # Theme and app settings
│   │   ├── TagsViewModel.cs              # Tag management
│   │   ├── LogsViewModel.cs              # Log history display
│   │   └── ViewModelBase.cs              # INotifyPropertyChanged base
│   ├── Views/                # Avalonia XAML + code-behind
│   │   ├── MainWindow.axaml(.cs)
│   │   ├── TaskFormWindow.axaml(.cs)
│   │   ├── SettingsWindow.axaml(.cs)
│   │   ├── TagsWindow.axaml(.cs)
│   │   ├── NewDayWindow.axaml(.cs)
│   │   ├── GraphWindow.axaml(.cs)
│   │   └── LogsWindow.axaml(.cs)
│   ├── Services/             # Infrastructure (I/O, persistence, mapping)
│   │   ├── StorageService.cs       # JSON + SQLite persistence
│   │   ├── UserService.cs          # Multi-user profile management
│   │   ├── SettingsService.cs      # App-wide settings (singleton)
│   │   ├── DayDetectionService.cs  # Timer-based midnight detection
│   │   ├── NotificationService.cs  # Windows toast notifications
│   │   ├── TaskMapper.cs           # TaskData ↔ TaskBase mapping
│   │   └── RewardMapper.cs         # RewardData ↔ Reward mapping
│   ├── Data/                 # DTOs for JSON serialization
│   │   ├── TaskData.cs / DailyTaskData.cs / HabitTaskData.cs / TodoTaskData.cs
│   │   ├── RewardData.cs
│   │   ├── TagData.cs
│   │   ├── ChecklistItemData.cs
│   │   └── StreakBonusRuleData.cs
│   └── Converters/           # Avalonia IValueConverter implementations
│       ├── BooleanNegationConverter.cs
│       ├── BooleanToTextDecorationConverter.cs
│       ├── DueDateDisplayConverter.cs
│       ├── FilterTabStyleConverter.cs
│       └── HiddenToggleLabelConverter.cs
│
└── TaskApp.Tests/            # xUnit test project (references TaskApp via InternalsVisibleTo)
```

## Layer Diagram

```
┌──────────────────────────────────────────────┐
│                   Views                      │  XAML + code-behind (UI only)
│  MainWindow, TaskFormWindow, GraphWindow...  │
└────────────────────┬─────────────────────────┘
                     │ DataContext binding
┌────────────────────▼─────────────────────────┐
│                ViewModels                    │  Presentation logic, commands
│  MainWindowViewModel, GraphViewModel...      │
└────────────────────┬─────────────────────────┘
                     │ calls
┌────────────────────▼─────────────────────────┐
│                 Services                     │  I/O, persistence, mapping
│  StorageService, UserService, Mappers...     │
└────────────────────┬─────────────────────────┘
                     │ reads/writes
┌────────────────────▼─────────────────────────┐
│              Models / Data                   │  Domain entities + DTOs
│  TaskBase, Reward, UserProfile, LogEntry...  │
└──────────────────────────────────────────────┘
```

## Application Lifecycle

### Startup (`App.axaml.cs`)

1. `UserService` loads synchronously to determine the active user.
2. `StorageService` is constructed with the user service (derives data directory from the active user).
3. `MainWindowViewModel` is created and set as the main window's `DataContext`.
4. On `Startup`:
   - Settings are loaded and the theme is applied.
   - Task/reward/tag data is loaded from disk.
   - If the user's `LastActiveDate` is before today, the **New Day** flow runs (streak evaluation, missed-daily review window).
   - `DayDetectionService` starts polling for midnight crossings.

### Shutdown

1. `MainWindow.Closing` fires — the activity timer is stopped, current session duration is logged, and all data is saved asynchronously.
2. If the graceful path doesn't fire (crash/kill), `AppDomain.ProcessExit` calls `EmergencySaveSync()` which writes everything synchronously as a last resort.

### New Day Detection

`DayDetectionService` uses a `System.Threading.Timer` polling every 60 seconds. When `DateTime.Now.Date` advances past the last checked date:

1. Uncompleted dailies from the previous period are collected (gap-based filter using `LastCompletionPeriod`).
2. If vacation mode is enabled, all daily streaks are silently protected at no gold cost.
3. Otherwise, if any uncompleted dailies exist, the **New Day Window** is shown for review (check or protect each daily).
4. All tasks are refreshed for the new period (daily cadence checks, habit counter resets, streak updates).
5. Data is saved.

## Data Flow: Task Completion Example

```
User clicks "Complete" on a DailyTask
  → View command invokes ViewModel method
    → DailyTask.Complete() updates domain state (streak, completion period)
    → MainWindowViewModel.AddGold(amount) credits the user
    → StorageService.SaveTasksAsync() persists to tasks.json
    → StorageService.AddLogEntryAsync() writes a LogEntry to logs.db
    → RefreshFilter() re-sorts and re-filters the displayed collection
```

## Key Design Decisions

| Decision | Rationale |
|---|---|
| JSON for tasks/rewards/tags | Human-readable, easy to inspect and recover manually |
| SQLite for logs | Efficient for append-heavy time-series data and range queries |
| Atomic file writes (`.tmp` → rename) | Prevents data loss on crash during write |
| `.bak` backup rotation | One-deep backup provides recovery from the last known-good state |
| `InternalsVisibleTo` for tests | Allows tests to access `internal` setters on domain entities |
| Mapper classes (not attributes) | Explicit control over serialization, decouples domain from persistence |
| Singleton `SettingsService` | App-wide settings (theme) shared across all windows |
| `DomainEntity` base class | Shared identity (`Id`), metadata (`CreatedAt`), title, notes, tags, and hidden flag |
