using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Avalonia.Threading;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly RecipeRepository _repo;
    private readonly RecipeExcelService _excel;
    private readonly RecipeImportService _importService;
    private readonly Action _navigateBack;

    private RecipeDocument? _document;
    private RecipePoint? _selectedPoint;

    // Simulation
    private readonly DispatcherTimer _timer;
    private readonly SimulationPathService _pathService = new();
    private bool _isPlaying;
    private double _speedMultiplier = 1.0;
    private double _progress;
    private double _elapsedSec;
    private double _totalDurationSec;
    private Vector3 _toolPosition;
    private double _currentAlfa;
    private double _currentBetta;
    private double _currentTargetX;
    private double _currentTargetZ;
    private int _currentSegmentIndex = -1;
    private double _currentSegmentT;
    private SimulationPath _timeline = new(new List<PathWaypoint>(), new List<PathSegment>(), 0);

    private bool _suppressRecalc;
    private string _importDiagnosticsSummary = "";

    public EditorViewModel(SettingsService settings, RecipeRepository repo, RecipeExcelService excel, RecipeImportService importService, Action navigateBack)
    {
        _settings = settings;
        _repo = repo;
        _excel = excel;
        _importService = importService;
        _navigateBack = navigateBack;

        Points = new ObservableCollection<RecipePoint>();

        SaveCommand = new RelayCommand(Save, () => HasDocument);
        BackCommand = new RelayCommand(() => _navigateBack());
        RecalculateCommand = new RelayCommand(Recalculate, () => HasDocument);

        ExportExcelCommand = new RelayCommand(ExportExcelRequested, () => HasDocument);
        ImportExcelCommand = new RelayCommand(ImportExcelRequested, () => HasDocument);
        ShowChartsCommand = new RelayCommand(ShowChartsRequested, () => HasDocument);

        AddPointCommand = new RelayCommand(AddPoint, () => HasDocument);
        RemovePointCommand = new RelayCommand(RemoveSelectedPoint, () => HasDocument && SelectedPoint is not null);
        DuplicatePointCommand = new RelayCommand(DuplicateSelectedPoint, () => HasDocument && SelectedPoint is not null);

        MoveUpCommand = new RelayCommand(MoveUp, () => HasDocument && SelectedPoint is not null && SelectedPoint.NPoint > 1);
        MoveDownCommand = new RelayCommand(MoveDown, () => HasDocument && SelectedPoint is not null && SelectedPoint.NPoint < Points.Count);

        PlayPauseCommand = new RelayCommand(TogglePlay, () => HasDocument);
        StopCommand = new RelayCommand(Stop, () => HasDocument);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += (_, __) => Tick();
    }

    public bool HasDocument => _document is not null;

    public AppSettings AppSettings => _settings.Settings;

    public void SaveAppSettings() => _settings.Save();


    public RecipeDocument? Document
    {
        get => _document;
        private set
        {
            if (SetProperty(ref _document, value))
            {
                RaisePropertyChanged(nameof(HasDocument));
                RaisePropertyChanged(nameof(RecipeCode));
                RaisePropertyChanged(nameof(FilePath));
                RaisePropertyChanged(nameof(ContainerPresent));
                RaisePropertyChanged(nameof(DClampForm));
                RaisePropertyChanged(nameof(DClampCont));
                SaveCommand.RaiseCanExecuteChanged();
                RecalculateCommand.RaiseCanExecuteChanged();
                ExportExcelCommand.RaiseCanExecuteChanged();
                ImportExcelCommand.RaiseCanExecuteChanged();
                ShowChartsCommand.RaiseCanExecuteChanged();
                AddPointCommand.RaiseCanExecuteChanged();
                PlayPauseCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RecipeCode
    {
        get => Document?.RecipeCode ?? "";
        set
        {
            if (Document is null) return;
            Document.RecipeCode = value;
            RaisePropertyChanged();
        }
    }

    public string FilePath => Document?.FilePath ?? "";

    public bool ContainerPresent
    {
        get => Document?.ContainerPresent ?? true;
        set
        {
            if (Document is null) return;
            Document.ContainerPresent = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public double DClampForm
    {
        get => Document?.DClampForm ?? 800;
        set
        {
            if (Document is null) return;
            Document.DClampForm = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public double DClampCont
    {
        get => Document?.DClampCont ?? 1600;
        set
        {
            if (Document is null) return;
            Document.DClampCont = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public ObservableCollection<RecipePoint> Points { get; }
    public IList<RecipePoint> PointsForAnimation => GetAnimationPoints();

    public RecipePoint? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (SetProperty(ref _selectedPoint, value))
            {
                RaisePropertyChanged(nameof(HasSelectedPoint));
                RemovePointCommand.RaiseCanExecuteChanged();
                DuplicatePointCommand.RaiseCanExecuteChanged();
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPoint => SelectedPoint is not null;

    // Commands
    public RelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand RecalculateCommand { get; }

    // Excel commands are triggered by the View (file dialogs).
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ImportExcelCommand { get; }

    public event Action? RequestExportExcel;
    public event Action? RequestImportExcel;
    public event Action? RequestShowCharts;

    public RelayCommand AddPointCommand { get; }
    public RelayCommand RemovePointCommand { get; }
    public RelayCommand DuplicatePointCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ShowChartsCommand { get; }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => SetProperty(ref _speedMultiplier, value);
    }

    /// <summary>
    /// 0..1 progress along the tool path.
    /// </summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public double ToolXRaw => _toolPosition.X;
    public double ToolZRaw => _toolPosition.Z;

    public double CurrentAlfa
    {
        get => _currentAlfa;
        private set => SetProperty(ref _currentAlfa, value);
    }

    public double CurrentBetta
    {
        get => _currentBetta;
        private set => SetProperty(ref _currentBetta, value);
    }

    public double CurrentTargetX => _currentTargetX;
    public double CurrentTargetZ => _currentTargetZ;
    public int CurrentSegmentIndex => _currentSegmentIndex;
    public double CurrentSegmentT => _currentSegmentT;

    public string ImportDiagnosticsSummary
    {
        get => _importDiagnosticsSummary;
        private set => SetProperty(ref _importDiagnosticsSummary, value);
    }

    public void CloseDocument()
    {
        Stop();
        UnhookPoints();
        Points.Clear();
        SelectedPoint = null;
        Document = null;
        ImportDiagnosticsSummary = "";
        RecalculateTimeline();
    }

    public void LoadDocument(RecipeDocument doc)
    {
        UnhookPoints();

        Document = doc;
        Points.Clear();
        foreach (var p in doc.Points)
        {
            Points.Add(p);
        }

        HookPoints();

        RecalculateIfNeeded();
        SelectedPoint = Points.FirstOrDefault();
        Stop();
        ImportDiagnosticsSummary = "";
        RecalculateTimeline();
    }

    private void RecalculateIfNeeded()
    {
        if (Document is null)
            return;

        SyncDocumentPointsFromEditor();

        if (HasCalculatedCoordinates(Points))
            return;

        Recalculate();
    }

    private void SyncDocumentPointsFromEditor()
    {
        if (Document is null)
            return;

        Document.Points.Clear();
        foreach (var p in Points)
            Document.Points.Add(p);
    }

    private static bool HasCalculatedCoordinates(ObservableCollection<RecipePoint> points)
    {
        const double eps = 1e-6;
        return points.Any(p =>
            Math.Abs(p.Xr0) > eps ||
            Math.Abs(p.Zr0) > eps ||
            Math.Abs(p.DX) > eps ||
            Math.Abs(p.DZ) > eps ||
            Math.Abs(p.XPuls) > eps ||
            Math.Abs(p.ZPuls) > eps);
    }

    public void Recalculate()
    {
        if (Document is null) return;
        if (_suppressRecalc) return;

        try
        {
            _suppressRecalc = true;

            SyncDocumentPointsFromEditor();
            RecipeCalculator.Recalculate(Document, _settings.Settings);

            // Refresh computed values in-place; also refresh recipe-level fields
            RaisePropertyChanged(nameof(RecipeCode));
            RaisePropertyChanged(nameof(ContainerPresent));
            RaisePropertyChanged(nameof(DClampForm));
            RaisePropertyChanged(nameof(DClampCont));
            RecalculateTimeline();
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private void Save()
    {
        if (Document is null) return;
        Recalculate();
        _repo.Save(Document);
    }

    private void ExportExcelRequested()
        => RequestExportExcel?.Invoke();

    private void ImportExcelRequested()
        => RequestImportExcel?.Invoke();

    private void ShowChartsRequested()
        => RequestShowCharts?.Invoke();

    public void ExportToExcel(string path)
    {
        if (Document is null) return;
        Recalculate();
        _excel.Export(Document, path);
    }

    public RecipeImportPreview PreviewImport(string path)
    {
        return _importService.Preview(path);
    }

    public bool ApplyImportedPreview(RecipeImportPreview preview)
    {
        if (Document is null) return false;

        ImportDiagnosticsSummary = preview.Diagnostics;

        if (!preview.IsSuccess || preview.HasBlockingIssues || preview.Document is null)
        {
            return false;
        }

        var imported = preview.Document;

        // Replace points in the current document (same RecipeId, do not rename recipe in editor import flow).
        imported.RecipeId = Document.RecipeId;
        imported.CreatedUtc = Document.CreatedUtc;
        imported.ModifiedUtc = Document.ModifiedUtc;

        UnhookPoints();

        Document.ContainerPresent = imported.ContainerPresent;
        Document.DClampForm = imported.DClampForm;
        Document.DClampCont = imported.DClampCont;

        Points.Clear();
        foreach (var p in imported.Points)
            Points.Add(p);

        HookPoints();

        RecalculateIfNeeded();
        return true;
    }

    private void AddPoint()
    {
        if (Document is null) return;

        var last = Points.LastOrDefault();
        var p = last is null
            ? RecipeDocumentFactory.CreateStarter(Document.RecipeCode).Points.First()
            : last.Clone();

        p.NPoint = Points.Count + 1;

        Points.Add(p);
        p.PropertyChanged += OnPointPropertyChanged;
        SelectedPoint = p;

        Recalculate();
    }

    private void DuplicateSelectedPoint()
    {
        if (SelectedPoint is null) return;

        var idx = SelectedPoint.NPoint - 1;
        var clone = SelectedPoint.Clone();
        clone.NPoint = idx + 2;

        Points.Insert(idx + 1, clone);
        clone.PropertyChanged += OnPointPropertyChanged;
        SelectedPoint = clone;
        Recalculate();
    }

    private void RemoveSelectedPoint()
    {
        if (SelectedPoint is null) return;
        var pointToRemove = SelectedPoint;
        var idx = pointToRemove.NPoint - 1;
        pointToRemove.PropertyChanged -= OnPointPropertyChanged;
        Points.RemoveAt(idx);
        SelectedPoint = Points.ElementAtOrDefault(Math.Clamp(idx, 0, Points.Count - 1));
        Recalculate();
    }

    private void MoveUp()
    {
        if (SelectedPoint is null) return;
        var idx = SelectedPoint.NPoint - 1;
        if (idx <= 0) return;

        Points.Move(idx, idx - 1);
        SelectedPoint = Points[idx - 1];
        Recalculate();
    }

    private void MoveDown()
    {
        if (SelectedPoint is null) return;
        var idx = SelectedPoint.NPoint - 1;
        if (idx >= Points.Count - 1) return;

        Points.Move(idx, idx + 1);
        SelectedPoint = Points[idx + 1];
        Recalculate();
    }

    private void HookPoints()
    {
        foreach (var p in Points)
        {
            p.PropertyChanged += OnPointPropertyChanged;
        }
    }

    private void UnhookPoints()
    {
        foreach (var p in Points)
        {
            p.PropertyChanged -= OnPointPropertyChanged;
        }
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRecalc) return;

        // If user edits an input column (or drags a point), we recalculate.
        switch (e.PropertyName)
        {
            case nameof(RecipePoint.RCrd):
            case nameof(RecipePoint.ZCrd):
            case nameof(RecipePoint.Place):
            case nameof(RecipePoint.ANozzle):
            case nameof(RecipePoint.Alfa):
            case nameof(RecipePoint.Betta):
            case nameof(RecipePoint.SpeedTable):
            case nameof(RecipePoint.IceRate):
            case nameof(RecipePoint.Container):
            case nameof(RecipePoint.DClampForm):
            case nameof(RecipePoint.DClampCont):
            case nameof(RecipePoint.Safe):
                Recalculate();
                break;
            case nameof(RecipePoint.Act):
            case nameof(RecipePoint.Hidden):
                RecalculateTimeline();
                break;
        }
    }


    private void TogglePlay()
    {
        if (GetAnimationPoints().Count < 2)
            return;

        if (IsPlaying)
        {
            IsPlaying = false;
            _timer.Stop();
        }
        else
        {
            if (Progress >= 1)
            {
                _elapsedSec = 0;
                UpdateFromElapsed();
            }

            IsPlaying = true;
            _timer.Start();
        }
    }

    private void Stop()
    {
        IsPlaying = false;
        _timer.Stop();
        _elapsedSec = 0;
        UpdateFromElapsed();
    }

    private void Tick()
    {
        if (!IsPlaying || _totalDurationSec <= 0)
            return;

        _elapsedSec += 0.016 * SpeedMultiplier;
        if (_elapsedSec >= _totalDurationSec)
        {
            _elapsedSec = _totalDurationSec;
            IsPlaying = false;
            _timer.Stop();
        }

        UpdateFromElapsed();
    }

    private void RecalculateTimeline()
    {
        var points = GetAnimationPoints();
        _timeline = _pathService.Build(points.ToList(), smoothMotion: true);
        _totalDurationSec = _timeline.TotalDurationSec;
        _elapsedSec = Math.Min(_elapsedSec, _totalDurationSec);
        UpdateFromElapsed();
        RaisePropertyChanged(nameof(PointsForAnimation));
    }

    private void UpdateFromElapsed()
    {
        var points = GetAnimationPoints();
        if (points.Count == 0)
        {
            _toolPosition = default;
            _currentTargetX = 0;
            _currentTargetZ = 0;
            _currentSegmentIndex = -1;
            _currentSegmentT = 0;
            Progress = 0;
            CurrentAlfa = 0;
            CurrentBetta = 0;
            RaiseTelemetry();
            return;
        }

        var sample = _pathService.Evaluate(_timeline, _elapsedSec, smoothMotion: true);
        _toolPosition = sample.Position;
        Progress = sample.Progress;
        CurrentAlfa = sample.Alfa;
        CurrentBetta = sample.Betta;
        var (targetX, targetZ) = EvaluateTargetPosition(points, sample);
        _currentTargetX = targetX;
        _currentTargetZ = targetZ;
        _currentSegmentIndex = sample.SegmentIndex;
        _currentSegmentT = sample.SegmentT;
        RaiseTelemetry();
    }

    private void RaiseTelemetry()
    {
        RaisePropertyChanged(nameof(ToolXRaw));
        RaisePropertyChanged(nameof(ToolZRaw));
        RaisePropertyChanged(nameof(CurrentTargetX));
        RaisePropertyChanged(nameof(CurrentTargetZ));
        RaisePropertyChanged(nameof(CurrentSegmentIndex));
        RaisePropertyChanged(nameof(CurrentSegmentT));
    }

    private IList<RecipePoint> GetAnimationPoints()
    {
        var all = Points.ToList();
        if (all.Count < 2)
            return all;

        var activeRobot = all.Where(p => p.Act && !p.Hidden && HasRobotGeometry(p)).ToList();
        if (activeRobot.Count >= 2)
            return activeRobot;

        var activeRobotIncludingHidden = all.Where(p => p.Act && HasRobotGeometry(p)).ToList();
        if (activeRobotIncludingHidden.Count >= 2)
            return activeRobotIncludingHidden;

        var activeRenderable = all.Where(p => p.Act && !p.Hidden && IsRenderable(p)).ToList();
        if (activeRenderable.Count >= 2)
            return activeRenderable;

        return all.Where(p => p.Act && !p.Hidden).ToList() is { Count: >= 2 } activeVisible
            ? activeVisible
            : all;
    }

    private (double X, double Z) EvaluateTargetPosition(IList<RecipePoint> points, PathSample sample)
    {
        if (points.Count == 0)
            return (0, 0);

        if (points.Count == 1)
        {
            var p = points[0].GetTargetPoint(AppSettings.HZone);
            return (p.Xp, p.Zp);
        }

        var seg = Math.Clamp(sample.SegmentIndex, 0, points.Count - 2);
        var t = Math.Clamp(sample.SegmentT, 0f, 1f);
        var a = points[seg].GetTargetPoint(AppSettings.HZone);
        var b = points[seg + 1].GetTargetPoint(AppSettings.HZone);
        return (
            a.Xp + (b.Xp - a.Xp) * t,
            a.Zp + (b.Zp - a.Zp) * t);
    }

    private static bool HasRobotGeometry(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.Xr0) > eps
            || Math.Abs(p.Yx0) > eps
            || Math.Abs(p.Zr0) > eps
            || Math.Abs(p.DX) > eps
            || Math.Abs(p.DY) > eps
            || Math.Abs(p.DZ) > eps;
    }

    private static bool IsRenderable(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.RCrd) > eps
            || Math.Abs(p.ZCrd) > eps
            || Math.Abs(p.Xr0 + p.DX) > eps
            || Math.Abs(p.Zr0 + p.DZ) > eps;
    }
}
