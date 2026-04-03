using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.Models;

public class UserProfile : INotifyPropertyChanged
{
    private double _gold;
    private string _habitsSortMode = "Name (A-Z)";
    private string _dailiesSortMode = "Name (A-Z)";
    private string _todosSortMode = "Name (A-Z)";
    private string _rewardsSortMode = "Name (A-Z)";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The local date when data was last saved for this profile.
    /// Used to determine if the new day window should show on user switch.
    /// </summary>
    public DateOnly? LastActiveDate { get; set; }

    public double Gold
    {
        get => _gold;
        set
        {
            if (_gold != value)
            {
                _gold = value;
                OnPropertyChanged();
            }
        }
    }

    public string HabitsSortMode
    {
        get => _habitsSortMode;
        set
        {
            if (_habitsSortMode != value)
            {
                _habitsSortMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string DailiesSortMode
    {
        get => _dailiesSortMode;
        set
        {
            if (_dailiesSortMode != value)
            {
                _dailiesSortMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string TodosSortMode
    {
        get => _todosSortMode;
        set
        {
            if (_todosSortMode != value)
            {
                _todosSortMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string RewardsSortMode
    {
        get => _rewardsSortMode;
        set
        {
            if (_rewardsSortMode != value)
            {
                _rewardsSortMode = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
