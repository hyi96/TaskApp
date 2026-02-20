using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class LogsWindow : Window
{
    public LogsWindow()
    {
        InitializeComponent();
    }

    private async void ApplyFilter_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
