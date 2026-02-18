using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();

        DataContextChanged += (_, __) => HookVm();
        AttachedToVisualTree += (_, __) =>
        {
            HookVm();
            InitializePanelsLayout();
            PanelsCanvas.SizeChanged += (_, __) =>
            {
                if (!_panelsInitialized)
                    InitializePanelsLayout();
            };
        };
    }

    private EditorViewModel? _vm;
    private Border? _dragPanel;
    private Point _dragOffset;
    private bool _panelsInitialized;

    private void HookVm()
    {
        if (ReferenceEquals(_vm, DataContext))
            return;

        if (_vm is not null)
        {
            _vm.RequestImportExcel -= OnRequestImportExcel;
            _vm.RequestExportExcel -= OnRequestExportExcel;
            _vm.RequestShowCharts -= OnRequestShowCharts;
        }

        _vm = DataContext as EditorViewModel;
        if (_vm is not null)
        {
            _vm.RequestImportExcel += OnRequestImportExcel;
            _vm.RequestExportExcel += OnRequestExportExcel;
            _vm.RequestShowCharts += OnRequestShowCharts;
        }
    }

    private async void OnRequestShowCharts()
    {
        if (_vm is null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dialog = new RecipeAnalysisDialog(_vm.Points);
        await dialog.ShowDialog(owner);
    }

    private async void OnRequestImportExcel()
    {
        if (_vm is null) return;

        var top = TopLevel.GetTopLevel(this);
        var sp = top?.StorageProvider;
        if (sp is null) return;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Импорт рецепта из Excel",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel")
                {
                    Patterns = new[] { "*.xlsx" },
                    AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        _vm.ImportFromExcel(path);
    }

    private async void OnRequestExportExcel()
    {
        if (_vm is null) return;

        var top = TopLevel.GetTopLevel(this);
        var sp = top?.StorageProvider;
        if (sp is null) return;

        var suggested = string.IsNullOrWhiteSpace(_vm.RecipeCode) ? "recipe" : _vm.RecipeCode;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт рецепта в Excel",
            SuggestedFileName = suggested + ".xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel")
                {
                    Patterns = new[] { "*.xlsx" },
                    AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
                }
            }
        });

        if (file is null) return;
        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        _vm.ExportToExcel(path);
    }

    private void InitializePanelsLayout()
    {
        if (_panelsInitialized)
            return;

        var canvasWidth = PanelsCanvas.Bounds.Width > 0 ? PanelsCanvas.Bounds.Width : Bounds.Width;
        var rightMargin = 10d;
        var x = Math.Max(0, canvasWidth - ParametersPanel.Width - rightMargin);

        Canvas.SetLeft(ParametersPanel, x);
        Canvas.SetTop(ParametersPanel, 8);

        Canvas.SetLeft(VisualizationPanel, x);
        Canvas.SetTop(VisualizationPanel, 220);

        Canvas.SetLeft(SelectedPointPanel, x);
        Canvas.SetTop(SelectedPointPanel, 620);

        _panelsInitialized = true;
    }

    private void Panel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Control source &&
            (source is TextBox || source is CheckBox || source is Slider || source is Button))
            return;

        var control = sender as Control;
        while (control is not null && control is not Border)
        {
            control = control.Parent as Control;
        }

        if (control is not Border panel) return;

        _dragPanel = panel;
        var pos = e.GetPosition(PanelsCanvas);
        _dragOffset = new Point(pos.X - Canvas.GetLeft(panel), pos.Y - Canvas.GetTop(panel));
        panel.ZIndex = 10;
        e.Pointer.Capture(panel);
    }

    private void Panel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPanel is null) return;

        var pos = e.GetPosition(PanelsCanvas);
        var newLeft = Math.Max(0, pos.X - _dragOffset.X);
        var newTop = Math.Max(0, pos.Y - _dragOffset.Y);

        var maxLeft = Math.Max(0, PanelsCanvas.Bounds.Width - _dragPanel.Bounds.Width);
        var maxTop = Math.Max(0, PanelsCanvas.Bounds.Height - _dragPanel.Bounds.Height);

        Canvas.SetLeft(_dragPanel, Math.Min(newLeft, maxLeft));
        Canvas.SetTop(_dragPanel, Math.Min(newTop, maxTop));
    }

    private void Panel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragPanel is null) return;

        _dragPanel.ZIndex = 0;
        e.Pointer.Capture(null);
        _dragPanel = null;
    }

    private void HideParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ParametersPanel.IsVisible = false;

    private void HideVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => VisualizationPanel.IsVisible = false;

    private void HideSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => SelectedPointPanel.IsVisible = false;

    private void ShowParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ParametersPanel.IsVisible = true;

    private void ShowVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => VisualizationPanel.IsVisible = true;

    private void ShowSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => SelectedPointPanel.IsVisible = true;

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = true;
        VisualizationPanel.IsVisible = true;
        SelectedPointPanel.IsVisible = true;
        _panelsInitialized = false;
        InitializePanelsLayout();
    }
}
