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
                else
                    UpdateResizeHandlePositions();
            };
        };
    }

    private EditorViewModel? _vm;
    private Border? _dragPanel;
    private Point _dragOffset;
    private bool _panelsInitialized;

    private Border? _resizePanel;
    private Point _resizeStart;
    private Size _resizeStartSize;

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

        Canvas.SetLeft(ParametersPanel, 40);
        Canvas.SetTop(ParametersPanel, 320);

        Canvas.SetLeft(SelectedPointPanel, Math.Max(20, canvasWidth - SelectedPointPanel.Width - 20));
        Canvas.SetTop(SelectedPointPanel, 20);

        Canvas.SetLeft(VisualizationPanel, Math.Max(20, canvasWidth - VisualizationPanel.Width - 20));
        Canvas.SetTop(VisualizationPanel, 330);

        _panelsInitialized = true;
        UpdateResizeHandlePositions();
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
        UpdateResizeHandleFor(_dragPanel);
    }

    private void Panel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragPanel is null) return;

        _dragPanel.ZIndex = 0;
        e.Pointer.Capture(null);
        _dragPanel = null;
    }

    private void ResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _resizePanel = sender switch
        {
            Border handle when ReferenceEquals(handle, ParametersResizeHandle) => ParametersPanel,
            Border handle when ReferenceEquals(handle, VisualizationResizeHandle) => VisualizationPanel,
            Border handle when ReferenceEquals(handle, SelectedPointResizeHandle) => SelectedPointPanel,
            _ => null
        };

        if (_resizePanel is null)
            return;

        _resizeStart = e.GetPosition(PanelsCanvas);
        _resizeStartSize = new Size(_resizePanel.Width, _resizePanel.Height);
        _resizePanel.ZIndex = 10;
        e.Pointer.Capture(sender as IInputElement);
        e.Handled = true;
    }

    private void ResizeHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizePanel is null)
            return;

        var pos = e.GetPosition(PanelsCanvas);
        var deltaX = pos.X - _resizeStart.X;
        var deltaY = pos.Y - _resizeStart.Y;

        var minW = _resizePanel.MinWidth <= 0 ? 260 : _resizePanel.MinWidth;
        var minH = _resizePanel.MinHeight <= 0 ? 160 : _resizePanel.MinHeight;

        var left = Canvas.GetLeft(_resizePanel);
        var top = Canvas.GetTop(_resizePanel);

        var maxW = Math.Max(minW, PanelsCanvas.Bounds.Width - left);
        var maxH = Math.Max(minH, PanelsCanvas.Bounds.Height - top);

        var width = Math.Clamp(_resizeStartSize.Width + deltaX, minW, maxW);
        var height = Math.Clamp(_resizeStartSize.Height + deltaY, minH, maxH);

        _resizePanel.Width = width;
        _resizePanel.Height = height;
        UpdateResizeHandleFor(_resizePanel);
    }

    private void ResizeHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizePanel is null)
            return;

        _resizePanel.ZIndex = 0;
        _resizePanel = null;
        e.Pointer.Capture(null);
    }

    private void HideParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = false;
        ParametersResizeHandle.IsVisible = false;
    }

    private void HideVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VisualizationPanel.IsVisible = false;
        VisualizationResizeHandle.IsVisible = false;
    }

    private void HideSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedPointPanel.IsVisible = false;
        SelectedPointResizeHandle.IsVisible = false;
    }

    private void ShowParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = !ParametersPanel.IsVisible;
        ParametersResizeHandle.IsVisible = ParametersPanel.IsVisible;
        if (ParametersPanel.IsVisible)
            UpdateResizeHandleFor(ParametersPanel);
    }

    private void ShowVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VisualizationPanel.IsVisible = !VisualizationPanel.IsVisible;
        VisualizationResizeHandle.IsVisible = VisualizationPanel.IsVisible;
        if (VisualizationPanel.IsVisible)
            UpdateResizeHandleFor(VisualizationPanel);
    }

    private void ShowSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedPointPanel.IsVisible = !SelectedPointPanel.IsVisible;
        SelectedPointResizeHandle.IsVisible = SelectedPointPanel.IsVisible;
        if (SelectedPointPanel.IsVisible)
            UpdateResizeHandleFor(SelectedPointPanel);
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = true;
        VisualizationPanel.IsVisible = true;
        SelectedPointPanel.IsVisible = true;

        ParametersResizeHandle.IsVisible = true;
        VisualizationResizeHandle.IsVisible = true;
        SelectedPointResizeHandle.IsVisible = true;

        _panelsInitialized = false;
        InitializePanelsLayout();
    }

    private void UpdateResizeHandlePositions()
    {
        UpdateResizeHandleFor(ParametersPanel);
        UpdateResizeHandleFor(VisualizationPanel);
        UpdateResizeHandleFor(SelectedPointPanel);
    }

    private void UpdateResizeHandleFor(Border panel)
    {
        var handle = panel == ParametersPanel
            ? ParametersResizeHandle
            : panel == VisualizationPanel
                ? VisualizationResizeHandle
                : SelectedPointResizeHandle;

        if (!panel.IsVisible)
        {
            handle.IsVisible = false;
            return;
        }

        handle.IsVisible = true;
        handle.ZIndex = panel.ZIndex + 1;
        Canvas.SetLeft(handle, Canvas.GetLeft(panel) + panel.Bounds.Width - handle.Width / 2);
        Canvas.SetTop(handle, Canvas.GetTop(panel) + panel.Bounds.Height - handle.Height / 2);
    }
}
