# Testing

The `TaskApp.Tests` project contains unit and integration tests using xUnit. It references the main `TaskApp` project with `InternalsVisibleTo`, allowing tests to access `internal` members.

---

## Test Project Setup

| Setting | Value |
|---|---|
| Framework | .NET 10 (`net10.0-windows10.0.19041.0`\*) |
| Test Framework | xUnit 2.9.3 |
| Runner | xunit.runner.visualstudio 3.1.4 |
| UI Testing | Avalonia.Headless.XUnit 11.3.9 |
| Coverage | Coverlet 6.0.4 |

\* The test project targets the Windows-specific TFM to reference the full Windows build of the main project (including notification code). The test code itself is platform-agnostic.

### Project Reference

```xml
<ProjectReference Include="..\TaskApp\TaskApp.csproj" />
```

The main project grants test access to internals:

```xml
<InternalsVisibleTo Include="TaskApp.Tests" />
```

---

## Test Categories

### Domain Model Tests

| Test Class | Covers |
|---|---|
| `DailyTaskTests` | Daily task completion, streaks, period calculations |
| `TodoTaskTests` | Todo completion, due dates |
| `RewardTests` | Reward claiming, gold cost validation, repeatability |
| `StreakBonusRuleTests` | Streak bonus rule configuration and gold calculation |
| `HabitCounterResetTests` | Habit counter auto-reset logic |

### ViewModel Tests

| Test Class | Covers |
|---|---|
| `MainWindowViewModelTests` | Core ViewModel operations (add, delete, filter, sort, gold) |
| `CurrentActivityViewModelTests` | Activity timer start, pause, stop, reset, duration logging |
| `LogEntryViewModelTests` | Log display and formatting |
| `UndoLogEntryTests` | Undo operations for all log types |

### Service Tests

| Test Class | Covers |
|---|---|
| `UserServiceTests` | User CRUD, switching, data directory management |
| `TaskMapperRoundTripTests` | TaskMapper serialization/deserialization fidelity |
| `RewardMapperRoundTripTests` | RewardMapper serialization/deserialization fidelity |
| `ImportExportTests` | User data export and import (ZIP archive) |
| `BackupRecoveryTests` | Backup file fallback on corruption |

### Feature Tests

| Test Class | Covers |
|---|---|
| `NewDayCompletionTests` | New day detection and daily status transitions |
| `DailyAutocompleteTests` | Activity timer autocomplete threshold logic |
| `ManualDurationLoggingTests` | Manual activity duration logging |
| `MergeActivityTests` | Activity log merging between tasks |

---

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run all tests with detailed output
dotnet test --verbosity normal

# Run tests in a specific class
dotnet test --filter "FullyQualifiedName~DailyTaskTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~DailyTaskTests.Complete_IncreasesStreak"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio

1. Open the solution in Visual Studio.
2. Open **Test Explorer** (Test → Test Explorer).
3. Click **Run All** or select specific tests to run.
4. Use the filter bar to find tests by name, class, or outcome.

---

## Test Conventions

### Naming

Tests follow the pattern: `MethodName_Scenario_ExpectedResult`

Examples:
- `Complete_IncreasesStreak`
- `TryClaim_InsufficientGold_ReturnsFalse`
- `UndoLogEntry_DailyCompleted_DecrementsStreak`

### Arrangement

Tests use the **Arrange-Act-Assert** pattern:

```csharp
[Fact]
public void Complete_IncreasesStreak()
{
    // Arrange
    var daily = new DailyTask();

    // Act
    daily.Complete();

    // Assert
    Assert.Equal(1, daily.CurrentStreak);
}
```

### Test Isolation

- Each test creates its own instances — no shared mutable state.
- Service tests that touch the filesystem use temporary directories.
- `UserService` accepts a custom `appDataFolder` path for test isolation.

### Avalonia Headless

Tests that require Avalonia UI components use `Avalonia.Headless.XUnit` which provides a headless Avalonia application context. The `AvaloniaTestApp` class in the test project configures this.
