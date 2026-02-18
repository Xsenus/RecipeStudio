using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.Controls;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class RecipeAnalysisDialog : Window
{
    public RecipeAnalysisDialog(IList<RecipePoint> points)
    {
        InitializeComponent();

        Chart.Points = points;
        Chart.ChartType = AnalysisChartType.Speeds;

        Tabs.SelectionChanged += (_, __) =>
        {
            Chart.ChartType = Tabs.SelectedIndex switch
            {
                1 => AnalysisChartType.Angles,
                2 => AnalysisChartType.Coordinates,
                3 => AnalysisChartType.Acceleration,
                _ => AnalysisChartType.Speeds
            };
        };
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
