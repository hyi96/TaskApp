using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using ScottPlot.Avalonia;
using TaskApp.Services;
using TaskApp.Views;

namespace TaskApp.Tests;

public class ThemeTests
{
    [Theory]
    [InlineData(0, ThemeMode.Light)]
    [InlineData(1, ThemeMode.Dark)]
    [InlineData(2, ThemeMode.System)]
    [InlineData(3, ThemeMode.Forest)]
    public void SavedThemeValuesKeepTheirMeaning(int savedValue, ThemeMode expected)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>($"{{\"ThemeMode\":{savedValue}}}");
        Assert.NotNull(settings);
        Assert.Equal(expected, settings.ThemeMode);
        Assert.Equal(expected, JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!.ThemeMode);
    }

    [AvaloniaFact]
    public void ProductionAppLoadsForestColorsInsteadOfTheDarkFallback()
    {
        var app = new App();
        app.Initialize();
        var forest = App.GetThemeVariant(ThemeMode.Forest);
        Assert.Equal(ThemeVariant.Dark, forest.InheritVariant);

        Assert.True(app.TryGetResource("AppBackgroundBrush", forest, out var forestBackground));
        Assert.Equal(Color.Parse("#101914"), Assert.IsAssignableFrom<ISolidColorBrush>(forestBackground).Color);
        Assert.True(app.TryGetResource("AppBackgroundBrush", ThemeVariant.Dark, out var darkBackground));
        Assert.Equal(Color.Parse("#1E1E1E"), Assert.IsAssignableFrom<ISolidColorBrush>(darkBackground).Color);
    }

    [AvaloniaFact]
    public void ForestThemeUpdatesTheSettingsWindowAndItsControls()
    {
        var window = new SettingsWindow();
        try
        {
            var picker = window.GetLogicalDescendants().OfType<ComboBox>().First();
            window.Show();
            window.RequestedThemeVariant = App.GetThemeVariant(ThemeMode.Forest);
            window.UpdateLayout();

            Assert.Equal(Color.Parse("#101914"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
            Assert.Equal(Color.Parse("#1B2B23"), Assert.IsAssignableFrom<ISolidColorBrush>(picker.Background).Color);

            window.RequestedThemeVariant = ThemeVariant.Light;
            window.UpdateLayout();
            Assert.Equal(ThemeVariant.Light, picker.ActualThemeVariant);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ForestThemeRecolorsTheChartAndItsExistingSeries()
    {
        var window = new GraphWindow();
        try
        {
            var plot = window.FindControl<AvaPlot>("GraphPlot")!.Plot;
            var line = plot.Add.Scatter(new double[] { 0, 1 }, new double[] { 0, 1 });
            window.RequestedThemeVariant = App.GetThemeVariant(ThemeMode.Forest);

            Assert.Equal(new ScottPlot.Color(16, 25, 20), plot.FigureBackground.Color);
            Assert.Equal(new ScottPlot.Color(23, 36, 29), plot.DataBackground.Color);
            Assert.Equal(new ScottPlot.Color(121, 214, 177), line.Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ForestWindowFrameColorsReturnToWindowsDefaultsWhenSwitchingThemes()
    {
        var window = new Window();
        try
        {
            window.RequestedThemeVariant = App.ForestTheme;
            Assert.Equal((0x002B3522u, 0x00F0F5F0u, 0x003D4833u), WindowsWindowFrame.GetColors(window));

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal((uint.MaxValue, uint.MaxValue, uint.MaxValue), WindowsWindowFrame.GetColors(window));
            window.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal((uint.MaxValue, uint.MaxValue, uint.MaxValue), WindowsWindowFrame.GetColors(window));
        }
        finally
        {
            window.Close();
        }
    }
}
