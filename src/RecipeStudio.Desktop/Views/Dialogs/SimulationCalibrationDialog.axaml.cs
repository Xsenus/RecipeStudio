using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.Controls;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class SimulationCalibrationDialog : Window
{
    private readonly Action _saveDefaults;
    private readonly Action _resetCalibration;
    private readonly Func<Task> _autoCalibration;

    public SimulationCalibrationDialog()
    {
        InitializeComponent();
        _saveDefaults = static () => { };
        _resetCalibration = static () => { };
        _autoCalibration = static () => Task.CompletedTask;
    }

    public SimulationCalibrationDialog(
        SimulationBlueprint2DControl source,
        Action saveDefaults,
        Action resetCalibration,
        Func<Task> autoCalibration)
        : this()
    {
        DataContext = source;
        _saveDefaults = saveDefaults;
        _resetCalibration = resetCalibration;
        _autoCalibration = autoCalibration;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _saveDefaults();
        Close();
    }

    private void Reset_Click(object? sender, RoutedEventArgs e) => _resetCalibration();

    private async void AutoCal_Click(object? sender, RoutedEventArgs e) => await _autoCalibration();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
