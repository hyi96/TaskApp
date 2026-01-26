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

    private void UncheckAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewDayViewModel vm)
        {
            vm.UncheckAll();
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
