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

    public void Start()
    {
        if (IsRunning) return;

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
        var elapsed = _stopwatch.Elapsed;
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();

        if (elapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(elapsed, Title);
        }
    }

    public void Reset()
    {
        var wasRunning = IsRunning;
        var elapsed = _stopwatch.Elapsed;

        _stopwatch.Reset();
        IsRunning = false;
        _timer.Stop();
        UpdateElapsed();

        if (wasRunning && elapsed > TimeSpan.Zero)
        {
            ActivityDurationRecorded?.Invoke(elapsed, Title);
        }
    }

    public void SetTitleAndReset(string title)
    {
        Title = title;
        Reset();
    }

    private void UpdateElapsed()
    {
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedDisplay));
    }
}
