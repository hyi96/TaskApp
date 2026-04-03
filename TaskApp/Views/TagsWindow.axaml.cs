using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class TagsWindow : Window
{
    private readonly Dictionary<TextBox, string> _originalTagNames = new();

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

    private async void RemoveTag_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectableTag tag && DataContext is TagsViewModel vm)
        {
            await vm.RemoveTagAsync(tag);
        }
    }

    private void TagName_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not SelectableTag tag)
            return;

        if (e.Key == Key.Enter)
        {
            // Store original name if not already stored
            _originalTagNames.TryAdd(textBox, tag.Name);

            var newName = textBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newName))
            {
                // Revert to original name
                textBox.Text = _originalTagNames[textBox];
            }
            else if (DataContext is TagsViewModel vm)
            {
                // Update tag name
                vm.UpdateTagName(tag, newName);
                _originalTagNames[textBox] = tag.Name; // Update stored original
            }

            // Move focus away from textbox
            this.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Revert to original name on Escape
            if (_originalTagNames.TryGetValue(textBox, out var originalName))
            {
                textBox.Text = originalName;
            }
            this.Focus();
            e.Handled = true;
        }
    }

    private void TagName_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not SelectableTag tag)
            return;

        // Store original name on first focus
        _originalTagNames.TryAdd(textBox, tag.Name);

        // On lost focus, revert if text is empty, otherwise keep current text without updating
        var currentText = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentText))
        {
            textBox.Text = _originalTagNames[textBox];
        }
        else
        {
            textBox.Text = tag.Name; // Ensure it displays the actual tag name
        }
    }
}





