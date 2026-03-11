using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using System;
using System.Threading.Tasks;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class CncInstructionDialog : Window
{
    private RecipeDocument? _doc;
    private AppSettings? _settings;
    private Func<Window, Task>? _exportHandler;

    public CncInstructionDialog()
    {
        InitializeComponent();
        BuildColumns();
    }

    public CncInstructionDialog(RecipeDocument doc, AppSettings settings, Func<Window, Task>? exportHandler = null)
        : this()
    {
        _doc = doc;
        _settings = settings;
        _exportHandler = exportHandler;
        RefreshRows();
    }

    private void BuildColumns()
    {
        if (CalcGrid.Columns.Count > 0)
            return;

        foreach (var column in CncInstructionService.Columns)
        {
            CalcGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding($"[{column.Key}]"),
                Width = new DataGridLength(column.Width),
                IsReadOnly = true
            });
        }
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (_exportHandler is null)
            return;

        await _exportHandler(this);
    }

    private void RefreshRows()
    {
        if (_doc is null || _settings is null)
            return;

        CalcGrid.ItemsSource = CncInstructionService.BuildRows(_doc, _settings);
    }
}
