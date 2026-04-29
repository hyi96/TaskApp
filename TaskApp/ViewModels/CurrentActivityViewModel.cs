using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace TaskApp.ViewModels;

public class CurrentActivityViewModel : ViewModelBase
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;
    private string _title = string.Empty;
    private bool _isRunning;
    private TimeSpan _sessionStartElapsed = TimeSpan.Zero;
    private DateTimeOffset? _sessionStartedAt;
    private Guid? _taskId;
    private Guid? _rewardId;
    private bool _autocompleteTriggered;

    /// <summary>
    /// Delegate that returns the remaining time before autocomplete triggers.
    /// Parameters: taskId, session start timestamp, current session elapsed time.
    /// Returns null if autocomplete is not applicable, or remaining time (may be zero/negative when threshold crossed).
    /// </summary>
    public Func<Guid, DateTimeOffset, TimeSpan, Task<TimeSpan?>>? GetAutocompleteRemainingTime { get; set; }

    /// <summary>
    /// Raised when autocomplete threshold is crossed. Parameter is the task ID.
    /// </summary>
    public event Action<Guid>? AutocompleteTriggered;

    public CurrentActivityViewModel()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += async (_, _) =>
        {
            UpdateElapsed();
            await CheckAutocompleteAsync();
        };
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public string ElapsedDisplay => Elapsed.ToString(@"hh\:mm\:ss");

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(StartButtonLabel));
            }
        }
    }

    public string StartButtonLabel => Elapsed > TimeSpan.Zero && !IsRunning ? "Resume" : "Start";

    public Guid? TaskId => _taskId;
    public Guid? RewardId => _rewardId;

    public event Action<TimeSpan, string, Guid?, Guid?>? ActivityDurationRecorded;

    public TimeSpan GetSessionStartElapsed() => _sessionStartElapsed;

    public void StopWithoutLogging()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        IsRunning = false;
        _sessionStartedAt = null;
        _timer.Stop();
        UpdateElapsed();
    }

    public void Start()
    {
        if (IsRunning) return;

        _sessionStartElapsed = _stopwatch.Elapsed;
        _sessionStartedAt = DateTimeOffset.UtcNow;
        _stopwatch.Start();
        IsRunning = true;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        UpdateElapsed();
    }

    public void Pause()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        var sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        IsRunning = false;
        _sessionStartedAt = null;
        _timer.Stop();
        UpdateElapsed();

        if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title, _taskId, _rewardId);
        }
    }

    public void Reset()
    {
        StopAndLogSession();
    }

    public void Remove()
    {
        StopAndLogSession();

        Title = string.Empty;
        _taskId = null;
        _rewardId = null;
    }

    private void StopAndLogSession()
    {
        var wasRunning = IsRunning;
        var sessionElapsed = TimeSpan.Zero;

        if (wasRunning)
        {
            _stopwatch.Stop();
            sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        }

        _stopwatch.Reset();
        IsRunning = false;
        _sessionStartedAt = null;
        _timer.Stop();
        UpdateElapsed();

        if (wasRunning && !string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title, _taskId, _rewardId);
        }
    }

    public void SetTitleAndReset(string title, Guid? taskId = null, Guid? rewardId = null)
    {
        StopAndLogSession();

        Title = title ?? string.Empty;
        _taskId = taskId;
        _rewardId = rewardId;
        _autocompleteTriggered = false;
    }

    public void LogAndStopIfRunning()
    {
        if (!IsRunning) return;
        Pause();
    }

    private async Task CheckAutocompleteAsync()
    {
        if (_autocompleteTriggered || !IsRunning || _taskId is not Guid taskId || _sessionStartedAt is not DateTimeOffset sessionStartedAt || GetAutocompleteRemainingTime == null)
            return;

        var currentSessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        var remaining = await GetAutocompleteRemainingTime(taskId, sessionStartedAt, currentSessionElapsed);

        if (remaining.HasValue && remaining.Value <= TimeSpan.Zero)
        {
            _autocompleteTriggered = true;
            AutocompleteTriggered?.Invoke(taskId);
        }
    }

    private void UpdateElapsed()
    {
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedDisplay));
        OnPropertyChanged(nameof(StartButtonLabel));
    }
}
