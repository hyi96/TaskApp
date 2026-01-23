using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskApp.Models;

public class UserProfile : INotifyPropertyChanged
{
    private double _gold;

    public Guid Id { get; set; } = Guid.NewGuid();

    public double Gold
    {
        get => _gold;
        set
        {
            _gold = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
