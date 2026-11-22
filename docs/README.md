# TaskApp Documentation

**TaskApp** combines a cross-platform [Avalonia UI](https://avaloniaui.net/) desktop app, a native Android client, and an optional ASP.NET Core API for sharing profiles. The desktop app stores data locally and runs without a cloud account.

## Features

- **Habits** — Trackable counters with configurable increment/decrement and auto-reset cadences (daily, weekly, monthly).
- **Dailies** — Recurring tasks with flexible repeat schedules (daily, weekly, monthly, yearly), streak tracking, and streak bonus rules.
- **Todos** — One-off tasks with optional due dates and checklists.
- **Rewards** — Custom rewards purchased with earned gold; supports one-time and repeatable rewards.
- **Gold Economy** — Earn gold by completing tasks, spend it on rewards. Streak bonuses amplify daily task payouts.
- **Streak Protection** — Pay a configurable gold cost per missed period to preserve daily streaks instead of losing them.
- **Activity Timer** — Built-in stopwatch that logs time spent on any task or reward, with daily autocomplete support.
- **Graphs & Analytics** — Visualize completion history, gold trends, and activity durations over time using ScottPlot.
- **Tags & Filtering** — Organize tasks and rewards with tags; filter and sort by multiple criteria.
- **Multi-User Profiles** — Create, switch, rename, delete, import, and export user profiles.
- **Undo System** — Undo any logged action (completions, claims, streak protections) with full state reversal.
- **New Day Detection** — Automatic detection of day changes with a review window to check off or protect missed dailies (supports multi-day gaps).
- **Theming** — Light, Dark, Forest, and Follow System. Forest uses a matching native frame on Windows 11, and each theme uses the same compact scrollbars.
- **Cloud Profiles** — Upload or download profile snapshots in desktop Settings. Use the profile picker on Android to switch between uploaded profiles.
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

# Run the desktop app on Windows
dotnet run --project TaskApp/TaskApp.csproj -f net10.0-windows10.0.19041.0
```

Use `-f net10.0` on macOS and Linux. See [Contributing](contributing.md#building) for desktop release builds and [Android APK](android-apk.md) for the Android workload and packaging instructions.

### Run Tests

```bash
dotnet test TaskApp.Tests/TaskApp.Tests.csproj
```

## Project Structure

For the current exact layout, see [Architecture](architecture.md). The shared domain/data layer lives in `TaskApp.Core`, while `TaskApp` contains the Avalonia desktop app and local infrastructure.

```text
TaskApp.Core/               # Shared domain, DTOs, mappers, and service contracts
TaskApp.Api/                # ASP.NET Core API for account and profile snapshots
TaskApp.Android/            # Native Android app
TaskApp/                    # Avalonia desktop app and local infrastructure
TaskApp.Tests/              # xUnit test project
docs/                       # Documentation (you are here)
```

## Documentation Index

| Document | Description |
|---|---|
| [Architecture](architecture.md) | Solution structure, layers, and data flow |
| [Cloud Foundation](cloud-foundation.md) | Shared-core interfaces and how profile snapshots are used |
| [Cloud API](cloud-api.md) | Local API run instructions and profile snapshot endpoints |
| [Android APK](android-apk.md) | Install, sign in, switch profiles, and build the Android app |
| [VPS Deployment](../deploy/vps/README.md) | Deploy the Cloud API with Docker Compose and Caddy |
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
