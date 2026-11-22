using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace TaskApp.Tests;

public class ScrollbarStyleTests
{
    [AvaloniaFact]
    public void SharedStyleReservesATenPixelGutterForEachOverflowingAxis()
    {
        var content = new Border { Width = 600, Height = 450 };
        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var window = new Window { Width = 320, Height = 240, Content = scroll };
        try
        {
            window.Show();
            window.UpdateLayout();

            var visuals = scroll.GetVisualDescendants().ToArray();
            var vertical = Assert.Single(visuals.OfType<ScrollBar>(), bar => bar.Name == "PART_VerticalScrollBar");
            var horizontal = Assert.Single(visuals.OfType<ScrollBar>(), bar => bar.Name == "PART_HorizontalScrollBar");
            var presenter = Assert.Single(visuals.OfType<ScrollContentPresenter>());
            Assert.True(vertical.IsVisible);
            Assert.True(horizontal.IsVisible);
            Assert.Equal(10, vertical.Bounds.Width);
            Assert.Equal(10, horizontal.Bounds.Height);
            Assert.False(scroll.AllowAutoHide);
            Assert.False(vertical.AllowAutoHide);
            Assert.False(horizontal.AllowAutoHide);

            var contentBottomRight = presenter.TranslatePoint(new Point(presenter.Bounds.Width, presenter.Bounds.Height), window)!.Value;
            var verticalTopLeft = vertical.TranslatePoint(default, window)!.Value;
            var horizontalTopLeft = horizontal.TranslatePoint(default, window)!.Value;
            Assert.True(contentBottomRight.X <= verticalTopLeft.X);
            Assert.True(contentBottomRight.Y <= horizontalTopLeft.Y);

            foreach (var bar in new[] { vertical, horizontal })
            {
                var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
                Assert.Equal(new CornerRadius(5), thumb.CornerRadius);
                var arrows = bar.GetVisualDescendants().OfType<RepeatButton>()
                    .Where(button => button.Name is "PART_LineUpButton" or "PART_LineDownButton").ToArray();
                Assert.Equal(2, arrows.Length);
                Assert.All(arrows, arrow => Assert.False(arrow.IsVisible));
            }

            content.Width = content.Height = 80;
            window.UpdateLayout();
            Assert.False(vertical.IsVisible);
            Assert.False(horizontal.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EmbeddedListAndTextScrollViewersUseTheSharedStyle()
    {
        var notes = new TextBox
        {
            AcceptsReturn = true,
            Height = 70,
            Text = string.Join(Environment.NewLine, Enumerable.Repeat("Task notes", 20))
        };
        var list = new ListBox { Height = 100, ItemsSource = Enumerable.Range(1, 25).ToArray() };
        var panel = new StackPanel { Children = { notes, list } };
        var window = new Window { Width = 320, Height = 240, Content = panel };
        try
        {
            window.Show();
            window.UpdateLayout();

            foreach (var control in panel.Children)
            {
                var scroll = Assert.Single(control.GetVisualDescendants().OfType<ScrollViewer>(),
                    viewer => viewer.TemplatedParent == control);
                var bar = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(),
                    item => item.Name == "PART_VerticalScrollBar" && item.TemplatedParent == scroll);
                Assert.False(scroll.AllowAutoHide);
                Assert.True(bar.IsVisible);
                Assert.Equal(10, bar.Bounds.Width);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
