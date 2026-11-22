using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ScottPlot;
using ScottPlot.Avalonia;
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
        
        ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        
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

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyPlotTheme();
        _graphPlot?.Refresh();
    }

    private void ApplyPlotTheme()
    {
        if (_graphPlot == null) return;

        var plot = _graphPlot.Plot;
        var bgColor = GetPlotColor("AppBackgroundBrush", ScottPlot.Colors.White);
        var dataBgColor = GetPlotColor("GraphSurfaceBrush", ScottPlot.Colors.White);
        var fgColor = GetPlotColor("AppTextBrush", ScottPlot.Colors.Black);
        var gridColor = GetPlotColor("GraphGridBrush", ScottPlot.Colors.LightGray);
        var lineColor = GetPlotColor("GraphSeriesBrush", ScottPlot.Colors.Blue);

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
        plot.Axes.Title.Label.ForeColor = fgColor;
        plot.Grid.MajorLineColor = gridColor;

        foreach (var scatter in plot.GetPlottables<ScottPlot.Plottables.Scatter>())
            scatter.Color = lineColor;
    }

    private ScottPlot.Color GetPlotColor(string resourceKey, ScottPlot.Color fallback)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
            return new ScottPlot.Color(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);

        return fallback;
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
        
        var xs = Enumerable.Range(0, data.Values.Length).Select(i => (double)i).ToArray();
        var scatter = plot.Add.Scatter(xs, data.Values);
        scatter.MarkerSize = 6;
        scatter.LineWidth = 2;
        
        plot.Axes.Bottom.SetTicks(xs, data.Labels);
        plot.Axes.Bottom.TickLabelStyle.Rotation = 45;
        plot.Axes.Left.Label.Text = data.TargetValueName;
        
        plot.Title($"{data.TargetType} - {data.TargetValueName}");
        ApplyPlotTheme();
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
