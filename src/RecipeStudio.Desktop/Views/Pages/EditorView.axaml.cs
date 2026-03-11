using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RecipeStudio.Desktop.Controls;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;
using System.Threading.Tasks;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class EditorView : UserControl
{
    private const double PanelMargin = 20;
    private const double DefaultPanelsTopOffset = 42;
    private const double DefaultPanelGap = 16;
    private const double ResizeBorderThickness = 8;

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
    private Point _resizeStartOrigin;
    private Size _resizeStartSize;
    private PanelResizeDirection _resizeDirection;
    private int _zOrderCounter;
    private bool _isApplyingSavedColumnWidths;
    private bool _applyingTargetDisplayModes;

    [Flags]
    private enum PanelResizeDirection
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    private void HookVm()
    {
        if (ReferenceEquals(_vm, DataContext))
            return;

        if (_vm is not null)
        {
            _vm.RequestImportExcel -= OnRequestImportExcel;
            _vm.RequestExportExcel -= OnRequestExportExcel;
            _vm.RequestShowCharts -= OnRequestShowCharts;
            _vm.SaveCompleted -= OnSaveCompleted;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as EditorViewModel;
        if (_vm is not null)
        {
            _vm.RequestImportExcel += OnRequestImportExcel;
            _vm.RequestExportExcel += OnRequestExportExcel;
            _vm.RequestShowCharts += OnRequestShowCharts;
            _vm.SaveCompleted += OnSaveCompleted;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        if (VisualRoot is not null)
        {
            ApplySavedTargetDisplayModes();
            ApplySavedPair2DOverlaySettings();
        }
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        HookVm();
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        PanelsCanvas.SizeChanged += OnPanelsCanvasSizeChanged;

        if (HasUsableCanvasSize())
            InitializePanelsLayout();

        InitializePanelZOrder();

        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        RecipePlot.ZoomChanged += OnRecipePlotZoomChanged;
        Pair2DPlot.ZoomChanged -= OnPair2DPlotZoomChanged;
        Pair2DPlot.ZoomChanged += OnPair2DPlotZoomChanged;
        PointsGrid.PointerReleased -= OnPointsGridPointerReleased;
        PointsGrid.PointerReleased += OnPointsGridPointerReleased;
        PointsGrid.AddHandler(InputElement.PointerPressedEvent, OnPointsGridHeaderPointerPressed, RoutingStrategies.Tunnel, true);

        ApplySavedTargetDisplayModes();
        ApplySavedPair2DOverlaySettings();
        UpdateZoomText();
        UpdatePair2DZoomText();
        UpdatePlotOverlayButtons();
        Dispatcher.UIThread.Post(ApplySavedGridColumnWidths, DispatcherPriority.Loaded);
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        PanelsCanvas.SizeChanged -= OnPanelsCanvasSizeChanged;
        RecipePlot.ZoomChanged -= OnRecipePlotZoomChanged;
        Pair2DPlot.ZoomChanged -= OnPair2DPlotZoomChanged;
        PointsGrid.PointerReleased -= OnPointsGridPointerReleased;
        PointsGrid.RemoveHandler(InputElement.PointerPressedEvent, OnPointsGridHeaderPointerPressed);
        PersistGridColumnWidths(force: true);
        PersistPanelsLayout(force: false);
        _panelsInitialized = false;
    }


    private void OnRecipePlotZoomChanged(double _)
    {
        UpdateZoomText();
        UpdatePlotOverlayButtons();
    }

    private void OnPair2DPlotZoomChanged(double _) => UpdatePair2DZoomText();

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
        ClampPanelToCanvas(Pair2DPanel);
        ClampPanelToCanvas(SelectedPointPanel);
        UpdateResizeHandlePositions();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.PropertyName == nameof(EditorViewModel.HasDocument) && !_vm.HasDocument)
        {
            PersistGridColumnWidths(force: true);
            PersistPanelsLayout(force: false);
        }
    }

    private void OnPointsGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PersistGridColumnWidths(force: false);
    }

    private void OnPointsGridHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || e.Handled)
            return;

        if (!e.GetCurrentPoint(PointsGrid).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is not Visual source)
            return;

        var header = source as DataGridColumnHeader ?? source.FindAncestorOfType<DataGridColumnHeader>();
        if (header?.Content is not Control { Tag: string tag } content)
            return;

        if (tag is "Act" or "Top" or "Hidden")
        {
            BulkFlagHeader_PointerPressed(content, e);
            return;
        }

        if (tag == "ApplyRecommendedIceRate")
        {
            BulkRecommendedFlowHeader_PointerPressed(content, e);
            return;
        }

        if (tag is "ANozzle" or "Betta" or "SpeedTable" or "TimeSec" or "AirPressure" or "AirTemp")
            BulkNumericHeader_PointerPressed(content, e);
    }

    private async void BulkFlagHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (sender is not Control { Tag: string flagKey })
            return;

        e.Handled = true;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var dialog = new BulkFlagActionDialog(
            "Массовое изменение",
            $"Выставить для всех точек одинаковое значение?\nПоле: {GetBulkFlagColumnName(flagKey)}");

        var action = await dialog.ShowDialog<BulkFlagAction>(owner);
        switch (action)
        {
            case BulkFlagAction.SetAll:
                ApplyBulkFlag(flagKey, value: true);
                break;
            case BulkFlagAction.ClearAll:
                ApplyBulkFlag(flagKey, value: false);
                break;
        }
    }

    private async void BulkNumericHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (sender is not Control { Tag: string fieldKey })
            return;

        e.Handled = true;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var options = GetBulkNumericOptions(fieldKey);
        var dialog = new BulkNumericValueDialog(
            "Массовое изменение",
            BuildBulkNumericMessage(fieldKey),
            options.InitialValue,
            options.Minimum,
            options.Maximum,
            options.Increment,
            options.FormatString);

        var value = await dialog.ShowDialog<double?>(owner);
        if (value is null)
            return;

        ApplyBulkNumericField(fieldKey, value.Value);
    }

    private async void BulkRecommendedFlowHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        e.Handled = true;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var dialog = new BulkFlagActionDialog(
            "Массовое изменение",
            "Применить рекомендованный расход?",
            "Да",
            "Отмена",
            showClearAllButton: false);

        var action = await dialog.ShowDialog<BulkFlagAction>(owner);
        if (action == BulkFlagAction.SetAll)
            _vm.ApplyRecommendedIceRateForAll();
    }

    private void ApplySavedGridColumnWidths()
    {
        if (_vm is null || PointsGrid.Columns.Count == 0 || _isApplyingSavedColumnWidths)
            return;

        var savedNamed = _vm.AppSettings.EditorGridColumns;
        var savedLegacy = _vm.AppSettings.EditorGridColumnWidths;
        var migratedFromNamed = false;
        var migratedFromLegacy = false;

        _isApplyingSavedColumnWidths = true;
        try
        {
            if (savedNamed is { Count: > 0 })
            {
                var byName = savedNamed
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name) && IsFinite(x.Width) && x.Width > 0)
                    .GroupBy(x => x.Name, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last().Width, StringComparer.Ordinal);

                // Migrate the old narrow C column width to match Top.
                if (byName.TryGetValue("C", out var cWidth) &&
                    cWidth <= 25.5 &&
                    byName.TryGetValue("Top", out var topWidth) &&
                    topWidth > 0)
                {
                    byName["C"] = topWidth;
                    migratedFromNamed = true;
                }

                for (var i = 0; i < PointsGrid.Columns.Count; i++)
                {
                    var key = GetColumnName(PointsGrid.Columns[i], i);
                    if (!byName.TryGetValue(key, out var width))
                        continue;

                    PointsGrid.Columns[i].Width = new DataGridLength(width);
                }
            }
            else if (savedLegacy is { Count: > 0 })
            {
                migratedFromLegacy = true;
                var count = Math.Min(savedLegacy.Count, PointsGrid.Columns.Count);
                for (var i = 0; i < count; i++)
                {
                    var width = savedLegacy[i];
                    if (!IsFinite(width) || width <= 0)
                        continue;

                    PointsGrid.Columns[i].Width = new DataGridLength(width);
                }
            }
        }
        finally
        {
            _isApplyingSavedColumnWidths = false;
        }

        if (migratedFromNamed)
            PersistGridColumnWidths(force: true);

        if (migratedFromLegacy)
            PersistGridColumnWidths(force: true);
    }

    private void PersistGridColumnWidths(bool force)
    {
        if (_vm is null || PointsGrid.Columns.Count == 0 || _isApplyingSavedColumnWidths)
            return;

        var current = new List<EditorGridColumnWidthSettings>(PointsGrid.Columns.Count);
        for (var i = 0; i < PointsGrid.Columns.Count; i++)
        {
            var width = Math.Round(PointsGrid.Columns[i].ActualWidth, 2);
            if (!IsFinite(width) || width <= 0)
                return;

            current.Add(new EditorGridColumnWidthSettings
            {
                Name = GetColumnName(PointsGrid.Columns[i], i),
                Width = width
            });
        }

        var saved = _vm.AppSettings.EditorGridColumns;
        if (!force && AreColumnWidthsEqual(saved, current))
            return;

        _vm.AppSettings.EditorGridColumns = current;
        _vm.AppSettings.EditorGridColumnWidths = null;
        _vm.SaveAppSettings();
    }

    private static bool AreColumnWidthsEqual(IReadOnlyList<EditorGridColumnWidthSettings>? left, IReadOnlyList<EditorGridColumnWidthSettings> right)
    {
        if (left is null || left.Count != right.Count)
            return false;

        for (var i = 0; i < right.Count; i++)
        {
            if (!string.Equals(left[i].Name, right[i].Name, StringComparison.Ordinal))
                return false;

            if (Math.Abs(left[i].Width - right[i].Width) > 0.5)
                return false;
        }

        return true;
    }

    private static string GetColumnName(DataGridColumn column, int index)
    {
        var headerText = column.Header switch
        {
            TextBlock textBlock => textBlock.Text,
            Decorator { Child: TextBlock textBlock } => textBlock.Text,
            string text => text,
            _ => column.Header?.ToString()
        };

        return string.IsNullOrWhiteSpace(headerText) ? $"Column-{index + 1}" : headerText.Trim();
    }

    private static string GetBulkFlagColumnName(string flagKey)
    {
        return flagKey switch
        {
            "Act" => "A",
            "Top" => "Top",
            "Hidden" => "Micro",
            _ => flagKey
        };
    }

    private string BuildBulkNumericMessage(string fieldKey)
    {
        var message = $"Выставить для всех точек одинаковое значение?\nПоле: {GetBulkNumericColumnName(fieldKey)}";
        return fieldKey == "TimeSec"
            ? message + "\nЗначение задается в секундах, Speed будет пересчитан автоматически."
            : message;
    }

    private (double InitialValue, double Minimum, double Maximum, double Increment, string FormatString) GetBulkNumericOptions(string fieldKey)
    {
        var source = _vm?.SelectedPoint ?? _vm?.Points.FirstOrDefault();
        var initialValue = source is null
            ? 0
            : fieldKey switch
            {
                "ANozzle" => source.ANozzle,
                "Betta" => source.Betta,
                "SpeedTable" => source.SpeedTable,
                "TimeSec" => source.TimeSec,
                "AirPressure" => source.AirPressure,
                "AirTemp" => source.AirTemp,
                _ => 0
            };

        return fieldKey switch
        {
            "Betta" => (initialValue, -50000d, 50000d, 0.1d, "0.###"),
            "AirTemp" => (initialValue, -50000d, 50000d, 0.1d, "0.###"),
            _ => (Math.Max(0, initialValue), 0d, 50000d, 0.1d, "0.###")
        };
    }

    private static string GetBulkNumericColumnName(string fieldKey)
    {
        return fieldKey switch
        {
            "ANozzle" => "a",
            "Betta" => "β",
            "SpeedTable" => "Speed",
            "TimeSec" => "Time",
            "AirPressure" => "Air.P",
            "AirTemp" => "Air.T",
            _ => fieldKey
        };
    }

    private void ApplyBulkFlag(string flagKey, bool value)
    {
        if (_vm is null)
            return;

        switch (flagKey)
        {
            case "Act":
                _vm.SetActForAll(value);
                break;
            case "Top":
                _vm.SetTopForAll(value);
                break;
            case "Hidden":
                _vm.SetHiddenForAll(value);
                break;
        }
    }

    private void ApplyBulkNumericField(string fieldKey, double value)
    {
        if (_vm is null)
            return;

        switch (fieldKey)
        {
            case "ANozzle":
                _vm.SetANozzleForAll(value);
                break;
            case "Betta":
                _vm.SetBettaForAll(value);
                break;
            case "SpeedTable":
                _vm.SetSpeedTableForAll(value);
                break;
            case "TimeSec":
                _vm.SetTimeForAll(value);
                break;
            case "AirPressure":
                _vm.SetAirPressureForAll(value);
                break;
            case "AirTemp":
                _vm.SetAirTempForAll(value);
                break;
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

    private async void ShowCncInstruction_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.Document is null)
            return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        if (_vm.Document.Points.Count == 0)
        {
            var info = new InfoDialog(
                "\u0427\u041f\u0423-\u0438\u043d\u0441\u0442\u0440\u0443\u043a\u0446\u0438\u044f",
                "\u0412 \u0440\u0435\u0446\u0435\u043f\u0442\u0435 \u043d\u0435\u0442 \u0442\u043e\u0447\u0435\u043a \u0434\u043b\u044f \u043e\u0442\u043e\u0431\u0440\u0430\u0436\u0435\u043d\u0438\u044f.",
                "OK");
            await info.ShowDialog(owner);
            return;
        }

        var dialog = new CncInstructionDialog(_vm.Document, _vm.AppSettings, ExportCurrentRecipeAsync);
        await dialog.ShowDialog(owner);
    }

    private async void OnRequestImportExcel()
    {
        var importOwner = TopLevel.GetTopLevel(this) as Window;
        if (importOwner is null)
            return;

        await ImportCurrentRecipeAsync(importOwner);
        return;

#if false
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
#endif
    }

    private async void OnRequestExportExcel()
    {
        var exportOwner = TopLevel.GetTopLevel(this) as Window;
        if (exportOwner is null)
            return;

        await ExportCurrentRecipeAsync(exportOwner);
        return;

#if false
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

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        try
        {
            _vm.ExportToExcel(path);

            var dialog = new InfoDialog(
                "Экспорт Excel",
                $"Файл Excel успешно сохранен.\n{path}",
                "OK");
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            var dialog = new InfoDialog(
                "Ошибка экспорта",
                $"Не удалось экспортировать файл Excel.\n{ex.Message}",
                "OK");
            await dialog.ShowDialog(owner);
        }
#endif
    }

    private async void OnSaveCompleted(bool success, string title, string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var dialog = new InfoDialog(title, message, success ? "OK" : "Закрыть");
        await dialog.ShowDialog(owner);
    }

    private async Task<bool> ImportCurrentRecipeAsync(Window owner)
    {
        if (_vm is null)
            return false;

        var sp = owner.StorageProvider;
        if (sp is null)
            return false;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "РРјРїРѕСЂС‚ С‚РѕС‡РµРє РёР· Excel/CSV",
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
        if (file is null)
            return false;

        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var preview = _vm.PreviewImport(path);
        var dialog = new ImportRecipeDialog(preview, allowRename: false, title: "РРјРїРѕСЂС‚ С‚РѕС‡РµРє РІ С‚РµРєСѓС‰РёР№ СЂРµС†РµРїС‚");
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
            return false;

        return _vm.ApplyImportedPreview(preview);
    }

    private async Task ExportCurrentRecipeAsync(Window owner)
    {
        if (_vm is null)
            return;

        var sp = owner.StorageProvider;
        if (sp is null)
            return;

        var suggested = string.IsNullOrWhiteSpace(_vm.RecipeCode) ? "recipe" : _vm.RecipeCode;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "\u042d\u043a\u0441\u043f\u043e\u0440\u0442 \u0440\u0435\u0446\u0435\u043f\u0442\u0430 \u0432 Excel",
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

        if (file is null)
            return;

        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            _vm.ExportToExcel(path);

            var dialog = new InfoDialog(
                "\u042d\u043a\u0441\u043f\u043e\u0440\u0442 Excel",
                $"\u0424\u0430\u0439\u043b Excel \u0443\u0441\u043f\u0435\u0448\u043d\u043e \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d.\n{path}",
                "OK");
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            var dialog = new InfoDialog(
                "\u041e\u0448\u0438\u0431\u043a\u0430 \u044d\u043a\u0441\u043f\u043e\u0440\u0442\u0430",
                $"\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u044d\u043a\u0441\u043f\u043e\u0440\u0442\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u0444\u0430\u0439\u043b Excel.\n{ex.Message}",
                "OK");
            await dialog.ShowDialog(owner);
        }
    }

    private void InitializePanelsLayout()
    {
        if (_panelsInitialized)
            return;

        var saved = _vm?.AppSettings.EditorPanels;
        ApplyPanelLayout(ParametersPanel, saved?.Parameters, ParametersPanelDefaultPosition);
        ApplyPanelLayout(VisualizationPanel, saved?.Visualization, VisualizationPanelDefaultPosition);
        ApplyPanelLayout(Pair2DPanel, saved?.Pair2D, Pair2DPanelDefaultPosition);
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
        return GetDefaultPanelPositions().Parameters;
    }

    private Point VisualizationPanelDefaultPosition(Border panel)
    {
        return GetDefaultPanelPositions().Visualization;
    }

    private Point Pair2DPanelDefaultPosition(Border panel)
    {
        return GetDefaultPanelPositions().Pair2D;
    }

    private Point SelectedPointPanelDefaultPosition(Border panel)
    {
        return GetDefaultPanelPositions().SelectedPoint;
    }

    private (Point Parameters, Point Visualization, Point Pair2D, Point SelectedPoint) GetDefaultPanelPositions()
    {
        var canvasWidth = GetCanvasWidth();
        var top = Math.Max(PanelMargin, DefaultPanelsTopOffset);

        var pair2DLeft = PanelMargin;
        var pair2DTop = top;

        var visualizationLeft = pair2DLeft + Pair2DPanel.Width + DefaultPanelGap;
        var visualizationTop = top;

        var parametersLeft = Math.Max(PanelMargin, canvasWidth - ParametersPanel.Width - PanelMargin);
        var parametersTop = top;

        var selectedPointLeft = Math.Max(PanelMargin, canvasWidth - SelectedPointPanel.Width - PanelMargin);
        var selectedPointTop = parametersTop + ParametersPanel.Height + DefaultPanelGap;

        return (
            new Point(parametersLeft, parametersTop),
            new Point(visualizationLeft, visualizationTop),
            new Point(pair2DLeft, pair2DTop),
            new Point(selectedPointLeft, selectedPointTop));
    }

    private void ApplyDefaultPanelSizes()
    {
        if (!HasUsableCanvasSize())
        {
            ParametersPanel.Width = 390;
            ParametersPanel.Height = 210;
            VisualizationPanel.Width = 640;
            VisualizationPanel.Height = 680;
            Pair2DPanel.Width = 600;
            Pair2DPanel.Height = 680;
            SelectedPointPanel.Width = 600;
            SelectedPointPanel.Height = 620;
            return;
        }

        var canvasWidth = GetCanvasWidth();
        var canvasHeight = GetCanvasHeight();
        var top = Math.Max(PanelMargin, DefaultPanelsTopOffset);
        var availableMainHeight = Math.Max(Pair2DPanel.MinHeight, canvasHeight - top - PanelMargin);

        ParametersPanel.Width = ClampPanelDimension(canvasWidth * 0.2, ParametersPanel.MinWidth, 390);
        ParametersPanel.Height = ClampPanelDimension(canvasHeight * 0.2, ParametersPanel.MinHeight, 220);

        SelectedPointPanel.Width = ClampPanelDimension(canvasWidth * 0.32, SelectedPointPanel.MinWidth, 620);

        var selectedAvailableHeight = canvasHeight - top - ParametersPanel.Height - DefaultPanelGap - PanelMargin;
        SelectedPointPanel.Height = ClampPanelDimension(selectedAvailableHeight, SelectedPointPanel.MinHeight, 760);

        var mainAvailableWidth = Math.Max(
            Pair2DPanel.MinWidth + VisualizationPanel.MinWidth + DefaultPanelGap,
            canvasWidth - SelectedPointPanel.Width - PanelMargin * 2 - DefaultPanelGap);

        var pair2DWidth = ClampPanelDimension(mainAvailableWidth * 0.48, Pair2DPanel.MinWidth, 620);
        var visualizationWidth = Math.Max(VisualizationPanel.MinWidth, mainAvailableWidth - pair2DWidth - DefaultPanelGap);
        if (pair2DWidth + visualizationWidth + DefaultPanelGap > mainAvailableWidth)
            pair2DWidth = Math.Max(Pair2DPanel.MinWidth, mainAvailableWidth - VisualizationPanel.MinWidth - DefaultPanelGap);

        Pair2DPanel.Width = pair2DWidth;
        VisualizationPanel.Width = Math.Max(VisualizationPanel.MinWidth, mainAvailableWidth - Pair2DPanel.Width - DefaultPanelGap);

        Pair2DPanel.Height = availableMainHeight;
        VisualizationPanel.Height = availableMainHeight;
    }

    private static double ClampPanelDimension(double value, double min, double max)
    {
        var safeMin = min > 0 ? min : 0;
        var safeMax = Math.Max(safeMin, max);
        return Math.Clamp(value, safeMin, safeMax);
    }

    private void ApplyDefaultPanelsLayout()
    {
        ApplyDefaultPanelSizes();

        var parametersPos = ParametersPanelDefaultPosition(ParametersPanel);
        Canvas.SetLeft(ParametersPanel, parametersPos.X);
        Canvas.SetTop(ParametersPanel, parametersPos.Y);

        var visualizationPos = VisualizationPanelDefaultPosition(VisualizationPanel);
        Canvas.SetLeft(VisualizationPanel, visualizationPos.X);
        Canvas.SetTop(VisualizationPanel, visualizationPos.Y);

        var pair2DPos = Pair2DPanelDefaultPosition(Pair2DPanel);
        Canvas.SetLeft(Pair2DPanel, pair2DPos.X);
        Canvas.SetTop(Pair2DPanel, pair2DPos.Y);

        var selectedPointPos = SelectedPointPanelDefaultPosition(SelectedPointPanel);
        Canvas.SetLeft(SelectedPointPanel, selectedPointPos.X);
        Canvas.SetTop(SelectedPointPanel, selectedPointPos.Y);

        ClampPanelToCanvas(ParametersPanel);
        ClampPanelToCanvas(VisualizationPanel);
        ClampPanelToCanvas(Pair2DPanel);
        ClampPanelToCanvas(SelectedPointPanel);
    }


    private void BringPanelToFront(Border panel)
    {
        _zOrderCounter = Math.Max(_zOrderCounter + 1, 1);
        panel.ZIndex = _zOrderCounter * 10;
        UpdateResizeHandleFor(panel);
    }

    private void InitializePanelZOrder()
    {
        _zOrderCounter = 0;
        if (ParametersPanel.IsVisible)
            BringPanelToFront(ParametersPanel);
        if (VisualizationPanel.IsVisible)
            BringPanelToFront(VisualizationPanel);
        if (Pair2DPanel.IsVisible)
            BringPanelToFront(Pair2DPanel);
        if (SelectedPointPanel.IsVisible)
            BringPanelToFront(SelectedPointPanel);
    }

    private void Panel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Control source)
        {
            if (source is TextBox || source is CheckBox || source is Slider || source is Button || source is ToggleButton || source is ComboBox || source is NumericUpDown || source is ScrollViewer ||
                source.FindAncestorOfType<TextBox>() is not null ||
                source.FindAncestorOfType<CheckBox>() is not null ||
                source.FindAncestorOfType<Slider>() is not null ||
                source.FindAncestorOfType<Button>() is not null ||
                source.FindAncestorOfType<ToggleButton>() is not null ||
                source.FindAncestorOfType<ComboBox>() is not null ||
                source.FindAncestorOfType<NumericUpDown>() is not null ||
                source.FindAncestorOfType<ScrollViewer>() is not null)
                return;

            if (IsDescendantOf(source, RecipePlot) || IsDescendantOf(source, Pair2DPlot))
                return;
        }

        var control = sender as Control;
        while (control is not null && control is not Border)
        {
            control = control.Parent as Control;
        }

        if (control is not Border panel) return;

        var resizeDirection = GetResizeDirection(panel, e.GetPosition(panel));
        if (resizeDirection != PanelResizeDirection.None)
        {
            StartPanelResize(panel, resizeDirection, e, panel);
            return;
        }

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
        if (sender is Border panel)
        {
            if (ReferenceEquals(_resizePanel, panel))
            {
                ResizeActivePanel(e, 0);
                return;
            }

            UpdatePanelCursor(panel, e.GetPosition(panel));
        }

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
        if (_resizePanel is not null)
        {
            _resizePanel = null;
            _resizeDirection = PanelResizeDirection.None;
            e.Pointer.Capture(null);
            PersistPanelsLayout();
            return;
        }

        if (_dragPanel is null) return;

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
            Border handle when ReferenceEquals(handle, Pair2DResizeHandle) => Pair2DPanel,
            Border handle when ReferenceEquals(handle, SelectedPointResizeHandle) => SelectedPointPanel,
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

        ResizeActivePanel(e, 0);
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

    private void HidePair2DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Pair2DPanel.IsVisible = false;
        Pair2DResizeHandle.IsVisible = false;
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
        {
            BringPanelToFront(ParametersPanel);
            UpdateResizeHandleFor(ParametersPanel);
        }
        PersistPanelsLayout();
    }

    private void ShowVisualizationPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VisualizationPanel.IsVisible = !VisualizationPanel.IsVisible;
        VisualizationResizeHandle.IsVisible = VisualizationPanel.IsVisible;
        if (VisualizationPanel.IsVisible)
        {
            BringPanelToFront(VisualizationPanel);
            UpdateResizeHandleFor(VisualizationPanel);
        }
        PersistPanelsLayout();
    }

    private void ShowPair2DPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Pair2DPanel.IsVisible = !Pair2DPanel.IsVisible;
        Pair2DResizeHandle.IsVisible = Pair2DPanel.IsVisible;
        if (Pair2DPanel.IsVisible)
        {
            BringPanelToFront(Pair2DPanel);
            UpdateResizeHandleFor(Pair2DPanel);
        }
        PersistPanelsLayout();
    }

    private void ShowSelectedPointPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedPointPanel.IsVisible = !SelectedPointPanel.IsVisible;
        SelectedPointResizeHandle.IsVisible = SelectedPointPanel.IsVisible;
        if (SelectedPointPanel.IsVisible)
        {
            BringPanelToFront(SelectedPointPanel);
            UpdateResizeHandleFor(SelectedPointPanel);
        }
        PersistPanelsLayout();
    }

    private async void DeleteSelectedPoint_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm?.SelectedPoint is null)
            return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var point = _vm.SelectedPoint;
        var dialog = new ConfirmDialog(
            "Удаление точки",
            $"Удалить точку #{point.NPoint}?\nR={point.RCrd:0.###}, Z={point.ZCrd:0.###}",
            "Удалить",
            "Отмена");

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed || !_vm.RemovePointCommand.CanExecute(null))
            return;

        _vm.RemovePointCommand.Execute(null);
    }

    private void ZoomInPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ZoomIn();
        UpdateZoomText();
        UpdatePlotOverlayButtons();
    }

    private void ZoomOutPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ZoomOut();
        UpdateZoomText();
        UpdatePlotOverlayButtons();
    }

    private void FitPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ResetZoom();
        UpdateZoomText();
    }

    private void ZoomInPair2D_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Pair2DPlot.ZoomIn();
        UpdatePair2DZoomText();
    }

    private void ZoomOutPair2D_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Pair2DPlot.ZoomOut();
        UpdatePair2DZoomText();
    }

    private void FitPair2D_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Pair2DPlot.ResetZoom();
        UpdatePair2DZoomText();
    }

    private void UpdateZoomText()
    {
        ZoomText.Text = $"x{RecipePlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void UpdatePair2DZoomText()
    {
        Pair2DZoomText.Text = $"x{Pair2DPlot.ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void ApplySavedPair2DOverlaySettings()
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var enabled = _vm.AppSettings.SimulationPanels.View2DPairShowRedLink;
        Pair2DPlot.ShowPairLink = enabled;
        EditorPair2DLinkToggleButton.Content = enabled ? "Линия: вкл" : "Линия: выкл";
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
        ApplyTargetDisplayMode(RecipePlot, EditorPlotTargetSideButton, EditorPlotTargetCoverageButton, panels.PlotTargetDisplayMode, mirrored);
        ApplyTargetDisplayMode(Pair2DPlot, EditorPair2DTargetSideButton, EditorPair2DTargetCoverageButton, panels.View2DPairTargetDisplayMode, mirrored);
        _applyingTargetDisplayModes = false;
    }

    private void ApplyTargetViewOrientation(bool mirrored)
    {
        RecipePlot.InvertHorizontal = mirrored;
        Pair2DPlot.InvertHorizontal = mirrored;
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
        sideButton.Content = mirrored ? "Зеркальная" : "Исходная";
        coverageButton.Content = full ? "Полная" : "Частичная";
    }

    private static string BuildTargetDisplayMode(bool full)
        => full ? SimulationTargetDisplayModes.Full : SimulationTargetDisplayModes.Original;

    private void EditorPlotTargetSideButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        SetTargetViewOrientation(panels, !panels.TargetViewMirrored);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void EditorPlotTargetCoverageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private void EditorPair2DTargetSideButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_applyingTargetDisplayModes || _vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        var panels = _vm.AppSettings.SimulationPanels;
        SetTargetViewOrientation(panels, !panels.TargetViewMirrored);
        ApplySavedTargetDisplayModes();
        _vm.SaveAppSettings();
    }

    private void EditorPair2DTargetCoverageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private void ToggleLegend_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ShowLegend = !RecipePlot.ShowLegend;
        UpdatePlotOverlayButtons();
    }

    private void TogglePairLinks_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RecipePlot.ShowPairLinks = !RecipePlot.ShowPairLinks;
        UpdatePlotOverlayButtons();
    }

    private void ToggleEditorPair2DLink_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        _vm.AppSettings.SimulationPanels ??= new SimulationPanelsSettings();
        _vm.AppSettings.SimulationPanels.View2DPairShowRedLink = !_vm.AppSettings.SimulationPanels.View2DPairShowRedLink;
        ApplySavedPair2DOverlaySettings();
        _vm.SaveAppSettings();
    }

    private void UpdatePlotOverlayButtons()
    {
        LegendToggleButton.Content = RecipePlot.ShowLegend ? "Пояснения: вкл" : "Пояснения: выкл";
        LinksToggleButton.Content = RecipePlot.ShowPairLinks ? "Связи точек: вкл" : "Связи точек: выкл";
    }

    private void ResetPanels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyDefaultPanelSizes();
        RecipePlot.ResetZoom();
        Pair2DPlot.ResetZoom();
        RecipePlot.ShowLegend = true;
        RecipePlot.ShowPairLinks = false;
        UpdateZoomText();
        UpdatePair2DZoomText();
        ApplySavedPair2DOverlaySettings();
        UpdatePlotOverlayButtons();

        ParametersPanel.IsVisible = true;
        VisualizationPanel.IsVisible = true;
        Pair2DPanel.IsVisible = true;
        SelectedPointPanel.IsVisible = true;

        ParametersResizeHandle.IsVisible = true;
        VisualizationResizeHandle.IsVisible = true;
        Pair2DResizeHandle.IsVisible = true;
        SelectedPointResizeHandle.IsVisible = true;

        ApplyDefaultPanelsLayout();
        InitializePanelZOrder();
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
        _vm.AppSettings.EditorPanels.Pair2D = ToLayout(Pair2DPanel, _vm.AppSettings.EditorPanels.Pair2D);
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
        UpdateResizeHandleFor(Pair2DPanel);
        UpdateResizeHandleFor(SelectedPointPanel);
    }

    private void UpdateResizeHandleFor(Border panel)
    {
        var handle = panel == ParametersPanel
            ? ParametersResizeHandle
            : panel == VisualizationPanel
                ? VisualizationResizeHandle
                : panel == Pair2DPanel
                    ? Pair2DResizeHandle
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
