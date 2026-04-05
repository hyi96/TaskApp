# Contributing

Guidelines for building, developing, and contributing to TaskApp.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An IDE: Visual Studio 2026, Visual Studio Code (with C# Dev Kit), or JetBrains Rider
- Git

---

## Building

```bash
# Clone
git clone https://github.com/hyi96/TaskApp.git
cd TaskApp

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project TaskApp

# Run tests
dotnet test
```

---

## Project Layout

| Project | Type | Purpose |
|---|---|---|
| `TaskApp` | WinExe (`net10.0` + `net10.0-windows10.0.19041.0`\*) | Main desktop application |
| `TaskApp.Tests` | Library (`net10.0-windows10.0.19041.0`) | xUnit test suite |

\* The main project multi-targets both TFMs. Windows builds include `Microsoft.Toolkit.Uwp.Notifications` for toast notifications; non-Windows builds compile without it. The `build-all.ps1` script selects the correct TFM per platform RID automatically.

See [Architecture](architecture.md) for the full directory structure.

---

## Coding Conventions

### General

- **Target:** `net10.0` and `net10.0-windows10.0.19041.0` (multi-target) with `<Nullable>enable</Nullable>`. The `WINDOWS_NOTIFICATIONS` symbol is defined for the Windows TFM; use `#if WINDOWS_NOTIFICATIONS` for any Windows-only code.
- **Pattern:** MVVM — domain logic in Models, presentation logic in ViewModels, UI in Views.
- **No comments** unless explaining complex logic. The code should be self-documenting.

### Naming

| Element | Convention | Example |
|---|---|---|
| Classes, methods, properties | PascalCase | `DailyTask`, `LoadDataAsync` |
| Private fields | `_camelCase` | `_currentStreak`, `_storageService` |
| Local variables, parameters | camelCase | `dailyTask`, `goldDelta` |
| Constants | PascalCase | `SortNameAsc`, `BackupExtension` |
| Async methods | `*Async` suffix | `SaveDataAsync`, `LoadTagsAsync` |

### Domain Model Rules

- Domain entities extend `DomainEntity` or `TaskBase`.
- Properties that control domain state use `internal set` — mutations go through explicit methods (e.g., `SetGoldReward(double)`).
- The `InternalsVisibleTo` attribute grants tests access to internal setters.
- Domain entities implement `INotifyPropertyChanged` via `OnPropertyChanged()`.
- All property setters include equality checks to avoid unnecessary change notifications.

### Service Rules

- Services handle I/O and persistence — they don't contain domain logic.
- `StorageService` and `UserService` use constructor injection (or constructor parameters for testability).
- `SettingsService` is a singleton accessed via `SettingsService.Instance`.
- All file writes must use the atomic `.tmp` → rename pattern with `.bak` rotation.

### ViewModel Rules

- ViewModels extend `ViewModelBase` (which provides `SetProperty` and `OnPropertyChanged`).
- `MainWindowViewModel` is the central orchestrator — it coordinates tasks, rewards, gold, filtering, and data persistence.
- Form ViewModels (`*FormViewModel`) are scoped to individual edit windows.
- Collections use `ObservableCollection<T>` for UI binding.

### Data Transfer Objects

- DTOs live in `TaskApp/Data/` and are plain classes with public getters/setters.
- DTOs are mapped to/from domain models via static mapper classes (`TaskMapper`, `RewardMapper`).
- DTOs must not contain domain logic.

---

## Adding a New Feature

### Adding a new task property

1. Add the property to the domain model (e.g., `DailyTask.cs`) with `internal set`.
2. Add a public setter method if external mutation is needed.
3. Add the property to the corresponding DTO (e.g., `DailyTaskData.cs`).
4. Update `TaskMapper.ToModel()` and `TaskMapper.ToData()` to map the new property.
5. Update the relevant form ViewModel to expose the property for editing.
6. Update the XAML view to bind to the new property.
7. If the property is stored in SQLite, update `EnsureColumnsExistAsync` in `StorageService`.
8. Write tests for the new property.
9. Run `dotnet test` to verify nothing is broken.

### Adding a new service

1. Create the service class in `TaskApp/Services/`.
2. If it requires lifecycle management, initialize it in `App.axaml.cs`.
3. If it needs testing, accept dependencies via constructor parameters (avoid static state).
4. Write tests in `TaskApp.Tests/`.

---

## Testing Guidelines

- All new features should have corresponding tests.
- Tests follow the `MethodName_Scenario_ExpectedResult` naming pattern.
- Use the Arrange-Act-Assert structure.
- Service tests that touch the filesystem should use temporary directories.
- See [Testing](testing.md) for detailed test categories and running instructions.

---

## Pull Request Checklist

- [ ] Code builds without warnings (`dotnet build`)
- [ ] All existing tests pass (`dotnet test`)
- [ ] New tests added for new functionality
- [ ] Domain model changes include mapper updates
- [ ] File writes use atomic write pattern
- [ ] No unnecessary comments added
- [ ] Property setters include equality checks
