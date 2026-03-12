using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using ScottPlot;
using ScottPlot.Avalonia;
using TaskApp.Services;
using TaskApp.ViewModels;

namespace TaskApp.Views;

public partial class GraphWindow : Window
{
    private GraphViewModel? _viewModel;
    private AvaPlot? _graphPlot;

    public GraphWindow()
    {
        InitializeComponent();
        _graphPlot = this.FindControl<AvaPlot>("GraphPlot");
        DataContextChanged += OnDataContextChanged;
        
        // Apply initial theme
        ApplyPlotTheme();
        
        // Listen for theme changes
        SettingsService.Instance.ThemeChanged += OnThemeChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from theme changes
        SettingsService.Instance.ThemeChanged -= OnThemeChanged;
        
        // Unsubscribe from ViewModel events and dispose
        if (_viewModel != null)
        {
            _viewModel.PlotDataUpdated -= OnPlotDataUpdated;
            _viewModel.Dispose();
            _viewModel = null;
        }

        // Clear plot resources
        if (_graphPlot != null)
        {
            _graphPlot.Plot.Clear();
            _graphPlot.Refresh(); // Force redraw to release rendered resources
            _graphPlot = null;
        }
        
        // Unsubscribe from DataContext changes
        DataContextChanged -= OnDataContextChanged;
        
        // Clear DataContext reference
        DataContext = null;

        base.OnClosed(e);
        
        // Suggest GC collection (helps release Skia resources faster)
        GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    private void OnThemeChanged(ThemeMode mode)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ApplyPlotTheme();
            _graphPlot?.Refresh();
        });
    }

    private void ApplyPlotTheme()
    {
        if (_graphPlot == null) return;

        var plot = _graphPlot.Plot;
        var isDark = IsDarkTheme();

        var bgColor = isDark ? new ScottPlot.Color(30, 30, 30) : new ScottPlot.Color(255, 255, 255);
        var dataBgColor = isDark ? new ScottPlot.Color(45, 45, 45) : new ScottPlot.Color(250, 250, 250);
        var fgColor = isDark ? new ScottPlot.Color(220, 220, 220) : new ScottPlot.Color(30, 30, 30);
        var gridColor = isDark ? new ScottPlot.Color(60, 60, 60) : new ScottPlot.Color(200, 200, 200);

        plot.FigureBackground.Color = bgColor;
        plot.DataBackground.Color = dataBgColor;

        var axes = new ScottPlot.IAxis[] { plot.Axes.Bottom, plot.Axes.Left };
        foreach (var axis in axes)
        {
            axis.Label.ForeColor = fgColor;
            axis.TickLabelStyle.ForeColor = fgColor;
            axis.MajorTickStyle.Color = gridColor;
            axis.MinorTickStyle.Color = gridColor;
            axis.FrameLineStyle.Color = gridColor;
        }

        plot.Axes.Top.FrameLineStyle.Color = gridColor;
        plot.Axes.Right.FrameLineStyle.Color = gridColor;
        plot.Grid.MajorLineColor = gridColor;
    }

    private bool IsDarkTheme() => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PlotDataUpdated -= OnPlotDataUpdated;
        }

        _viewModel = DataContext as GraphViewModel;

        if (_viewModel != null)
        {
            _viewModel.PlotDataUpdated += OnPlotDataUpdated;
        }
    }

    private void OnPlotDataUpdated(PlotData data)
    {
        if (_graphPlot == null)
        {
            return;
        }

        var plot = _graphPlot.Plot;
        plot.Clear();
        
        // Re-apply theme after clearing (clear resets some styles)
        ApplyPlotTheme();

        var xs = Enumerable.Range(0, data.Values.Length).Select(i => (double)i).ToArray();
        var scatter = plot.Add.Scatter(xs, data.Values);
        scatter.MarkerSize = 6;
        scatter.LineWidth = 2;
        
        // Use theme-aware line color
        var isDark = IsDarkTheme();
        scatter.Color = isDark 
            ? new ScottPlot.Color(100, 180, 255)  // Bright blue for dark mode
            : new ScottPlot.Color(30, 100, 180);  // Darker blue for light mode

        plot.Axes.Bottom.SetTicks(xs, data.Labels);
        plot.Axes.Bottom.TickLabelStyle.Rotation = 45;
        plot.Axes.Left.Label.Text = data.TargetValueName;
        
        // Set title with theme-aware color
        plot.Title($"{data.TargetType} - {data.TargetValueName}");
        plot.Axes.Title.Label.ForeColor = isDark 
            ? new ScottPlot.Color(220, 220, 220)  // Light text for dark mode
            : new ScottPlot.Color(30, 30, 30);    // Dark text for light mode
            
        plot.Axes.AutoScale();

        _graphPlot.Refresh();
    }

    public void Hour_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetResolution(TimeResolution.Hour);
    }

    public void Day_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetResolution(TimeResolution.Day);
    }

    public void Week_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetResolution(TimeResolution.Week);
    }

    public void Month_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetResolution(TimeResolution.Month);
    }

    public void Year_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetResolution(TimeResolution.Year);
    }

    public async void Merge_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.MergeAsync();
        }
    }
}
