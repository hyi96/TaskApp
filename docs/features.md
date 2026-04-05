# Features Guide

A detailed walkthrough of every major feature in TaskApp.

---

## Task Types

TaskApp supports three task types, each designed for a different productivity pattern.

### Habits

Habits are counter-based tasks for tracking behaviors you want to build or break.

- **Increment/Decrement** — Each habit has a configurable `IncrementAmount` (default 1.0). The increment and decrement buttons can be independently enabled or disabled.
- **Auto-Reset** — Habits can be configured to reset their counter automatically on a schedule:
  - `Never` — counter persists indefinitely.
  - `Daily` — resets at the start of each day.
  - `Weekly` — resets at the start of each week.
  - `Monthly` — resets at the start of each month.
- **Gold Reward** — Gold is earned each time the habit is incremented.

### Dailies

Dailies are recurring tasks that must be completed within each period to maintain a streak.

- **Repeat Cadence** — Choose from Daily, Weekly, Monthly, or Yearly, multiplied by a `RepeatEvery` interval (e.g., every 2 weeks).
- **Streaks** — Completing a daily within its period increments the current streak. Missing a period resets the streak to zero. The best streak is tracked separately.
- **Streak Bonuses** — Configurable bonus rules award extra gold at streak milestones. Default rules:
  - 7-day streak → +10% bonus gold
  - 14-day streak → +20% bonus gold
  - 30-day streak → +30% bonus gold
- **Autocomplete** — Dailies can set a time threshold (`AutocompleteTimeThreshold`). When the cumulative activity timer duration for that daily exceeds the threshold in the current period, the daily is automatically marked complete and a notification is shown.
- **Period Awareness** — Dailies know their current period start and end dates. The UI shows whether the daily is due or already completed for the current period.

### Todos

Todos are one-off tasks for things that need to get done once.

- **Due Date** — Optional deadline for scheduling.
- **Checklist** — Each todo can have a list of sub-items (`ChecklistItem`) that can be independently checked off.
- **Completion** — Marking a todo as complete earns gold and records the completion timestamp.
- **Filters** — Todos can be filtered by: Active (not completed), Scheduled (has a due date, not completed), Completed, or Hidden.

---

## Rewards

Rewards are incentives that cost gold to claim.

- **Gold Cost** — Each reward has a configurable gold cost. You can only claim a reward if you have enough gold.
- **One-Time vs. Repeatable** — One-time rewards can only be claimed once. Repeatable rewards can be claimed as many times as you can afford.
- **Claim Count** — The total number of claims is tracked for repeatable rewards.
- **Filters** — Rewards can be filtered by: All, One-Time, Repeatable, or Hidden.

---

## Gold Economy

Gold is the central currency connecting tasks and rewards.

| Action | Gold Effect |
|---|---|
| Complete a daily | + `GoldReward` × (1 + streak bonus %) |
| Increment a habit | + `GoldReward` |
| Complete a todo | + `GoldReward` |
| Claim a reward | − `GoldCost` |
| Undo a completion | Reverses the original gold change |

- Gold cannot go below zero (clamped with `Math.Max(0, ...)`).
- The gold balance is displayed in the main window and persisted in the user profile.

---

## Activity Timer

The built-in stopwatch lets you time work sessions on any task or reward.

### Controls

| Action | Behavior |
|---|---|
| **Set** | Assign a title and optionally link to a task or reward |
| **Start** | Begin timing (or resume after pause) |
| **Pause** | Pause the stopwatch without logging |
| **Stop** | Stop and log the session duration |
| **Reset** | Clear the timer and title |

### Duration Logging

When an activity session ends (stop, window close, or shutdown):

1. The elapsed session time is recorded as a `LogEntry` with `LogType.ActivityDuration`.
2. The entry captures the title, linked task/reward IDs, and duration.
3. If the linked task is a daily with an autocomplete threshold, the system checks whether cumulative time has crossed the threshold.

### Autocomplete

For dailies with `AutocompleteTimeThreshold` set:

1. While the timer is running, the system periodically queries the total logged duration for the linked task in the current period.
2. When the threshold is crossed, `AutocompleteTriggered` fires.
3. The daily is marked complete, gold is awarded, and a Windows toast notification is shown.

---

## Graphs & Analytics

The Graph window provides visual analytics using ScottPlot charts.

### Dimensions

- **Time Resolution** — View data by Day, Week, or Month.
- **Target Type** — Filter by task type (Habits, Dailies, Todos), Rewards, or All.
- **Target Value** — Choose what to plot (completions, gold earned/spent, activity duration, counts, streaks).
- **Target Instance** — Narrow to a specific task or reward, or view aggregates.

### Search & Merge

- **Search** — Find specific tasks or rewards by name within the graph window.
- **Merge** — Combine activity logs from one entity into another (useful when a task is renamed or split).

---

## Tags

Tags are reusable labels for organizing tasks and rewards.

### Management

- Tags are managed through the Tags window.
- Create, rename, or delete tags.
- Deleting a tag removes it from all tasks and rewards.

### Filtering

- The main window shows all available tags as toggleable filters.
- When one or more tags are selected, only items with at least one matching tag are shown.
- Tag filtering combines with text search and column-specific filters.

---

## Filtering & Sorting

### Text Search

A global search bar filters items by title (case-insensitive substring match) across all columns.

### Column Filters

Each column has its own filter tabs:

| Column | Filters |
|---|---|
| Habits | All, Hidden |
| Dailies | All, Due, Not Due, Hidden |
| Todos | Active, Scheduled, Completed, Hidden |
| Rewards | All, One-Time, Repeatable, Hidden |

### Sort Options

Each column can be sorted independently. Available sort modes:

| Sort Mode | Available For |
|---|---|
| Name (A-Z / Z-A) | All columns |
| Created time (new/old) | All columns |
| Gold value (high/low) | All columns |
| Count (high/low) | Habits |
| Current streak (high/low) | Dailies |
| Best streak (high/low) | Dailies |
| Due date (earliest/latest) | Dailies, Todos |

Sort preferences are persisted per user and restored on next launch.

### Hidden Items

Any task or reward can be hidden. Hidden items don't appear in normal views — use the "Hidden" filter tab to see and manage them.

---

## Multi-User Profiles

TaskApp supports multiple user profiles, each with completely separate data.

### Operations

| Operation | Description |
|---|---|
| **Create** | Create a new user with a name |
| **Switch** | Switch to a different user (triggers full data reload) |
| **Rename** | Change a user's display name |
| **Delete** | Delete a user and all their data (last user cannot be deleted) |
| **Export** | Save a user's data as a `.taskapp` ZIP archive |
| **Import** | Load a `.taskapp` archive as a new user |

### Data Isolation

Each user has their own directory under `Users/{userId}/` containing separate `tasks.json`, `rewards.json`, `tags.json`, `user.json`, and `logs.db` files.

---

## Undo System

Any logged action can be undone from the Logs window.

### Undoable Actions

| Action | Undo Behavior |
|---|---|
| Habit increment | Reverts counter by increment amount, restores `LastCompletedDate` |
| Daily completion | Unmarks completion, decrements streak, restores `LastCompletionPeriod` |
| Todo completion | Clears `LastCompletedDate` |
| Reward claim | Decrements claim count, restores `IsClaimed` for one-time rewards |
| Activity duration | Deletes the log entry (no state to reverse) |

All undos also reverse the associated gold change (clamped to ≥ 0) and delete the log entry.

---

## New Day Detection

TaskApp detects when the calendar day changes and handles the transition.

### Flow

1. `DayDetectionService` polls every 60 seconds. When `DateTime.Now.Date` advances, it fires `NewDayDetected`.
2. The app collects all dailies that were **not completed** in their most recent period.
3. If any exist, a **New Day Window** is shown listing the missed dailies for review. Users can check off dailies they actually completed — these are retroactively marked as completed for the previous period and gold is awarded.
4. All tasks are refreshed:
   - Dailies check their period and update completion status.
   - Habits check their reset cadence and reset counters if due.
   - Streaks are evaluated — missed periods reset the current streak to zero.
5. All changes are saved.

### Startup Check

The same logic runs at startup if `LastActiveDate` is before today, ensuring the new-day flow fires even if the app was closed overnight.

---

## Vacation Mode

Vacation mode protects all daily streaks during absences.

- **Toggle** — Enabled and disabled from the main window via a toggle button.
- **Effect** — When enabled, all daily streaks are automatically protected on new-day transitions at no gold cost. The new-day review window is not shown.
- **Per-User** — The setting is stored in `UserProfile.IsVacationMode` and persisted with the user profile.
- **Tooltip** — The toggle button displays a tooltip indicating the current state and what clicking will do.

---

## Theming

TaskApp supports three theme modes via the Settings window:

| Mode | Behavior |
|---|---|
| Light | Always uses the light Fluent theme |
| Dark | Always uses the dark Fluent theme |
| System | Follows the operating system's theme preference |

The theme setting is app-wide (not per-user) and takes effect immediately.

---

## Notifications

TaskApp uses Windows toast notifications for:

- **Daily Autocomplete** — When a daily's activity timer crosses the autocomplete threshold, a toast notification confirms the completion and shows the gold earned.

Notifications are best-effort and **Windows-only** — they use the `Microsoft.Toolkit.Uwp.Notifications` library, conditionally compiled via the `WINDOWS_NOTIFICATIONS` preprocessor symbol (defined only for the `net10.0-windows10.0.19041.0` TFM). At runtime, a `OperatingSystem.IsWindows()` guard provides an additional safety check. On non-Windows builds the notification code is compiled out entirely; all other functionality works normally.
