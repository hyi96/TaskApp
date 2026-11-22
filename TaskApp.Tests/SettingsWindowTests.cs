using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TaskApp.Views;

namespace TaskApp.Tests;

public class SettingsWindowTests
{
    [AvaloniaFact]
    public void SettingsToolbarIconKeepsAnAccessibleName()
    {
        var window = new MainWindow();
        try
        {
            var button = window.FindControl<Button>("SettingsButton");
            Assert.NotNull(button);
            Assert.IsType<Viewbox>(button.Content);
            Assert.Equal("Settings", AutomationProperties.GetName(button));
            Assert.Equal("Settings", ToolTip.GetTip(button));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CloudSyncHasItsOwnTabWithProfileActions()
    {
        var window = new SettingsWindow();
        try
        {
            var tabs = window.FindControl<TabControl>("SettingsTabs");
            Assert.NotNull(tabs);
            var cloud = Assert.Single(tabs.Items.OfType<TabItem>(), tab => Equals(tab.Header, "Cloud Sync"));
            var content = Assert.IsType<ScrollViewer>(cloud.Content);
            var actions = content.GetLogicalDescendants().OfType<Button>().Select(button => button.Content).ToArray();

            Assert.Contains("Login", actions);
            Assert.Contains("Upload Profile", actions);
            Assert.Contains("Upload All Profiles", actions);
            Assert.Contains("Download Profile", actions);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SettingsScrollbarUsesAFixedGutterInBothThemes()
    {
        var window = new SettingsWindow { Height = 340 };
        try
        {
            window.Show();
            var tabs = window.FindControl<TabControl>("SettingsTabs")!;

            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark, App.ForestTheme })
            {
                window.RequestedThemeVariant = theme;
                foreach (var tab in tabs.Items.OfType<TabItem>())
                {
                    tabs.SelectedItem = tab;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();

                    var scroll = Assert.IsType<ScrollViewer>(tab.Content);
                    var visuals = scroll.GetVisualDescendants().ToArray();
                    var bar = Assert.Single(visuals.OfType<ScrollBar>(),
                        item => item.Name == "PART_VerticalScrollBar" && item.TemplatedParent == scroll);
                    var presenter = Assert.Single(visuals.OfType<ScrollContentPresenter>(),
                        item => item.Name == "PART_ContentPresenter" && item.TemplatedParent == scroll);
                    var contentRight = presenter.TranslatePoint(new Point(presenter.Bounds.Width, 0), window);
                    var barLeft = bar.TranslatePoint(new Point(0, 0), window);

                    Assert.True(bar.IsVisible);
                    Assert.Equal(10, bar.Bounds.Width);
                    Assert.False(bar.AllowAutoHide);
                    Assert.True(bar.IsExpanded);
                    Assert.NotNull(contentRight);
                    Assert.NotNull(barLeft);
                    Assert.True(contentRight.Value.X <= barLeft.Value.X);

                    var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
                    Assert.Equal(new CornerRadius(5), thumb.CornerRadius);
                    scroll.ScrollToEnd();
                    Assert.True(scroll.Offset.Y > 0);
                    Assert.Equal(10, bar.Bounds.Width);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }
}
