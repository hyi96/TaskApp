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
}
