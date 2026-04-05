# TaskApp Documentation

**TaskApp** is a cross-platform desktop productivity application built with [Avalonia UI](https://avaloniaui.net/) and .NET 10. It combines task management with a gamification layer — users earn gold by completing tasks and spend it on custom rewards. The project multi-targets `net10.0` and `net10.0-windows10.0.19041.0` — Windows builds include native toast notifications via `Microsoft.Toolkit.Uwp.Notifications`, while macOS and Linux builds compile without the notification package (the notification call is a silent no-op). The `build-all.ps1` script publishes self-contained binaries for all platforms automatically.

## Features

- **Habits** — Trackable counters with configurable increment/decrement and auto-reset cadences (daily, weekly, monthly).
- **Dailies** — Recurring tasks with flexible repeat schedules (daily, weekly, monthly, yearly), streak tracking, and streak bonus rules.
- **Todos** — One-off tasks with optional due dates and checklists.
- **Rewards** — Custom rewards purchased with earned gold; supports one-time and repeatable rewards.
- **Gold Economy** — Earn gold by completing tasks, spend it on rewards. Streak bonuses amplify daily task payouts.
- **Activity Timer** — Built-in stopwatch that logs time spent on any task or reward, with daily autocomplete support.
- **Graphs & Analytics** — Visualize completion history, gold trends, and activity durations over time using ScottPlot.
- **Tags & Filtering** — Organize tasks and rewards with tags; filter and sort by multiple criteria.
- **Multi-User Profiles** — Create, switch, rename, delete, import, and export user profiles.
- **Undo System** — Undo any logged action (completions, claims) with full state reversal.
- **New Day Detection** — Automatic detection of day changes with a review window for missed dailies.
- **Theming** — Light, Dark, and System theme modes.
- **Data Safety** — Atomic writes with `.tmp` → rename pattern, `.bak` backup rotation, and corruption detection with automatic fallback.
- **Vacation Mode** — Protects all daily streaks during absences without gold cost.
- **Notifications** — Windows toast notifications for autocomplete events.

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | [Avalonia UI 11.3](https://avaloniaui.net/) with Fluent theme |
| Target Framework | .NET 10 (`net10.0` + `net10.0-windows10.0.19041.0`\*) |
| Charting | [ScottPlot 5](https://scottplot.net/) (Avalonia integration) |
| Database | [SQLite](https://www.sqlite.org/) via `Microsoft.Data.Sqlite` |
| Notifications | [Microsoft.Toolkit.Uwp.Notifications](https://www.nuget.org/packages/Microsoft.Toolkit.Uwp.Notifications) |
| Rendering | SkiaSharp (with Linux and macOS native assets) |
| Testing | xUnit with Avalonia.Headless, Coverlet |
| Pattern | MVVM (Model-View-ViewModel) |

\* The project multi-targets both TFMs. The Windows-specific TFM is needed only by the `Microsoft.Toolkit.Uwp.Notifications` package and is selected automatically for Windows RIDs by `build-all.ps1`. Non-Windows RIDs use the plain `net10.0` TFM — notification code is conditionally compiled out via the `WINDOWS_NOTIFICATIONS` preprocessor symbol.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An IDE such as Visual Studio 2026, Visual Studio Code, or JetBrains Rider

### Build & Run

```bash
# Clone the repository
git clone https://github.com/hyi96/TaskApp.git
cd TaskApp

# Build
dotnet build

# Run
dotnet run --project TaskApp
```

### Run Tests

```bash
dotnet test
```

## Project Structure

```
TaskApp/
├── TaskApp/                  # Main application project
│   ├── Models/               # Domain entities (tasks, rewards, tags, logs)
│   ├── ViewModels/           # MVVM view models
│   ├── Views/                # Avalonia XAML views and code-behind
│   ├── Services/             # Storage, user management, settings, mappers
│   ├── Converters/           # Avalonia value converters
│   ├── Data/                 # Serialization DTOs (data transfer objects)
│   └── Assets/               # Application resources
├── TaskApp.Tests/            # xUnit test project
└── docs/                     # Documentation (you are here)
```

## Documentation Index

| Document | Description |
|---|---|
| [Architecture](architecture.md) | Solution structure, layers, and data flow |
| [Domain Models](models.md) | Entity reference — tasks, rewards, tags, logs |
| [Services](services.md) | Service layer — storage, users, settings, mappers |
| [Features](features.md) | Detailed feature guide with usage details |
| [Data Storage](data-storage.md) | Persistence, backup/recovery, import/export |
| [Testing](testing.md) | Test project overview and how to run tests |
| [Contributing](contributing.md) | Build instructions, coding conventions, and guidelines |

## Data Location

User data is stored under the OS-specific local application data folder:

| OS | Path |
|---|---|
| Windows | `%LOCALAPPDATA%\TaskApp\` |
| macOS | `~/.local/share/TaskApp/` |
| Linux | `~/.local/share/TaskApp/` |

Each user profile has its own subdirectory under `Users/{userId}/`.

## License

See the repository root for license information.
