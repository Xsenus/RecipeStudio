using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private Border? _resizePanel;
    private Point _resizeStart;
    private Size _resizeStartSize;
    private bool _panelsInitialized;
    private int _zOrderCounter;

    public SimulationView()
    {
        InitializeComponent();

        DataContextChanged += (_, __) => HookVm();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void HookVm()
    {
        if (ReferenceEquals(_vm, DataContext))
            return;

        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as SimulationViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookVm();
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PanelsCanvas.SizeChanged += OnPanelsCanvasSizeChanged;

        if (HasUsableCanvasSize())
            InitializePanelsLayout();

        InitializePanelZOrder();

        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        RecipePlot.ZoomChanged += OnRecipePlotZoomChanged;

        UpdateZoomText();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        PersistPanelsLayout(force: false);
        _panelsInitialized = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationViewModel.RecipePath) && string.IsNullOrWhiteSpace(_vm?.RecipePath))
            PersistPanelsLayout(force: false);
    }

    private void OnRecipePlotZoomChanged(double _)
    {
        UpdateZoomText();
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
        var canvasWidth = GetCanvasWidth();
        return new(PanelMargin, Math.Max(PanelMargin, canvasWidth * 0.02));
    }

    private Point TelemetryPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        return new(Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin), PanelMargin);
    }

    private Point TopViewPanelDefaultPosition(Border panel)
    {
        var canvasHeight = GetCanvasHeight();
        return new(PanelMargin, Math.Max(PanelMargin, canvasHeight - panel.Height - PanelMargin));
    }

    private Point View3DPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        return new(Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin), Math.Max(PanelMargin, canvasHeight - panel.Height - PanelMargin));
    }

    private void ApplyDefaultPanelsLayout()
    {
        Canvas.SetLeft(PlotPanel, PlotPanelDefaultPosition(PlotPanel).X);
        Canvas.SetTop(PlotPanel, PlotPanelDefaultPosition(PlotPanel).Y);

        Canvas.SetLeft(TelemetryPanel, TelemetryPanelDefaultPosition(TelemetryPanel).X);
        Canvas.SetTop(TelemetryPanel, TelemetryPanelDefaultPosition(TelemetryPanel).Y);

        Canvas.SetLeft(TopViewPanel, TopViewPanelDefaultPosition(TopViewPanel).X);
        Canvas.SetTop(TopViewPanel, TopViewPanelDefaultPosition(TopViewPanel).Y);

        Canvas.SetLeft(View3DPanel, View3DPanelDefaultPosition(View3DPanel).X);
        Canvas.SetTop(View3DPanel, View3DPanelDefaultPosition(View3DPanel).Y);

        ClampPanelToCanvas(PlotPanel);
        ClampPanelToCanvas(TelemetryPanel);
        ClampPanelToCanvas(TopViewPanel);
        ClampPanelToCanvas(View3DPanel);
    }

    private void InitializePanelZOrder()
    {
        _zOrderCounter = 0;
        if (PlotPanel.IsVisible) BringPanelToFront(PlotPanel);
        if (TelemetryPanel.IsVisible) BringPanelToFront(TelemetryPanel);
        if (TopViewPanel.IsVisible) BringPanelToFront(TopViewPanel);
        if (View3DPanel.IsVisible) BringPanelToFront(View3DPanel);
    }

    private void BringPanelToFront(Border panel)
    {
        _zOrderCounter = Math.Max(_zOrderCounter + 1, 1);
        panel.ZIndex = _zOrderCounter * 10;
        UpdateResizeHandleFor(panel);
    }

    private void Panel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Control source)
        {
            if (source is TextBox || source is CheckBox || source is Slider || source is Button || source is ToggleButton)
                return;

            if (IsDescendantOf(source, RecipePlot))
                return;
        }

        if (sender is not Border panel)
            return;

        _dragPanel = panel;
        BringPanelToFront(panel);
        var pos = e.GetPosition(PanelsCanvas);
        _dragOffset = new Point(pos.X - Canvas.GetLeft(panel), pos.Y - Canvas.GetTop(panel));
        e.Pointer.Capture(panel);
    }

    private static bool IsDescendantOf(Control source, Control ancestor)
    {
        Control? current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = current.Parent as Control;
        }

        return false;
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

        BringPanelToFront(_resizePanel);
        _resizeStart = e.GetPosition(PanelsCanvas);
        _resizeStartSize = new Size(_resizePanel.Width, _resizePanel.Height);
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

        _resizePanel = null;
        e.Pointer.Capture(null);
        PersistPanelsLayout();
    }

    private void HidePlotPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlotPanel.IsVisible = false;
        PlotResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideTelemetryPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TelemetryPanel.IsVisible = false;
        TelemetryResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideTopViewPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopViewPanel.IsVisible = false;
        TopViewResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideView3DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        View3DPanel.IsVisible = false;
        View3DResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void ShowPlotPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(PlotPanel, PlotResizeHandle);
    private void ShowTelemetryPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(TelemetryPanel, TelemetryResizeHandle);
    private void ShowTopViewPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(TopViewPanel, TopViewResizeHandle);
    private void ShowView3DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(View3DPanel, View3DResizeHandle);

    private void TogglePanel(Border panel, Border handle)
    {
        panel.IsVisible = !panel.IsVisible;
        handle.IsVisible = panel.IsVisible;
        if (panel.IsVisible)
        {
            BringPanelToFront(panel);
            UpdateResizeHandleFor(panel);
        }

        PersistPanelsLayout();
    }

    private void ZoomInPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ZoomIn();
        UpdateZoomText();
    }

    private void ZoomOutPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ZoomOut();
        UpdateZoomText();
    }

    private void FitPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ResetZoom();
        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        ZoomText.Text = $"x{RecipePlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlotPanel.Width = 860;
        PlotPanel.Height = 520;
        TelemetryPanel.Width = 300;
        TelemetryPanel.Height = 340;
        TopViewPanel.Width = 500;
        TopViewPanel.Height = 290;
        View3DPanel.Width = 500;
        View3DPanel.Height = 290;

        PlotPanel.IsVisible = true;
        TelemetryPanel.IsVisible = true;
        TopViewPanel.IsVisible = false;
        View3DPanel.IsVisible = true;

        PlotResizeHandle.IsVisible = true;
        TelemetryResizeHandle.IsVisible = true;
        TopViewResizeHandle.IsVisible = false;
        View3DResizeHandle.IsVisible = true;

        RecipePlot.ResetZoom();
        UpdateZoomText();

        ApplyDefaultPanelsLayout();
        InitializePanelZOrder();
        UpdateResizeHandlePositions();
        PersistPanelsLayout();
    }

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

    private bool HasUsableCanvasSize() => GetCanvasWidth() > 0 && GetCanvasHeight() > 0;
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
