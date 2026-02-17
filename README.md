# TaskApp

A gamified task management desktop application built with **Avalonia UI** and **.NET 8**. Track habits, dailies, and todos while earning gold rewards to stay motivated.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)

## Features

### 📋 Task Management
- **Habits** – Repeatable actions with increment counters and optional counter reset cadences (daily, weekly, monthly) with automatic resets
- **Dailies** – Recurring tasks with streak tracking, customizable schedules (daily, weekly, monthly, yearly), and optional time-based autocomplete
- **Todos** – One-time tasks with optional due dates and checklists
- **Hide/Unhide** – Archive and restore tasks or rewards without deleting them
- **Context Menus** – Right-click tasks and rewards for quick actions

### 🏆 Gamification
- **Gold System** – Earn gold by completing tasks, spend it on custom rewards
- **Streak Bonuses** – Earn bonus gold for maintaining daily streaks (7, 14, 30+ days)
- **Rewards Shop** – Create custom rewards to purchase with earned gold (one-time or repeatable)

### ⏱️ Current Activity Timer
- **Time Tracking** – Set any task as your current activity and track time spent on it
- **Daily Autocomplete** – Dailies can auto-complete when a configurable time threshold is reached
- **Overdue Highlighting** – Overdue dailies are visually highlighted with red titles

### 📊 Analytics & Insights
- **Graphs** – Visualize your productivity over time (hourly, daily, weekly, monthly, yearly views)
- **Activity Logs** – Track all task completions and reward claims with timestamps and last-completed info
- **SQLite Database** – Persistent local storage for logs and analytics

### 🎨 Customization
- **Light/Dark Themes** – Switch between visual themes
- **Multiple Users** – Support for multiple user profiles with separate data
- **Tags** – Organize tasks with custom tags
- **Sorting & Filtering** – Multiple sort options and filter tabs for each task type

### 💾 Data Management
- **Export/Import** – Backup and restore your data
- **Per-User Data Storage** – Each user has isolated task and reward data

## Screenshots

### Main Window
![Main Window](TaskApp/Assets/screenshots/main_window.png)

### Main Window (Verbose Mode)
![Main Window Verbose](TaskApp/Assets/screenshots/main_window_verbose.png)

### Dark Mode
![Dark Mode](TaskApp/Assets/screenshots/dark_mode.png)

### Edit Task Form
![Edit Task Form](TaskApp/Assets/screenshots/edit_task_form.png)

### Graphs & Analytics
![Graphs](TaskApp/Assets/screenshots/graph.png)

### Settings
![Settings](TaskApp/Assets/screenshots/settings_window.png)

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build From Source & Run

```bash
# Clone the repository
git clone https://github.com/hyi96/TaskApp.git
cd TaskApp

# Build the project
dotnet build

# Run the application
dotnet run --project TaskApp
```

## Project Structure

```
TaskApp/
├── Models/
│   ├── Tasks/          # HabitTask, DailyTask, TodoTask, ChecklistItem, StreakBonusRule
│   ├── Rewards/        # Reward model
│   ├── Logs/           # LogEntry for activity tracking
│   ├── Tags/           # Tag model
│   ├── DomainEntity.cs # Base class for tasks and rewards
│   └── User.cs         # User and UserProfile models
├── ViewModels/         # MVVM ViewModels (includes CurrentActivityViewModel)
├── Views/              # Avalonia XAML views
├── Services/           # StorageService, UserService, SettingsService, DayDetectionService
├── Converters/         # XAML value converters
└── Data/               # Data transfer objects for serialization
TaskApp.Tests/          # xUnit test project (daily autocomplete tests, etc.)
```

## Technology Stack

| Component | Technology |
|-----------|------------|
| UI Framework | [Avalonia UI 11.3](https://avaloniaui.net/) |
| Runtime | .NET 8 |
| Database | SQLite (Microsoft.Data.Sqlite) |
| Charts | [ScottPlot 5](https://scottplot.net/) |
| Architecture | MVVM |
| Testing | xUnit (.NET 10) |

## Usage Tips

- **Quick Add**: Type a task name and press `Enter` to quickly add it
- **Edit Tasks**: Click on any task to open the edit form with full options
- **Context Menus**: Right-click tasks or rewards for quick actions (set as current activity, hide/unhide, etc.)
- **Current Activity**: Set a task as your current activity to track time spent on it
- **Search**: Use the search bar to filter tasks across all categories
- **Verbose Mode**: Toggle "Verbose" checkbox to see detailed information including last-completed times
- **New Day Detection**: The app automatically detects day changes, resets daily tasks, and resets habit counters based on cadence
- **Hide/Unhide**: Archive tasks or rewards you don't need right now and restore them later

## Inspirations

This project was inspired by [Habitica](https://habitica.com/), a gamified task management platform that transforms productivity into an RPG adventure.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.
