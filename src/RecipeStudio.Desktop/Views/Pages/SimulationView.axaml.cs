using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RecipeStudio.Desktop.Controls;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class SimulationView : UserControl
{
    private const double PanelMargin = 20;
    private const double TopUiReserve = 52;
    private const double DefaultPanelGap = 16;
    private const double TelemetryTopOffset = 22;
    private const double ResizeBorderThickness = 8;

    private SimulationViewModel? _vm;
    private Border? _dragPanel;
    private Point _dragOffset;
    private Border? _resizePanel;
    private Point _resizeStart;
    private Point _resizeStartOrigin;
    private Size _resizeStartSize;
    private PanelResizeDirection _resizeDirection;
    private SimulationCalibrationDialog? _calibrationDialog;
    private bool _panelsInitialized;
    private bool _calibrationLoaded;
    private bool _applyingTargetDisplayModes;
    private int _zOrderCounter;

    [Flags]
    private enum PanelResizeDirection
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

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
        _calibrationLoaded = false;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;

        if (VisualRoot is not null)
        {
            ApplySavedTargetDisplayModes();
            ApplySavedView2DPairOverlaySettings();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookVm();
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PanelsCanvas.SizeChanged += OnPanelsCanvasSizeChanged;

        if (HasUsableCanvasSize())
            InitializePanelsLayout();

        ApplySaved2DCalibration();
        ApplySavedTargetDisplayModes();
        ApplySavedView2DPairOverlaySettings();
        InitializePanelZOrder();

        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        RecipePlot.ZoomChanged += OnRecipePlotZoomChanged;
        RecipePlot.InfoBoxPositionChanged -= OnRecipePlotInfoBoxPositionChanged;
        RecipePlot.InfoBoxPositionChanged += OnRecipePlotInfoBoxPositionChanged;
        TopViewPlot.ZoomChanged -= OnTopViewZoomChanged;
        TopViewPlot.ZoomChanged += OnTopViewZoomChanged;
        View2DPlot.ZoomChanged -= OnView2DZoomChanged;
        View2DPlot.ZoomChanged += OnView2DZoomChanged;
        View2DFactPlot.ZoomChanged -= OnView2DFactZoomChanged;
        View2DFactPlot.ZoomChanged += OnView2DFactZoomChanged;
        View2DPairPlot.ZoomChanged -= OnView2DPairZoomChanged;
        View2DPairPlot.ZoomChanged += OnView2DPairZoomChanged;

        UpdateZoomText();
        UpdateTopViewZoomText();
        UpdateView2DZoomText();
        UpdateView2DFactZoomText();
        UpdateView2DPairZoomText();
        UpdatePlotOverlayButtons();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        RecipePlot.InfoBoxPositionChanged -= OnRecipePlotInfoBoxPositionChanged;
        TopViewPlot.ZoomChanged -= OnTopViewZoomChanged;
        View2DPlot.ZoomChanged -= OnView2DZoomChanged;
        View2DFactPlot.ZoomChanged -= OnView2DFactZoomChanged;
        View2DPairPlot.ZoomChanged -= OnView2DPairZoomChanged;
        CloseCalibrationDialog();
        PersistPanelsLayout(force: false);
        _panelsInitialized = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationViewModel.RecipePath) && string.IsNullOrWhiteSpace(_vm?.RecipePath))
            PersistPanelsLayout(force: false);

        if (e.PropertyName is nameof(SimulationViewModel.ShowLegend) or nameof(SimulationViewModel.ShowPairLinks))
            UpdatePlotOverlayButtons();
    }

    private void OnRecipePlotZoomChanged(double _) => UpdateZoomText();
    private void OnRecipePlotInfoBoxPositionChanged() => _vm?.SaveAppSettings();
    private void OnTopViewZoomChanged(double _) => UpdateTopViewZoomText();
    private void OnView2DZoomChanged(double _) => UpdateView2DZoomText();
    private void OnView2DFactZoomChanged(double _) => UpdateView2DFactZoomText();
    private void OnView2DPairZoomChanged(double _) => UpdateView2DPairZoomText();

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
        ClampPanelToCanvas(View2DPanel);
        ClampPanelToCanvas(View2DFactPanel);
        ClampPanelToCanvas(View2DPairPanel);
        ClampPanelToCanvas(View3DPanel);
        UpdateResizeHandlePositions();
    }

    private void InitializePanelsLayout()
    {
        if (_panelsInitialized)
            return;

        ApplyPrimaryDefaultPanelSizes();

        var saved = _vm?.AppSettings.SimulationPanels;
        ApplyPanelLayout(PlotPanel, saved?.Plot, PlotPanelDefaultPosition);
        ApplyPanelLayout(TelemetryPanel, saved?.Telemetry, TelemetryPanelDefaultPosition);
        ApplyPanelLayout(TopViewPanel, saved?.TopView, TopViewPanelDefaultPosition);
        ApplyPanelLayout(View2DPanel, saved?.View2D, View2DPanelDefaultPosition);
        ApplyPanelLayout(View2DFactPanel, saved?.View2DFact, View2DFactPanelDefaultPosition);
        ApplyPanelLayout(View2DPairPanel, saved?.View2DPair, View2DPairPanelDefaultPosition);
        ApplyPanelLayout(View3DPanel, saved?.View3D, View3DPanelDefaultPosition);

        _panelsInitialized = true;
        ApplyPanelsAccessRestrictions();
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
        return GetPrimaryDefaultPanelPositions().Plot;
    }

    private Point TelemetryPanelDefaultPosition(Border panel)
    {
        return GetPrimaryDefaultPanelPositions().Telemetry;
    }

    private Point TopViewPanelDefaultPosition(Border panel)
    {
        var canvasHeight = GetCanvasHeight();
        return new(PanelMargin, Math.Max(TopUiReserve, canvasHeight - panel.Height - PanelMargin));
    }

    private Point View2DPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        return new(
            Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin),
            TopUiReserve);
    }

    private Point View2DFactPanelDefaultPosition(Border panel)
    {
        var canvasWidth = GetCanvasWidth();
        return new(
            Math.Max(PanelMargin, canvasWidth - panel.Width - PanelMargin),
            TopUiReserve + 380);
    }

    private Point View2DPairPanelDefaultPosition(Border panel)
    {
        return GetPrimaryDefaultPanelPositions().Pair2D;
    }

    private Point View3DPanelDefaultPosition(Border _) => new(PanelMargin, TopUiReserve);

    private (Point Plot, Point Telemetry, Point Pair2D) GetPrimaryDefaultPanelPositions()
    {
        var canvasWidth = GetCanvasWidth();
        var pair2DLeft = PanelMargin;
        var pair2DTop = TopUiReserve;

        var plotLeft = pair2DLeft + View2DPairPanel.Width + DefaultPanelGap;
        var plotTop = TopUiReserve;

        var telemetryLeft = Math.Max(PanelMargin, canvasWidth - TelemetryPanel.Width - PanelMargin);
        var telemetryTop = TopUiReserve + TelemetryTopOffset;

        return (
            new Point(plotLeft, plotTop),
            new Point(telemetryLeft, telemetryTop),
            new Point(pair2DLeft, pair2DTop));
    }

    private void ApplyPrimaryDefaultPanelSizes()
    {
        if (!HasUsableCanvasSize())
        {
            PlotPanel.Width = 700;
            PlotPanel.Height = 640;
            TelemetryPanel.Width = 300;
            TelemetryPanel.Height = 330;
            View2DPairPanel.Width = 580;
            View2DPairPanel.Height = 640;
            return;
        }

        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        var mainHeight = Math.Max(Math.Max(PlotPanel.MinHeight, View2DPairPanel.MinHeight), canvasHeight - TopUiReserve - PanelMargin);

        TelemetryPanel.Width = ClampPanelDimension(canvasWidth * 0.16, TelemetryPanel.MinWidth, 320);
        TelemetryPanel.Height = ClampPanelDimension(canvasHeight * 0.34, TelemetryPanel.MinHeight, 340);

        var mainAvailableWidth = Math.Max(
            View2DPairPanel.MinWidth + PlotPanel.MinWidth + DefaultPanelGap,
            canvasWidth - TelemetryPanel.Width - PanelMargin * 2 - DefaultPanelGap * 2);

        var pair2DWidth = ClampPanelDimension(mainAvailableWidth * 0.44, View2DPairPanel.MinWidth, 640);
        var plotWidth = Math.Max(PlotPanel.MinWidth, mainAvailableWidth - pair2DWidth - DefaultPanelGap);
        if (pair2DWidth + plotWidth + DefaultPanelGap > mainAvailableWidth)
            pair2DWidth = Math.Max(View2DPairPanel.MinWidth, mainAvailableWidth - PlotPanel.MinWidth - DefaultPanelGap);

        View2DPairPanel.Width = pair2DWidth;
        PlotPanel.Width = Math.Max(PlotPanel.MinWidth, mainAvailableWidth - View2DPairPanel.Width - DefaultPanelGap);

        View2DPairPanel.Height = mainHeight;
        PlotPanel.Height = mainHeight;
    }

    private static double ClampPanelDimension(double value, double min, double max)
    {
        var safeMin = min > 0 ? min : 0;
        var safeMax = Math.Max(safeMin, max);
        return Math.Clamp(value, safeMin, safeMax);
    }

    private SimulationPanelsAccessSettings GetPanelsAccess()
    {
        if (_vm is null)
            return new SimulationPanelsAccessSettings();

        _vm.AppSettings.SimulationPanels.Access ??= new SimulationPanelsAccessSettings();
        return _vm.AppSettings.SimulationPanels.Access;
    }

    private bool IsPanelAllowed(Border panel)
    {
        var access = GetPanelsAccess();
        if (ReferenceEquals(panel, PlotPanel))
            return access.Plot;

        if (ReferenceEquals(panel, TelemetryPanel))
            return access.Telemetry;

        if (ReferenceEquals(panel, TopViewPanel))
            return access.TopView;

        if (ReferenceEquals(panel, View2DPanel))
            return access.View2D;

        if (ReferenceEquals(panel, View2DFactPanel))
            return access.View2DFact;

        if (ReferenceEquals(panel, View2DPairPanel))
            return access.View2DPair;

        if (ReferenceEquals(panel, View3DPanel))
            return access.View3D;

        return true;
    }

    private void ApplyPanelsAccessRestrictions()
    {
        ApplyPanelAccess(PlotPanel, PlotResizeHandle, IsPanelAllowed(PlotPanel));
        ApplyPanelAccess(TelemetryPanel, TelemetryResizeHandle, IsPanelAllowed(TelemetryPanel));
        ApplyPanelAccess(TopViewPanel, TopViewResizeHandle, IsPanelAllowed(TopViewPanel));
        ApplyPanelAccess(View2DPanel, View2DResizeHandle, IsPanelAllowed(View2DPanel));
        ApplyPanelAccess(View2DFactPanel, View2DFactResizeHandle, IsPanelAllowed(View2DFactPanel));
        ApplyPanelAccess(View2DPairPanel, View2DPairResizeHandle, IsPanelAllowed(View2DPairPanel));
        ApplyPanelAccess(View3DPanel, View3DResizeHandle, IsPanelAllowed(View3DPanel));
    }

    private static void ApplyPanelAccess(Border panel, Border handle, bool isAllowed)
    {
        if (isAllowed)
            return;

        panel.IsVisible = false;
        handle.IsVisible = false;
    }

    private void ApplyDefaultPanelsLayout()
    {
        ApplyPrimaryDefaultPanelSizes();

        var plotPos = PlotPanelDefaultPosition(PlotPanel);
        Canvas.SetLeft(PlotPanel, plotPos.X);
        Canvas.SetTop(PlotPanel, plotPos.Y);

        var telemetryPos = TelemetryPanelDefaultPosition(TelemetryPanel);
        Canvas.SetLeft(TelemetryPanel, telemetryPos.X);
        Canvas.SetTop(TelemetryPanel, telemetryPos.Y);

        var topPos = TopViewPanelDefaultPosition(TopViewPanel);
        Canvas.SetLeft(TopViewPanel, topPos.X);
        Canvas.SetTop(TopViewPanel, topPos.Y);

        var view2DPos = View2DPanelDefaultPosition(View2DPanel);
        Canvas.SetLeft(View2DPanel, view2DPos.X);
        Canvas.SetTop(View2DPanel, view2DPos.Y);

        var view2DFactPos = View2DFactPanelDefaultPosition(View2DFactPanel);
        Canvas.SetLeft(View2DFactPanel, view2DFactPos.X);
        Canvas.SetTop(View2DFactPanel, view2DFactPos.Y);

        var view2DPairPos = View2DPairPanelDefaultPosition(View2DPairPanel);
        Canvas.SetLeft(View2DPairPanel, view2DPairPos.X);
        Canvas.SetTop(View2DPairPanel, view2DPairPos.Y);

        var view3DPos = View3DPanelDefaultPosition(View3DPanel);
        Canvas.SetLeft(View3DPanel, view3DPos.X);
        Canvas.SetTop(View3DPanel, view3DPos.Y);

        ClampPanelToCanvas(PlotPanel);
        ClampPanelToCanvas(TelemetryPanel);
        ClampPanelToCanvas(TopViewPanel);
        ClampPanelToCanvas(View2DPanel);
        ClampPanelToCanvas(View2DFactPanel);
        ClampPanelToCanvas(View2DPairPanel);
        ClampPanelToCanvas(View3DPanel);
    }

    private void InitializePanelZOrder()
    {
        _zOrderCounter = 0;
        if (View3DPanel.IsVisible) BringPanelToFront(View3DPanel);
        if (TelemetryPanel.IsVisible) BringPanelToFront(TelemetryPanel);
        if (PlotPanel.IsVisible) BringPanelToFront(PlotPanel);
        if (TopViewPanel.IsVisible) BringPanelToFront(TopViewPanel);
        if (View2DPanel.IsVisible) BringPanelToFront(View2DPanel);
        if (View2DFactPanel.IsVisible) BringPanelToFront(View2DFactPanel);
        if (View2DPairPanel.IsVisible) BringPanelToFront(View2DPairPanel);
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

        if (sender is not Border panel)
            return;

        var resizeDirection = GetResizeDirection(panel, e.GetPosition(panel));
        if (resizeDirection != PanelResizeDirection.None)
        {
            StartPanelResize(panel, resizeDirection, e, panel);
            return;
        }

        var header = GetPanelHeader(panel);
        if (header is null)
            return;

        // Panel drag is allowed only from panel headers.
        // This prevents stealing pointer input from embedded controls (plot/3D viewport).
        var headerPos = e.GetPosition(header);
        if (headerPos.X < 0 || headerPos.Y < 0 || headerPos.X > header.Bounds.Width || headerPos.Y > header.Bounds.Height)
            return;

        if (e.Source is Control source)
        {
            if (source is TextBox || source is CheckBox || source is Slider || source is Button || source is ToggleButton || source is ComboBox ||
                source.FindAncestorOfType<TextBox>() is not null ||
                source.FindAncestorOfType<CheckBox>() is not null ||
                source.FindAncestorOfType<Slider>() is not null ||
                source.FindAncestorOfType<Button>() is not null ||
                source.FindAncestorOfType<ToggleButton>() is not null ||
                source.FindAncestorOfType<ComboBox>() is not null)
                return;
        }

        _dragPanel = panel;
        BringPanelToFront(panel);
        var pos = e.GetPosition(PanelsCanvas);
        _dragOffset = new Point(pos.X - Canvas.GetLeft(panel), pos.Y - Canvas.GetTop(panel));
        e.Pointer.Capture(panel);
        e.Handled = true;
    }

    private Control? GetPanelHeader(Border panel)
    {
        if (ReferenceEquals(panel, PlotPanel))
            return PlotPanelHeader;

        if (ReferenceEquals(panel, TelemetryPanel))
            return TelemetryPanelHeader;

        if (ReferenceEquals(panel, TopViewPanel))
            return TopViewPanelHeader;

        if (ReferenceEquals(panel, View2DPanel))
            return View2DPanelHeader;

        if (ReferenceEquals(panel, View2DFactPanel))
            return View2DFactPanelHeader;

        if (ReferenceEquals(panel, View2DPairPanel))
            return View2DPairPanelHeader;

        if (ReferenceEquals(panel, View3DPanel))
            return View3DPanelHeader;

        return null;
    }

    private void Panel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Border panel)
        {
            if (ReferenceEquals(_resizePanel, panel))
            {
                ResizeActivePanel(e, TopUiReserve);
                return;
            }

            UpdatePanelCursor(panel, e.GetPosition(panel));
        }

        if (_dragPanel is null)
            return;

        var pos = e.GetPosition(PanelsCanvas);
        var newLeft = Math.Max(0, pos.X - _dragOffset.X);
        var newTop = Math.Max(TopUiReserve, pos.Y - _dragOffset.Y);
        var maxLeft = Math.Max(0, PanelsCanvas.Bounds.Width - _dragPanel.Bounds.Width);
        var maxTop = Math.Max(TopUiReserve, PanelsCanvas.Bounds.Height - _dragPanel.Bounds.Height);

        Canvas.SetLeft(_dragPanel, Math.Min(newLeft, maxLeft));
        Canvas.SetTop(_dragPanel, Math.Min(newTop, maxTop));
        UpdateResizeHandleFor(_dragPanel);
    }

    private void Panel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizePanel is not null)
        {
            _resizePanel = null;
            _resizeDirection = PanelResizeDirection.None;
            e.Pointer.Capture(null);
            PersistPanelsLayout();
            return;
        }

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
            Border handle when ReferenceEquals(handle, View2DResizeHandle) => View2DPanel,
            Border handle when ReferenceEquals(handle, View2DFactResizeHandle) => View2DFactPanel,
            Border handle when ReferenceEquals(handle, View2DPairResizeHandle) => View2DPairPanel,
            Border handle when ReferenceEquals(handle, View3DResizeHandle) => View3DPanel,
            _ => null
        };

        if (_resizePanel is null)
            return;

        StartPanelResize(_resizePanel, PanelResizeDirection.Right | PanelResizeDirection.Bottom, e, sender as IInputElement);
    }

    private void ResizeHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizePanel is null)
            return;

        ResizeActivePanel(e, TopUiReserve);
    }

    private void ResizeHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizePanel is null)
            return;

        _resizePanel = null;
        _resizeDirection = PanelResizeDirection.None;
        e.Pointer.Capture(null);
        PersistPanelsLayout();
    }

    private void StartPanelResize(Border panel, PanelResizeDirection direction, PointerPressedEventArgs e, IInputElement? captureTarget)
    {
        _dragPanel = null;
        _resizePanel = panel;
        _resizeDirection = direction;
        BringPanelToFront(panel);
        _resizeStart = e.GetPosition(PanelsCanvas);
        _resizeStartOrigin = new Point(Canvas.GetLeft(panel), Canvas.GetTop(panel));
        var width = panel.Bounds.Width > 0 ? panel.Bounds.Width : panel.Width;
        var height = panel.Bounds.Height > 0 ? panel.Bounds.Height : panel.Height;
        _resizeStartSize = new Size(width, height);
        e.Pointer.Capture(captureTarget ?? panel);
        e.Handled = true;
    }

    private void ResizeActivePanel(PointerEventArgs e, double minTop)
    {
        if (_resizePanel is null || _resizeDirection == PanelResizeDirection.None)
            return;

        var pos = e.GetPosition(PanelsCanvas);
        var deltaX = pos.X - _resizeStart.X;
        var deltaY = pos.Y - _resizeStart.Y;

        var minW = _resizePanel.MinWidth <= 0 ? 260 : _resizePanel.MinWidth;
        var minH = _resizePanel.MinHeight <= 0 ? 160 : _resizePanel.MinHeight;
        var canvasWidth = PanelsCanvas.Bounds.Width;
        var canvasHeight = PanelsCanvas.Bounds.Height;

        var left = _resizeStartOrigin.X;
        var top = _resizeStartOrigin.Y;
        var width = _resizeStartSize.Width;
        var height = _resizeStartSize.Height;

        if ((_resizeDirection & PanelResizeDirection.Left) != 0)
        {
            var maxWidth = Math.Max(minW, _resizeStartOrigin.X + _resizeStartSize.Width);
            width = Math.Clamp(_resizeStartSize.Width - deltaX, minW, maxWidth);
            left = _resizeStartOrigin.X + (_resizeStartSize.Width - width);
        }

        if ((_resizeDirection & PanelResizeDirection.Right) != 0)
        {
            var maxWidth = Math.Max(minW, canvasWidth - _resizeStartOrigin.X);
            width = Math.Clamp(_resizeStartSize.Width + deltaX, minW, maxWidth);
        }

        if ((_resizeDirection & PanelResizeDirection.Top) != 0)
        {
            var maxHeight = Math.Max(minH, _resizeStartOrigin.Y - minTop + _resizeStartSize.Height);
            height = Math.Clamp(_resizeStartSize.Height - deltaY, minH, maxHeight);
            top = _resizeStartOrigin.Y + (_resizeStartSize.Height - height);
        }

        if ((_resizeDirection & PanelResizeDirection.Bottom) != 0)
        {
            var maxHeight = Math.Max(minH, canvasHeight - _resizeStartOrigin.Y);
            height = Math.Clamp(_resizeStartSize.Height + deltaY, minH, maxHeight);
        }

        _resizePanel.Width = width;
        _resizePanel.Height = height;
        Canvas.SetLeft(_resizePanel, left);
        Canvas.SetTop(_resizePanel, top);
        UpdateResizeHandleFor(_resizePanel);
    }

    private static PanelResizeDirection GetResizeDirection(Border panel, Point point)
    {
        var width = panel.Bounds.Width > 0 ? panel.Bounds.Width : panel.Width;
        var height = panel.Bounds.Height > 0 ? panel.Bounds.Height : panel.Height;
        if (width <= 0 || height <= 0)
            return PanelResizeDirection.None;

        var direction = PanelResizeDirection.None;
        if (point.X <= ResizeBorderThickness)
            direction |= PanelResizeDirection.Left;
        else if (point.X >= width - ResizeBorderThickness)
            direction |= PanelResizeDirection.Right;

        if (point.Y <= ResizeBorderThickness)
            direction |= PanelResizeDirection.Top;
        else if (point.Y >= height - ResizeBorderThickness)
            direction |= PanelResizeDirection.Bottom;

        return direction;
    }

    private static Cursor? GetResizeCursor(PanelResizeDirection direction)
    {
        return direction switch
        {
            PanelResizeDirection.Left => new Cursor(StandardCursorType.LeftSide),
            PanelResizeDirection.Right => new Cursor(StandardCursorType.RightSide),
            PanelResizeDirection.Top => new Cursor(StandardCursorType.TopSide),
            PanelResizeDirection.Bottom => new Cursor(StandardCursorType.BottomSide),
            PanelResizeDirection.Left | PanelResizeDirection.Top => new Cursor(StandardCursorType.TopLeftCorner),
            PanelResizeDirection.Right | PanelResizeDirection.Top => new Cursor(StandardCursorType.TopRightCorner),
            PanelResizeDirection.Left | PanelResizeDirection.Bottom => new Cursor(StandardCursorType.BottomLeftCorner),
            PanelResizeDirection.Right | PanelResizeDirection.Bottom => new Cursor(StandardCursorType.BottomRightCorner),
            _ => new Cursor(StandardCursorType.Arrow)
        };
    }

    private void UpdatePanelCursor(Border panel, Point point)
    {
        if (_dragPanel is not null || (_resizePanel is not null && !ReferenceEquals(_resizePanel, panel)))
            return;

        panel.Cursor = GetResizeCursor(GetResizeDirection(panel, point));
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

    private void HideView2DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        View2DPanel.IsVisible = false;
        View2DResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideView2DFactPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        View2DFactPanel.IsVisible = false;
        View2DFactResizeHandle.IsVisible = false;
        PersistPanelsLayout();
    }

    private void HideView2DPairPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        View2DPairPanel.IsVisible = false;
        View2DPairResizeHandle.IsVisible = false;
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
    private void ShowView2DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(View2DPanel, View2DResizeHandle);
    private void ShowView2DFactPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(View2DFactPanel, View2DFactResizeHandle);
    private void ShowView2DPairPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(View2DPairPanel, View2DPairResizeHandle);
    private void ShowView3DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePanel(View3DPanel, View3DResizeHandle);

    private void TogglePanel(Border panel, Border handle)
    {
        if (!IsPanelAllowed(panel))
        {
            panel.IsVisible = false;
            handle.IsVisible = false;
            PersistPanelsLayout();
            return;
        }

        panel.IsVisible = !panel.IsVisible;
        handle.IsVisible = panel.IsVisible;
        if (panel.IsVisible)
        {
            BringPanelToFront(panel);
            UpdateResizeHandleFor(panel);
            if (ReferenceEquals(panel, View2DPanel))
            {
                // Always start 2D panel from a stable frame when it is opened.
                View2DPlot.ResetZoom();
                UpdateView2DZoomText();
            }
            else if (ReferenceEquals(panel, View2DFactPanel))
            {
                View2DFactPlot.ResetZoom();
                UpdateView2DFactZoomText();
            }
            else if (ReferenceEquals(panel, View2DPairPanel))
            {
                View2DPairPlot.ResetZoom();
                UpdateView2DPairZoomText();
            }
        }

        PersistPanelsLayout();
    }

    private void ZoomInPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RecipePlot.ZoomIn();
    private void ZoomOutPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RecipePlot.ZoomOut();
    private void FitPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RecipePlot.ResetZoom();

    private void ZoomInTopView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TopViewPlot.ZoomIn();
    private void ZoomOutTopView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TopViewPlot.ZoomOut();
    private void FitTopView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TopViewPlot.ResetZoom();
    private void ZoomIn2DView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPlot.ZoomIn();
    private void ZoomOut2DView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPlot.ZoomOut();
    private void Fit2DView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPlot.ResetZoom();
    private void ZoomIn2DFactView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DFactPlot.ZoomIn();
    private void ZoomOut2DFactView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DFactPlot.ZoomOut();
    private void Fit2DFactView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DFactPlot.ResetZoom();
    private void ZoomIn2DPairView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPairPlot.ZoomIn();
    private void ZoomOut2DPairView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPairPlot.ZoomOut();
    private void Fit2DPairView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => View2DPairPlot.ResetZoom();
    private void Open2DCalibration_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        if (_calibrationDialog is not null)
        {
            _calibrationDialog.Activate();
            return;
        }

        var dialog = new SimulationCalibrationDialog(
            View2DPlot,
            saveDefaults: Persist2DCalibrationDefaults,
            resetCalibration: () => Reset2DCalibration_Click(null, new RoutedEventArgs()),
            autoCalibration: AutoAlign2DCalibrationAsync);

        _calibrationDialog = dialog;
        dialog.Closed += OnCalibrationDialogClosed;
        dialog.Show(owner);
        dialog.Activate();
    }

    private void Reset2DCalibration_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var (manipulatorAnchorX, manipulatorAnchorY) = ResolveDefaultManipulatorAnchors();

        View2DPlot.NozzleAnchorX = Controls.SimulationBlueprint2DControl.DefaultNozzleAnchorX;
        View2DPlot.NozzleAnchorY = Controls.SimulationBlueprint2DControl.DefaultNozzleAnchorY;
        View2DPlot.ManipulatorAnchorX = manipulatorAnchorX;
        View2DPlot.ManipulatorAnchorY = manipulatorAnchorY;
        View2DPlot.ReferenceHeightMm = Controls.SimulationBlueprint2DControl.DefaultReferenceHeightMm;
        View2DPlot.VerticalOffsetMm = Controls.SimulationBlueprint2DControl.DefaultVerticalOffsetMm;
        View2DPlot.HorizontalOffsetMm = Controls.SimulationBlueprint2DControl.DefaultHorizontalOffsetMm;
        View2DPlot.PartWidthScalePercent = Controls.SimulationBlueprint2DControl.DefaultPartWidthScalePercent;
        View2DPlot.ReversePath = false;
        View2DPlot.ResetZoom();
        View2DFactPlot.ResetZoom();
        View2DPairPlot.ResetZoom();
        UpdateView2DZoomText();
        UpdateView2DFactZoomText();
        UpdateView2DPairZoomText();
    }

    private void OnCalibrationDialogClosed(object? sender, EventArgs e)
    {
        if (sender is SimulationCalibrationDialog dialog)
            dialog.Closed -= OnCalibrationDialogClosed;

        _calibrationDialog = null;
    }

    private void CloseCalibrationDialog()
    {
        if (_calibrationDialog is null)
            return;

        _calibrationDialog.Closed -= OnCalibrationDialogClosed;
        _calibrationDialog.Close();
        _calibrationDialog = null;
    }

    private (double X, double Y) ResolveDefaultManipulatorAnchors()
    {
        var spriteVersion = _vm?.AppSettings.SimulationPanels?.SpriteVersion;
        return (
            SimulationSpriteAnchors.GetManipulatorPivotAnchorX(spriteVersion),
            SimulationSpriteAnchors.GetManipulatorPivotAnchorY(spriteVersion));
    }

    private async void AutoAlign2DCalibration_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await AutoAlign2DCalibrationAsync();

    private async Task AutoAlign2DCalibrationAsync()
    {
        await View2DPlot.AutoAlignCalibrationAsync();
        View2DPlot.ResetZoom();
        UpdateView2DZoomText();
    }

    private void Save2DCalibration_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Persist2DCalibrationDefaults();
    }

    private void UpdateZoomText()
    {
        ZoomText.Text = $"x{RecipePlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void UpdateTopViewZoomText()
    {
        TopViewZoomText.Text = $"x{TopViewPlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void UpdateView2DZoomText()
    {
        View2DZoomText.Text = $"x{View2DPlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void UpdateView2DFactZoomText()
    {
        View2DFactZoomText.Text = $"x{View2DFactPlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void UpdateView2DPairZoomText()
    {
        View2DPairZoomText.Text = $"x{View2DPairPlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void TogglePlotLegend_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        _vm.ShowLegend = !_vm.ShowLegend;
        UpdatePlotOverlayButtons();
    }

    private void TogglePlotPairLinks_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        _vm.ShowPairLinks = !_vm.ShowPairLinks;
        UpdatePlotOverlayButtons();
    }

    private void UpdatePlotOverlayButtons()
    {
        if (_vm is null)
            return;

        SimulationLegendToggleButton.Content = _vm.ShowLegend ? "Пояснения: вкл" : "Пояснения: выкл";
        SimulationLinksToggleButton.Content = _vm.ShowPairLinks ? "Связи точек: вкл" : "Связи точек: выкл";
    }

    private void ApplySavedView2DPairOverlaySettings()
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var enabled = _vm.AppSettings.SimulationPanels.View2DPairShowRedLink;
        View2DPairPlot.ShowPairLink = enabled;
        View2DPairLinkToggleButton.Content = enabled ? "Линия: вкл" : "Линия: выкл";
    }

    private void ToggleView2DPairLink_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        _vm.AppSettings.SimulationPanels.View2DPairShowRedLink = !_vm.AppSettings.SimulationPanels.View2DPairShowRedLink;
        ApplySavedView2DPairOverlaySettings();
        _vm.SaveAppSettings();
    }

    private void ApplySavedTargetDisplayModes()
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        var mirrored = panels.TargetViewMirrored;

        _applyingTargetDisplayModes = true;
        ApplyTargetViewOrientation(mirrored);
        ApplyTargetDisplayMode(RecipePlot, PlotTargetSideButton, PlotTargetCoverageButton, panels.PlotTargetDisplayMode, mirrored);
        ApplyTargetDisplayMode(View2DPairPlot, View2DPairTargetSideButton, View2DPairTargetCoverageButton, panels.View2DPairTargetDisplayMode, mirrored);
        _applyingTargetDisplayModes = false;
    }

    private void ApplyTargetViewOrientation(bool mirrored)
    {
        RecipePlot.InvertHorizontal = mirrored;
        View2DPairPlot.InvertHorizontal = mirrored;
    }

    private static void SetTargetViewOrientation(SimulationPanelsSettings panels, bool mirrored)
    {
        panels.TargetViewMirrored = mirrored;
        var side = mirrored ? SimulationTargetDisplayModes.Mirrored : SimulationTargetDisplayModes.Original;
        panels.PlotTargetDisplaySide = side;
        panels.View2DPairTargetDisplaySide = side;
    }

    private static string NormalizeCoverageMode(string? mode)
        => SimulationTargetDisplayModes.NormalizeCoverage(mode);

    private void ApplyTargetDisplayMode(RecipePlotControl control, Button sideButton, Button coverageButton, string? mode, bool mirrored)
    {
        var normalizedMode = NormalizeCoverageMode(mode);
        control.TargetDisplayMode = normalizedMode;
        UpdateTargetDisplayButtons(sideButton, coverageButton, mirrored, normalizedMode == SimulationTargetDisplayModes.Full);
    }

    private void ApplyTargetDisplayMode(SimulationPointPair2DControl control, Button sideButton, Button coverageButton, string? mode, bool mirrored)
    {
        var normalizedMode = NormalizeCoverageMode(mode);
        control.TargetDisplayMode = normalizedMode;
        UpdateTargetDisplayButtons(sideButton, coverageButton, mirrored, normalizedMode == SimulationTargetDisplayModes.Full);
    }

    private static void UpdateTargetDisplayButtons(Button sideButton, Button coverageButton, bool mirrored, bool full)
    {
        var side = mirrored ? SimulationTargetDisplayModes.Mirrored : SimulationTargetDisplayModes.Original;
        sideButton.Content = side == SimulationTargetDisplayModes.Mirrored ? "Зеркальная" : "Исходная";
        coverageButton.Content = full ? "Полная" : "Частичная";
    }

    private static string BuildTargetDisplayMode(bool full)
        => full ? SimulationTargetDisplayModes.Full : SimulationTargetDisplayModes.Original;

    private void PlotTargetSideButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        SetTargetViewOrientation(panels, !panels.TargetViewMirrored);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void PlotTargetCoverageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        var full = NormalizeCoverageMode(panels.PlotTargetDisplayMode) != SimulationTargetDisplayModes.Full;
        panels.PlotTargetDisplayMode = BuildTargetDisplayMode(full);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void View2DPairTargetSideButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        SetTargetViewOrientation(panels, !panels.TargetViewMirrored);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void View2DPairTargetCoverageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        var full = NormalizeCoverageMode(panels.View2DPairTargetDisplayMode) != SimulationTargetDisplayModes.Full;
        panels.View2DPairTargetDisplayMode = BuildTargetDisplayMode(full);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var access = GetPanelsAccess();

        ApplyPrimaryDefaultPanelSizes();
        TopViewPanel.Width = 560;
        TopViewPanel.Height = 320;
        View2DPanel.Width = 740;
        View2DPanel.Height = 470;
        View2DFactPanel.Width = 700;
        View2DFactPanel.Height = 420;
        View3DPanel.Width = 1160;
        View3DPanel.Height = 640;

        PlotPanel.IsVisible = access.Plot;
        TelemetryPanel.IsVisible = access.Telemetry;
        TopViewPanel.IsVisible = false;
        View2DPanel.IsVisible = false;
        View2DFactPanel.IsVisible = false;
        View2DPairPanel.IsVisible = access.View2DPair;
        View3DPanel.IsVisible = false;

        ApplyPanelsAccessRestrictions();

        RecipePlot.ResetZoom();
        TopViewPlot.ResetZoom();
        View2DPlot.ResetZoom();
        View2DFactPlot.ResetZoom();
        View2DPairPlot.ResetZoom();

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
        _vm.AppSettings.SimulationPanels.View2D = ToLayout(View2DPanel, _vm.AppSettings.SimulationPanels.View2D);
        _vm.AppSettings.SimulationPanels.View2DFact = ToLayout(View2DFactPanel, _vm.AppSettings.SimulationPanels.View2DFact);
        _vm.AppSettings.SimulationPanels.View2DPair = ToLayout(View2DPairPanel, _vm.AppSettings.SimulationPanels.View2DPair);
        _vm.AppSettings.SimulationPanels.View3D = ToLayout(View3DPanel, _vm.AppSettings.SimulationPanels.View3D);
        _vm.SaveAppSettings();
    }

    private void ApplySaved2DCalibration()
    {
        if (_calibrationLoaded || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var calibration = _vm.AppSettings.SimulationPanels.Calibration2D ??= new Simulation2DCalibrationSettings();

        View2DPlot.ReferenceHeightMm = calibration.ReferenceHeightMm;
        View2DPlot.ManipulatorAnchorX = calibration.ManipulatorAnchorX;
        View2DPlot.ManipulatorAnchorY = calibration.ManipulatorAnchorY;
        View2DPlot.VerticalOffsetMm = calibration.VerticalOffsetMm;
        View2DPlot.HorizontalOffsetMm = calibration.HorizontalOffsetMm;
        View2DPlot.PartWidthScalePercent = calibration.PartWidthScalePercent;
        View2DPlot.ReversePath = calibration.ReversePath;
        _calibrationLoaded = true;
    }

    private void Persist2DCalibrationDefaults()
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var calibration = _vm.AppSettings.SimulationPanels.Calibration2D ??= new Simulation2DCalibrationSettings();
        calibration.ReferenceHeightMm = View2DPlot.ReferenceHeightMm;
        calibration.ManipulatorAnchorX = View2DPlot.ManipulatorAnchorX;
        calibration.ManipulatorAnchorY = View2DPlot.ManipulatorAnchorY;
        calibration.VerticalOffsetMm = View2DPlot.VerticalOffsetMm;
        calibration.HorizontalOffsetMm = View2DPlot.HorizontalOffsetMm;
        calibration.PartWidthScalePercent = View2DPlot.PartWidthScalePercent;
        calibration.ReversePath = View2DPlot.ReversePath;
        _vm.SaveAppSettings();
    }

    private static PanelPlacementSettings ToLayout(Border panel, PanelPlacementSettings previous)
    {
        var width = IsFinite(panel.Width) && panel.Width > 0 ? panel.Width : panel.Bounds.Width;
        var height = IsFinite(panel.Height) && panel.Height > 0 ? panel.Height : panel.Bounds.Height;

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
        var maxTop = Math.Max(TopUiReserve, GetCanvasHeight() - panel.Height);

        var left = Canvas.GetLeft(panel);
        var top = Canvas.GetTop(panel);

        Canvas.SetLeft(panel, Math.Clamp(IsFinite(left) ? left : 0, 0, maxLeft));
        Canvas.SetTop(panel, Math.Clamp(IsFinite(top) ? top : TopUiReserve, TopUiReserve, maxTop));
    }

    private void UpdateResizeHandlePositions()
    {
        UpdateResizeHandleFor(PlotPanel);
        UpdateResizeHandleFor(TelemetryPanel);
        UpdateResizeHandleFor(TopViewPanel);
        UpdateResizeHandleFor(View2DPanel);
        UpdateResizeHandleFor(View2DFactPanel);
        UpdateResizeHandleFor(View2DPairPanel);
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
                    : panel == View2DPanel
                        ? View2DResizeHandle
                        : panel == View2DFactPanel
                            ? View2DFactResizeHandle
                            : panel == View2DPairPanel
                                ? View2DPairResizeHandle
                                : View3DResizeHandle;

        if (!panel.IsVisible)
        {
            handle.IsVisible = false;
            return;
        }

        var width = IsFinite(panel.Width) && panel.Width > 0 ? panel.Width : panel.Bounds.Width;
        var height = IsFinite(panel.Height) && panel.Height > 0 ? panel.Height : panel.Bounds.Height;

        handle.IsVisible = true;
        handle.ZIndex = panel.ZIndex + 1;
        var safeWidth = width > 0 ? width : panel.MinWidth;
        var safeHeight = height > 0 ? height : panel.MinHeight;
        Canvas.SetLeft(handle, Canvas.GetLeft(panel) + safeWidth - handle.Width / 2);
        Canvas.SetTop(handle, Canvas.GetTop(panel) + safeHeight - handle.Height / 2);
    }
}
