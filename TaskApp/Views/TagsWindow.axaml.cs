using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class TagsWindow : Window
{
    public TagsWindow()
    {
        InitializeComponent();
    }

    private void AddTag_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TagsViewModel vm)
        {
            vm.AddTag();
        }
    }

    private void RemoveTag_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectableTag tag && DataContext is TagsViewModel vm)
        {
            vm.RemoveTag(tag);
        }
    }
}


