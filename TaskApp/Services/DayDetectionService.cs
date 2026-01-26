using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskApp.Models.Tasks;

namespace TaskApp.Services;

public class DayDetectionService
{
    private Timer? _dayCheckTimer;
    private DateTime _lastCheckedDate;
    private bool _disposed;

    public event Func<List<DailyTask>, Task>? NewDayDetected;

    public DayDetectionService()
    {
        _lastCheckedDate = DateTime.Now.Date;
    }

    public void Start()
    {
        if (_dayCheckTimer != null)
            return;

        _dayCheckTimer = new Timer(CheckForNewDay, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _dayCheckTimer?.Dispose();
        _dayCheckTimer = null;
    }

    private async void CheckForNewDay(object? state)
    {
        try
        {
            var currentDate = DateTime.Now.Date;
            if (currentDate > _lastCheckedDate)
            {
                _lastCheckedDate = currentDate;
                await OnNewDayDetected();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the timer
            System.Diagnostics.Debug.WriteLine($"Error in CheckForNewDay: {ex.Message}");
        }
    }

    private async Task OnNewDayDetected()
    {
        if (NewDayDetected != null)
        {
            // Get unchecked dailies from "yesterday" (the last period)
            await NewDayDetected(new List<DailyTask>());
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
