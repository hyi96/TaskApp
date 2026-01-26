using System;
using System.Diagnostics;
using Avalonia.Threading;

namespace TaskApp.ViewModels;

public class CurrentActivityViewModel : ViewModelBase
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;
    private string _title = string.Empty;
    private bool _isRunning;
    private TimeSpan _sessionStartElapsed = TimeSpan.Zero;

    public CurrentActivityViewModel()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => UpdateElapsed();
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
        private set => SetProperty(ref _isRunning, value);
    }

    public event Action<TimeSpan, string>? ActivityDurationRecorded;

    public TimeSpan GetSessionStartElapsed() => _sessionStartElapsed;

    public void StopWithoutLogging()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();
    }

    public void Start()
    {
        if (IsRunning) return;

        _sessionStartElapsed = _stopwatch.Elapsed;
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
        _timer.Stop();
        UpdateElapsed();

        if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title);
        }
    }

    public void Reset()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        var sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        _stopwatch.Reset();
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();

        if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title);
        }
    }

    public void Remove()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        var sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        _stopwatch.Reset();
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();

        if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title);
        }

        Title = string.Empty;
    }

    public void SetTitleAndReset(string title)
    {
        if (IsRunning)
        {
            _stopwatch.Stop();
            var sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;

            if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
            {
                ActivityDurationRecorded?.Invoke(sessionElapsed, Title);
            }
        }

        Title = title ?? string.Empty;
        _stopwatch.Reset();
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();
    }

    public void LogAndStopIfRunning()
    {
        if (!IsRunning) return;

        _stopwatch.Stop();
        var sessionElapsed = _stopwatch.Elapsed - _sessionStartElapsed;
        IsRunning = false;
        _timer.Stop();

        if (!string.IsNullOrWhiteSpace(Title) && sessionElapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(sessionElapsed, Title);
        }
    }

    private void UpdateElapsed()
    {
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedDisplay));
    }
}
