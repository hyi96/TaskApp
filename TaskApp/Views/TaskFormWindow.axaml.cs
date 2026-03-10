using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskApp.Models.Tasks;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class TaskFormWindow : Window
{
    public TaskFormWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Dispose ViewModel to clean up event subscriptions
        if (DataContext is TaskFormViewModel vm)
        {
            vm.Dispose();
        }
        base.OnClosed(e);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskFormViewModel vm)
        {
            vm.SaveTask();
        }
        Close();
    }
    
    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskFormViewModel vm)
        {
            vm.DeleteTask();
        }
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ClearDueDate_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TodoFormViewModel vm)
        {
            vm.DueDate = null;
            vm.DueTime = null;
        }
    }

    private void SetCurrentActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskFormViewModel vm)
        {
            vm.SetAsCurrentActivity();
        }
        Close();
    }

    private void AddChecklistItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TodoFormViewModel vm)
        {
            vm.AddChecklistItem();
        }
    }

    private void AddChecklistItem_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TodoFormViewModel vm)
        {
            vm.AddChecklistItem();
        }
    }

    private void RemoveChecklistItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && 
            control.DataContext is ChecklistItem item &&
            DataContext is TodoFormViewModel vm)
        {
            vm.RemoveChecklistItem(item);
        }
    }

    private void AddStreakBonusRule_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DailyFormViewModel vm)
        {
            vm.AddStreakBonusRule();
        }
    }

    private void RemoveStreakBonusRule_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.DataContext is StreakBonusRuleViewModel rule && DataContext is DailyFormViewModel vm)
        {
            vm.RemoveStreakBonusRule(rule);
        }
    }

    private void LogManualDuration_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskFormViewModel vm)
        {
            vm.LogManualDuration();
        }
    }
}
