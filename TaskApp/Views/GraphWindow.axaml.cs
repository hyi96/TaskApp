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

        if (isDark)
        {
            // Dark theme colors
            var bgColor = new ScottPlot.Color(30, 30, 30);       // Dark background
            var fgColor = new ScottPlot.Color(220, 220, 220);    // Light text
            var gridColor = new ScottPlot.Color(60, 60, 60);     // Subtle grid
            
            plot.FigureBackground.Color = bgColor;
            plot.DataBackground.Color = new ScottPlot.Color(45, 45, 45);
            
            plot.Axes.Bottom.Label.ForeColor = fgColor;
            plot.Axes.Bottom.TickLabelStyle.ForeColor = fgColor;
            plot.Axes.Bottom.MajorTickStyle.Color = gridColor;
            plot.Axes.Bottom.MinorTickStyle.Color = gridColor;
            plot.Axes.Bottom.FrameLineStyle.Color = gridColor;
            
            plot.Axes.Left.Label.ForeColor = fgColor;
            plot.Axes.Left.TickLabelStyle.ForeColor = fgColor;
            plot.Axes.Left.MajorTickStyle.Color = gridColor;
            plot.Axes.Left.MinorTickStyle.Color = gridColor;
            plot.Axes.Left.FrameLineStyle.Color = gridColor;
            
            plot.Axes.Top.FrameLineStyle.Color = gridColor;
            plot.Axes.Right.FrameLineStyle.Color = gridColor;
            
            plot.Grid.MajorLineColor = gridColor;
        }
        else
        {
            // Light theme colors
            var bgColor = new ScottPlot.Color(255, 255, 255);    // White background
            var fgColor = new ScottPlot.Color(30, 30, 30);       // Dark text
            var gridColor = new ScottPlot.Color(200, 200, 200);  // Light grid
            
            plot.FigureBackground.Color = bgColor;
            plot.DataBackground.Color = new ScottPlot.Color(250, 250, 250);
            
            plot.Axes.Bottom.Label.ForeColor = fgColor;
            plot.Axes.Bottom.TickLabelStyle.ForeColor = fgColor;
            plot.Axes.Bottom.MajorTickStyle.Color = gridColor;
            plot.Axes.Bottom.MinorTickStyle.Color = gridColor;
            plot.Axes.Bottom.FrameLineStyle.Color = gridColor;
            
            plot.Axes.Left.Label.ForeColor = fgColor;
            plot.Axes.Left.TickLabelStyle.ForeColor = fgColor;
            plot.Axes.Left.MajorTickStyle.Color = gridColor;
            plot.Axes.Left.MinorTickStyle.Color = gridColor;
            plot.Axes.Left.FrameLineStyle.Color = gridColor;
            
            plot.Axes.Top.FrameLineStyle.Color = gridColor;
            plot.Axes.Right.FrameLineStyle.Color = gridColor;
            
            plot.Grid.MajorLineColor = gridColor;
        }
    }

    private bool IsDarkTheme()
    {
        // Check the actual theme variant
        var themeVariant = Application.Current?.ActualThemeVariant;
        return themeVariant == ThemeVariant.Dark;
    }

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
}
