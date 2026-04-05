using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class NewDayWindow : Window
{
    public NewDayWindow()
    {
        InitializeComponent();
    }

    private void CheckAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewDayViewModel vm)
        {
            vm.CheckAll();
        }
    }

    private void ProtectAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewDayViewModel vm)
        {
            vm.ProtectAll();
        }
    }

    private void UncheckAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewDayViewModel vm)
        {
            vm.UncheckAll();
        }
    }

    private void StartNewDay_Click(object? sender, RoutedEventArgs e)
    {
        // Close the window, which will trigger the save logic in App.axaml.cs
        Close();
    }
}

