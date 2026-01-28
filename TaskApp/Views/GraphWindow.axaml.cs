using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
