# Architecture

TaskApp follows the MVVM pattern using Avalonia UI. The solution now separates reusable domain/data code into `TaskApp.Core` so the desktop app, future cloud API, and future Android client can share the same task logic and data contracts.

## Solution Layout

```text
TaskApp.slnx
|-- TaskApp.Core/              # Shared domain, DTOs, mappers, and service contracts
|   |-- Models/                # Task, reward, tag, user, profile, and log models
|   |-- Data/                  # Serialization DTOs
|   `-- Services/              # Mappers, store interfaces, snapshot contract
|-- TaskApp/                   # Avalonia desktop application
|   |-- App.axaml(.cs)         # Application entry, lifecycle, day detection
|   |-- ViewModels/            # Presentation logic and commands
|   |-- Views/                 # Avalonia XAML + code-behind
|   |-- Services/              # Local JSON/SQLite/user/settings infrastructure
|   |-- Converters/            # Avalonia value converters
|   `-- Assets/                # App resources and screenshots
`-- TaskApp.Tests/             # xUnit test project
```

## Layer Diagram

```text
Views (TaskApp)
  -> ViewModels (TaskApp)
    -> ITaskAppDataStore / ILocalUserCatalog (TaskApp.Core)
      -> StorageService / UserService (TaskApp desktop implementation)
        -> Local JSON + SQLite files

Models, DTOs, mappers, and snapshot contracts live in TaskApp.Core.
```

## Application Lifecycle

### Startup

1. `UserService` loads synchronously to determine the active user.
2. `StorageService` is constructed with the user catalog and derives the active user's data directory.
3. `MainWindowViewModel` receives `ITaskAppDataStore` and `ILocalUserCatalog`.
4. Settings are loaded and the theme is applied.
5. Task, reward, tag, profile, and log data are loaded through `ITaskAppDataStore`.
6. If the user's `LastActiveDate` is before today, the New Day flow runs.
7. `DayDetectionService` starts polling for midnight crossings.

### Shutdown

1. `MainWindow.Closing` stops the activity timer, logs current session duration, and saves data asynchronously.
2. If graceful shutdown does not run, `AppDomain.ProcessExit` calls `EmergencySaveSync()`.

## Data Flow: Task Completion

```text
User completes a DailyTask
  -> View command invokes MainWindowViewModel
    -> DailyTask.Complete() updates domain state
    -> MainWindowViewModel.AddGold(amount) credits the user
    -> ITaskAppDataStore.SaveTasksAsync() persists through the active store
    -> ITaskAppDataStore.AddLogEntryAsync() records the LogEntry
    -> RefreshFilter() re-sorts and re-filters visible collections
```

## Cloud Foundation

The desktop app still uses local JSON and SQLite through `StorageService`, but the view models now depend on shared interfaces:

| Contract | Purpose |
|---|---|
| `ITaskAppDataStore` | Persistence boundary for tasks, rewards, tags, profile data, logs, merge, undo, and activity-duration queries |
| `IUserCatalog` | Cloud-ready user/account boundary |
| `ILocalUserCatalog` | Desktop-specific user catalog with local directories and import/export |
| `TaskAppDataSnapshot` | Canonical full-user-data payload for future API bootstrap/import/sync |

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Shared `TaskApp.Core` project | Allows desktop, future API, and future Android clients to reuse domain logic and DTOs |
| Store interfaces | Keeps view models from depending directly on local JSON/SQLite infrastructure |
| `TaskAppDataSnapshot` | Provides one canonical payload for future cloud upload/import/bootstrap sync |
| JSON for tasks/rewards/tags | Human-readable, easy to inspect and recover manually |
| SQLite for logs | Efficient for append-heavy time-series data and range queries |
| Atomic file writes | Prevents data loss on crash during write |
| Backup rotation | One-deep `.bak` files provide recovery from the last known-good state |
| Mapper classes | Explicit control over serialization, decoupled from domain classes |
