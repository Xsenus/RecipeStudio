using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class SimulationView : UserControl
{
    private const double PanelMargin = 20;

    private SimulationViewModel? _vm;
    private Border? _dragPanel;
    private Point _dragOffset;
    private bool _panelsInitialized;

    private Border? _resizePanel;
    private Point _resizeStart;
    private Size _resizeStartSize;

    public SimulationView()
    {
        InitializeComponent();
        DataContextChanged += (_, __) => HookVm();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void HookVm()
    {
        _vm = DataContext as SimulationViewModel;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookVm();
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PanelsCanvas.SizeChanged += OnPanelsCanvasSizeChanged;

        if (HasUsableCanvasSize())
            InitializePanelsLayout();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
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

        ClampPanelToCanvas(PlotPanel);
        ClampPanelToCanvas(TelemetryPanel);
        ClampPanelToCanvas(TopViewPanel);
        ClampPanelToCanvas(View3DPanel);
        UpdateResizeHandlePositions();
    }

    private void InitializePanelsLayout()
    {
        if (_panelsInitialized)
            return;

        var saved = _vm?.AppSettings.SimulationPanels;
        ApplyPanelLayout(PlotPanel, saved?.Plot, PlotPanelDefaultPosition);
        ApplyPanelLayout(TelemetryPanel, saved?.Telemetry, TelemetryPanelDefaultPosition);
        ApplyPanelLayout(TopViewPanel, saved?.TopView, TopViewPanelDefaultPosition);
        ApplyPanelLayout(View3DPanel, saved?.View3D, View3DPanelDefaultPosition);

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

    private Point PlotPanelDefaultPosition(Border panel)
    {
        return new(PanelMargin, PanelMargin);
    }

    private Point TelemetryPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        return new(Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin), PanelMargin);
    }

    private Point TopViewPanelDefaultPosition(Border panel)
    {
        return new(PanelMargin, Math.Max(PanelMargin, GetCanvasHeight() - panel.Height - PanelMargin));
    }

    private Point View3DPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        return new(
            Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin),
            Math.Max(PanelMargin, canvasHeight - panel.Height - PanelMargin));
    }

    private void Panel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Control source &&
            (source is Slider || source is Button))
            return;

        if (sender is not Border panel)
            return;

        _dragPanel = panel;
        var pos = e.GetPosition(PanelsCanvas);
        _dragOffset = new Point(pos.X - Canvas.GetLeft(panel), pos.Y - Canvas.GetTop(panel));
        panel.ZIndex = 10;
        e.Pointer.Capture(panel);
    }

    private void Panel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPanel is null)
            return;

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
        if (_dragPanel is null)
            return;

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
            Border handle when ReferenceEquals(handle, PlotResizeHandle) => PlotPanel,
            Border handle when ReferenceEquals(handle, TelemetryResizeHandle) => TelemetryPanel,
            Border handle when ReferenceEquals(handle, TopViewResizeHandle) => TopViewPanel,
            Border handle when ReferenceEquals(handle, View3DResizeHandle) => View3DPanel,
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

        _resizePanel.Width = Math.Clamp(_resizeStartSize.Width + deltaX, minW, maxW);
        _resizePanel.Height = Math.Clamp(_resizeStartSize.Height + deltaY, minH, maxH);
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

    private void ShowPlotPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlotPanel.IsVisible = !PlotPanel.IsVisible;
        PlotResizeHandle.IsVisible = PlotPanel.IsVisible;
        if (PlotPanel.IsVisible)
            UpdateResizeHandleFor(PlotPanel);

        PersistPanelsLayout();
    }

    private void ShowTelemetryPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TelemetryPanel.IsVisible = !TelemetryPanel.IsVisible;
        TelemetryResizeHandle.IsVisible = TelemetryPanel.IsVisible;
        if (TelemetryPanel.IsVisible)
            UpdateResizeHandleFor(TelemetryPanel);

        PersistPanelsLayout();
    }

    private void ShowTopViewPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopViewPanel.IsVisible = !TopViewPanel.IsVisible;
        TopViewResizeHandle.IsVisible = TopViewPanel.IsVisible;
        if (TopViewPanel.IsVisible)
            UpdateResizeHandleFor(TopViewPanel);

        PersistPanelsLayout();
    }

    private void ShowView3DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        View3DPanel.IsVisible = !View3DPanel.IsVisible;
        View3DResizeHandle.IsVisible = View3DPanel.IsVisible;
        if (View3DPanel.IsVisible)
            UpdateResizeHandleFor(View3DPanel);

        PersistPanelsLayout();
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlotPanel.Width = 980;
        PlotPanel.Height = 560;
        TelemetryPanel.Width = 300;
        TelemetryPanel.Height = 360;
        TopViewPanel.Width = 420;
        TopViewPanel.Height = 280;
        View3DPanel.Width = 460;
        View3DPanel.Height = 320;

        PlotPanel.IsVisible = true;
        TelemetryPanel.IsVisible = true;
        TopViewPanel.IsVisible = false;
        View3DPanel.IsVisible = true;

        var plotPos = PlotPanelDefaultPosition(PlotPanel);
        Canvas.SetLeft(PlotPanel, plotPos.X);
        Canvas.SetTop(PlotPanel, plotPos.Y);

        var telemetryPos = TelemetryPanelDefaultPosition(TelemetryPanel);
        Canvas.SetLeft(TelemetryPanel, telemetryPos.X);
        Canvas.SetTop(TelemetryPanel, telemetryPos.Y);

        var topViewPos = TopViewPanelDefaultPosition(TopViewPanel);
        Canvas.SetLeft(TopViewPanel, topViewPos.X);
        Canvas.SetTop(TopViewPanel, topViewPos.Y);

        var view3DPos = View3DPanelDefaultPosition(View3DPanel);
        Canvas.SetLeft(View3DPanel, view3DPos.X);
        Canvas.SetTop(View3DPanel, view3DPos.Y);

        UpdateResizeHandlePositions();
        PersistPanelsLayout();
    }

    private bool HasUsableCanvasSize() => GetCanvasWidth() > 0 && GetCanvasHeight() > 0;

    private void PersistPanelsLayout(bool force = true)
    {
        if (_vm is null)
            return;

        if (!force && !HasUsableCanvasSize())
            return;

        _vm.AppSettings.SimulationPanels.Plot = ToLayout(PlotPanel, _vm.AppSettings.SimulationPanels.Plot);
        _vm.AppSettings.SimulationPanels.Telemetry = ToLayout(TelemetryPanel, _vm.AppSettings.SimulationPanels.Telemetry);
        _vm.AppSettings.SimulationPanels.TopView = ToLayout(TopViewPanel, _vm.AppSettings.SimulationPanels.TopView);
        _vm.AppSettings.SimulationPanels.View3D = ToLayout(View3DPanel, _vm.AppSettings.SimulationPanels.View3D);
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

        return IsFinite(layout.Left) && IsFinite(layout.Top) && IsFinite(layout.Width) && IsFinite(layout.Height) && layout.Width > 0 && layout.Height > 0;
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
        UpdateResizeHandleFor(PlotPanel);
        UpdateResizeHandleFor(TelemetryPanel);
        UpdateResizeHandleFor(TopViewPanel);
        UpdateResizeHandleFor(View3DPanel);
    }

    private void UpdateResizeHandleFor(Border panel)
    {
        var handle = panel == PlotPanel
            ? PlotResizeHandle
            : panel == TelemetryPanel
                ? TelemetryResizeHandle
                : panel == TopViewPanel
                    ? TopViewResizeHandle
                    : View3DResizeHandle;

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
