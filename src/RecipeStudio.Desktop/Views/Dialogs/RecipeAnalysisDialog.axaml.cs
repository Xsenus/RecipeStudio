using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.Controls;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class RecipeAnalysisDialog : Window
{
    public RecipeAnalysisDialog()
    {
        InitializeComponent();
        SetChartType(AnalysisChartType.Speeds);
    }

    public RecipeAnalysisDialog(IList<RecipePoint> points)
        : this()
    {
        Chart.Points = points;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void TabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
            return;

        var type = tag switch
        {
            "Angles" => AnalysisChartType.Angles,
            "Coordinates" => AnalysisChartType.Coordinates,
            "Acceleration" => AnalysisChartType.Acceleration,
            _ => AnalysisChartType.Speeds
        };

        SetChartType(type);
    }

    private void SetChartType(AnalysisChartType type)
    {
        Chart.ChartType = type;
        Chart.StartRevealAnimation();

        SpeedsTabButton.Classes.Set("active", type == AnalysisChartType.Speeds);
        AnglesTabButton.Classes.Set("active", type == AnalysisChartType.Angles);
        CoordinatesTabButton.Classes.Set("active", type == AnalysisChartType.Coordinates);
        AccelerationTabButton.Classes.Set("active", type == AnalysisChartType.Acceleration);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
