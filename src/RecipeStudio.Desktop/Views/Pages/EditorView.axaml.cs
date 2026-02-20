using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class EditorView : UserControl
{
    private const double PanelMargin = 20;

    public EditorView()
    {
        InitializeComponent();

        DataContextChanged += (_, __) => HookVm();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
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
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as EditorViewModel;
        if (_vm is not null)
        {
            _vm.RequestImportExcel += OnRequestImportExcel;
            _vm.RequestExportExcel += OnRequestExportExcel;
            _vm.RequestShowCharts += OnRequestShowCharts;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        HookVm();
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PanelsCanvas.SizeChanged += OnPanelsCanvasSizeChanged;

        if (HasUsableCanvasSize())
            InitializePanelsLayout();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PersistPanelsLayout(force: false);
        _panelsInitialized = false;
    }

    private void OnPanelsCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_panelsInitialized)
        {
            if (HasUsableCanvasSize())
                InitializePanelsLayout();

            return;
        }

        ClampPanelToCanvas(ParametersPanel);
        ClampPanelToCanvas(VisualizationPanel);
        ClampPanelToCanvas(SelectedPointPanel);
        UpdateResizeHandlePositions();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.PropertyName == nameof(EditorViewModel.HasDocument) && !_vm.HasDocument)
        {
            PersistPanelsLayout(force: false);
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
            Title = "Импорт точек из Excel/CSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel/CSV")
                {
                    Patterns = new[] { "*.xlsx", "*.csv", "*.tsv" },
                    AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet", "public.comma-separated-values-text", "public.tab-separated-values-text" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "text/csv", "text/tab-separated-values" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        var preview = _vm.PreviewImport(path);

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dialog = new ImportRecipeDialog(preview, allowRename: false, title: "Импорт точек в текущий рецепт");
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (confirmed)
        {
            _vm.ApplyImportedPreview(preview);
        }
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

        var saved = _vm?.AppSettings.EditorPanels;
        ApplyPanelLayout(ParametersPanel, saved?.Parameters, ParametersPanelDefaultPosition);
        ApplyPanelLayout(VisualizationPanel, saved?.Visualization, VisualizationPanelDefaultPosition);
        ApplyPanelLayout(SelectedPointPanel, saved?.SelectedPoint, SelectedPointPanelDefaultPosition);

        _panelsInitialized = true;
        UpdateResizeHandlePositions();
    }

    private void ApplyPanelLayout(Border panel, PanelPlacementSettings? layout, Func<Border, Point> defaultPosition)
    {
        if (!HasValidLayout(layout))
        {
            var fallback = defaultPosition(panel);
            Canvas.SetLeft(panel, fallback.X);
            Canvas.SetTop(panel, fallback.Y);
            panel.IsVisible = layout?.IsVisible ?? true;
            ClampPanelToCanvas(panel);
            return;
        }

        panel.Width = Math.Max(layout!.Width, panel.MinWidth);
        panel.Height = Math.Max(layout.Height, panel.MinHeight);

        Canvas.SetLeft(panel, layout.Left);
        Canvas.SetTop(panel, layout.Top);
        panel.IsVisible = layout.IsVisible;

        ClampPanelToCanvas(panel);
    }

    private Point ParametersPanelDefaultPosition(Border panel)
    {
        var canvasHeight = GetCanvasHeight();
        return new(PanelMargin, Math.Max(PanelMargin, canvasHeight - panel.Height - PanelMargin));
    }

    private Point VisualizationPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        return new(Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin), PanelMargin);
    }

    private Point SelectedPointPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        return new(
            Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin),
            Math.Max(PanelMargin, canvasHeight - panel.Height - PanelMargin));
    }

    private void ApplyDefaultPanelsLayout()
    {
        var parametersPos = ParametersPanelDefaultPosition(ParametersPanel);
        Canvas.SetLeft(ParametersPanel, parametersPos.X);
        Canvas.SetTop(ParametersPanel, parametersPos.Y);

        var visualizationPos = VisualizationPanelDefaultPosition(VisualizationPanel);
        Canvas.SetLeft(VisualizationPanel, visualizationPos.X);
        Canvas.SetTop(VisualizationPanel, visualizationPos.Y);

        var selectedPointPos = SelectedPointPanelDefaultPosition(SelectedPointPanel);
        Canvas.SetLeft(SelectedPointPanel, selectedPointPos.X);
        Canvas.SetTop(SelectedPointPanel, selectedPointPos.Y);

        ClampPanelToCanvas(ParametersPanel);
        ClampPanelToCanvas(VisualizationPanel);
        ClampPanelToCanvas(SelectedPointPanel);
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
        PersistPanelsLayout();
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
        PersistPanelsLayout();
    }

    private void HideParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = false;
        ParametersResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VisualizationPanel.IsVisible = false;
        VisualizationResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedPointPanel.IsVisible = false;
        SelectedPointResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void ShowParametersPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.IsVisible = !ParametersPanel.IsVisible;
        ParametersResizeHandle.IsVisible = ParametersPanel.IsVisible;
        if (ParametersPanel.IsVisible)
            UpdateResizeHandleFor(ParametersPanel);
        PersistPanelsLayout();
    }

    private void ShowVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VisualizationPanel.IsVisible = !VisualizationPanel.IsVisible;
        VisualizationResizeHandle.IsVisible = VisualizationPanel.IsVisible;
        if (VisualizationPanel.IsVisible)
            UpdateResizeHandleFor(VisualizationPanel);
        PersistPanelsLayout();
    }

    private void ShowSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedPointPanel.IsVisible = !SelectedPointPanel.IsVisible;
        SelectedPointResizeHandle.IsVisible = SelectedPointPanel.IsVisible;
        if (SelectedPointPanel.IsVisible)
            UpdateResizeHandleFor(SelectedPointPanel);
        PersistPanelsLayout();
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ParametersPanel.Width = 390;
        ParametersPanel.Height = 210;
        VisualizationPanel.Width = 520;
        VisualizationPanel.Height = 360;
        SelectedPointPanel.Width = 430;
        SelectedPointPanel.Height = 280;

        ParametersPanel.IsVisible = true;
        VisualizationPanel.IsVisible = true;
        SelectedPointPanel.IsVisible = true;

        ParametersResizeHandle.IsVisible = true;
        VisualizationResizeHandle.IsVisible = true;
        SelectedPointResizeHandle.IsVisible = true;

        ApplyDefaultPanelsLayout();
        UpdateResizeHandlePositions();
        PersistPanelsLayout();
    }

    private bool HasUsableCanvasSize()
    {
        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        return canvasWidth > 0 && canvasHeight > 0;
    }

    private void PersistPanelsLayout(bool force = true)
    {
        if (_vm is null)
            return;

        if (!force && !HasUsableCanvasSize())
            return;

        _vm.AppSettings.EditorPanels.Parameters = ToLayout(ParametersPanel, _vm.AppSettings.EditorPanels.Parameters);
        _vm.AppSettings.EditorPanels.Visualization = ToLayout(VisualizationPanel, _vm.AppSettings.EditorPanels.Visualization);
        _vm.AppSettings.EditorPanels.SelectedPoint = ToLayout(SelectedPointPanel, _vm.AppSettings.EditorPanels.SelectedPoint);
        _vm.SaveAppSettings();
    }

    private static PanelPlacementSettings ToLayout(Border panel, PanelPlacementSettings previous)
    {
        var width = panel.Bounds.Width > 0 ? panel.Bounds.Width : panel.Width;
        var height = panel.Bounds.Height > 0 ? panel.Bounds.Height : panel.Height;

        var left = Canvas.GetLeft(panel);
        var top = Canvas.GetTop(panel);

        return new PanelPlacementSettings
        {
            Left = IsFinite(left) ? left : previous.Left,
            Top = IsFinite(top) ? top : previous.Top,
            Width = IsFinite(width) && width > 0 ? width : (previous.Width > 0 ? previous.Width : panel.MinWidth),
            Height = IsFinite(height) && height > 0 ? height : (previous.Height > 0 ? previous.Height : panel.MinHeight),
            IsVisible = panel.IsVisible
        };
    }

    private static bool HasValidLayout(PanelPlacementSettings? layout)
    {
        if (layout is null)
            return false;

        return IsFinite(layout.Left) &&
               IsFinite(layout.Top) &&
               IsFinite(layout.Width) &&
               IsFinite(layout.Height) &&
               layout.Width > 0 &&
               layout.Height > 0;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private double GetCanvasWidth() => PanelsCanvas.Bounds.Width > 0 ? PanelsCanvas.Bounds.Width : Bounds.Width;

    private double GetCanvasHeight() => PanelsCanvas.Bounds.Height > 0 ? PanelsCanvas.Bounds.Height : Bounds.Height;

    private void ClampPanelToCanvas(Border panel)
    {
        if (!HasUsableCanvasSize())
            return;

        var maxLeft = Math.Max(0, GetCanvasWidth() - panel.Width);
        var maxTop = Math.Max(0, GetCanvasHeight() - panel.Height);

        var left = Canvas.GetLeft(panel);
        var top = Canvas.GetTop(panel);

        Canvas.SetLeft(panel, Math.Clamp(IsFinite(left) ? left : 0, 0, maxLeft));
        Canvas.SetTop(panel, Math.Clamp(IsFinite(top) ? top : 0, 0, maxTop));
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
