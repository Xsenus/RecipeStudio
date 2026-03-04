using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class SimulationViewModel : ViewModelBase
{
    private readonly EditorViewModel _editor;
    private readonly DispatcherTimer _timer;
    private readonly SimulationPathService _pathService = new();
    private readonly List<double> _stepTimes = new();

    private double _speedMultiplier = 2.0;
    private bool _isPlaying;
    private double _elapsedSec;
    private double _totalDurationSec;
    private double _progress;
    private Point3D _toolPosition;
    private double _currentAlfa;
    private double _currentBetta;
    private double _currentTargetX;
    private double _currentTargetZ;
    private int _currentSegmentIndex = -1;
    private double _currentSegmentT;
    private bool _showGrid = true;
    private bool _showPairLinks = false;
    // Startup defaults for the industrial simulation scenario:
    // include safe travel points and use smooth interpolation.
    private bool _includeSafePoints = true;
    private bool _smoothMotion = true;
    private string _nozzleAngleWarning = string.Empty;
    private SimulationPath _timeline = new(new List<PathWaypoint>(), new List<PathSegment>(), 0);

    public SimulationViewModel(EditorViewModel editor)
    {
        _editor = editor;
        _editor.Points.CollectionChanged += OnEditorPointsChanged;
        HookPointHandlers(_editor.Points);
        _editor.AppSettings.NozzleOrientationMode = NozzleOrientationModes.Normalize(_editor.AppSettings.NozzleOrientationMode);

        PlayPauseCommand = new RelayCommand(TogglePlay, () => _editor.HasDocument && GetAnimationPoints().Count > 1);
        StopCommand = new RelayCommand(Stop, () => _editor.HasDocument);
        StepPreviousCommand = new RelayCommand(StepPrevious, () => _editor.HasDocument && GetAnimationPoints().Count > 1);
        StepNextCommand = new RelayCommand(StepNext, () => _editor.HasDocument && GetAnimationPoints().Count > 1);
        ResetRecommendedModesCommand = new RelayCommand(ApplyRecommendedModes);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, __) => Tick();

        RecalculateTimeline();
    }

    public IList<RecipePoint> PointsForPlot => _editor.Points.ToList();
    public IList<RecipePoint> PointsForAnimation => GetAnimationPoints();
    public AppSettings AppSettings => _editor.AppSettings;
    public string RecipePath => _editor.FilePath;

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand StepPreviousCommand { get; }
    public RelayCommand StepNextCommand { get; }
    public RelayCommand ResetRecommendedModesCommand { get; }

    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
    public bool ShowGrid { get => _showGrid; set => SetProperty(ref _showGrid, value); }
    public bool ShowPairLinks { get => _showPairLinks; set => SetProperty(ref _showPairLinks, value); }
    public string NozzleOrientationMode => NozzleOrientationModes.Normalize(AppSettings.NozzleOrientationMode);
    public string NozzleOrientationLabel => UsePhysicalNozzleOrientation ? "Ориентация: A/B (физика)" : "Ориентация: на цель (legacy)";
    public string NozzleAngleWarning => _nozzleAngleWarning;
    public bool HasNozzleAngleWarning => !string.IsNullOrWhiteSpace(_nozzleAngleWarning);

    public bool UsePhysicalNozzleOrientation
    {
        get => NozzleOrientationPolicy.UsePhysicalOrientation(AppSettings.NozzleOrientationMode);
        set
        {
            var mode = value ? NozzleOrientationModes.PhysicalAngles : NozzleOrientationModes.TargetTracking;
            if (NozzleOrientationMode == mode)
                return;

            AppSettings.NozzleOrientationMode = mode;
            RaisePropertyChanged(nameof(UsePhysicalNozzleOrientation));
            RaisePropertyChanged(nameof(NozzleOrientationMode));
            RaisePropertyChanged(nameof(NozzleOrientationLabel));
            SaveAppSettings();
        }
    }

    public bool IncludeSafePoints
    {
        get => _includeSafePoints;
        set { if (SetProperty(ref _includeSafePoints, value)) RecalculateTimeline(); }
    }

    public bool SmoothMotion
    {
        get => _smoothMotion;
        set { if (SetProperty(ref _smoothMotion, value)) RecalculateTimeline(); }
    }

    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => SetProperty(ref _speedMultiplier, Math.Clamp(value, 0.2, 12));
    }

    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public int CurrentStepIndex => FindCurrentStep();
    public int TotalSteps => Math.Max(0, GetAnimationPoints().Count - 1);
    public double ToolR => Math.Round(Math.Sqrt(_toolPosition.X * _toolPosition.X + _toolPosition.Y * _toolPosition.Y), 1);
    public double ToolZ => Math.Round(_toolPosition.Z, 1);
    public double ToolX => Math.Round(_toolPosition.X, 1);
    public double ToolY => Math.Round(_toolPosition.Y, 1);
    public double ToolXRaw => _toolPosition.X;
    public double ToolYRaw => _toolPosition.Y;
    public double ToolZRaw => _toolPosition.Z;
    public double CurrentAlfa { get => _currentAlfa; private set => SetProperty(ref _currentAlfa, value); }
    public double CurrentBetta { get => _currentBetta; private set => SetProperty(ref _currentBetta, value); }
    public double CurrentTargetX => _currentTargetX;
    public double CurrentTargetZ => _currentTargetZ;
    public int CurrentSegmentIndex => _currentSegmentIndex;
    public double CurrentSegmentT => _currentSegmentT;
    public int CurrentXPuls => (int)Math.Round(ToolX * AppSettings.PulseX, 0, MidpointRounding.AwayFromZero);
    public int CurrentYPuls => (int)Math.Round(ToolY * AppSettings.PulseY, 0, MidpointRounding.AwayFromZero);
    public int CurrentZPuls => (int)Math.Round(ToolZ * AppSettings.PulseZ, 0, MidpointRounding.AwayFromZero);
    public int ProgressPercent => (int)Math.Round(Progress * 100, 0, MidpointRounding.AwayFromZero);

    public void SaveAppSettings() => _editor.SaveAppSettings();

    private void ApplyRecommendedModes()
    {
        IncludeSafePoints = true;
        SmoothMotion = true;
        ShowGrid = true;
        ShowPairLinks = false;
        SpeedMultiplier = 2.0;
        UsePhysicalNozzleOrientation = true;
    }

    private void TogglePlay()
    {
        if (GetAnimationPoints().Count < 2) return;
        if (IsPlaying) { IsPlaying = false; _timer.Stop(); return; }
        if (Progress >= 1) { _elapsedSec = 0; UpdateFromElapsed(); }
        IsPlaying = true;
        _timer.Start();
    }

    private void Stop()
    {
        IsPlaying = false;
        _timer.Stop();
        _elapsedSec = 0;
        UpdateFromElapsed();
    }

    private void StepNext()
    {
        if (_stepTimes.Count < 2) return;
        IsPlaying = false;
        _timer.Stop();
        var idx = FindCurrentStep();
        _elapsedSec = _stepTimes[Math.Clamp(idx + 1, 0, _stepTimes.Count - 1)];
        UpdateFromElapsed();
    }

    private void StepPrevious()
    {
        if (_stepTimes.Count < 2) return;
        IsPlaying = false;
        _timer.Stop();
        var idx = FindCurrentStep();
        _elapsedSec = _stepTimes[Math.Clamp(idx - 1, 0, _stepTimes.Count - 1)];
        UpdateFromElapsed();
    }

    private void Tick()
    {
        if (!IsPlaying || _totalDurationSec <= 0) return;
        _elapsedSec += 0.016 * SpeedMultiplier;
        if (_elapsedSec >= _totalDurationSec) { _elapsedSec = _totalDurationSec; IsPlaying = false; _timer.Stop(); }
        UpdateFromElapsed();
    }

    private void RecalculateTimeline()
    {
        var points = GetAnimationPoints();
        _timeline = _pathService.Build(points.ToList(), SmoothMotion);
        _totalDurationSec = _timeline.TotalDurationSec;
        _stepTimes.Clear();
        _stepTimes.Add(0);
        foreach (var seg in _timeline.Segments)
            _stepTimes.Add(seg.EndSec);

        _elapsedSec = Math.Min(_elapsedSec, _totalDurationSec);
        UpdateNozzleAngleWarning(points);
        UpdateFromElapsed();
        RaiseCommandsState();
        RaisePropertyChanged(nameof(PointsForAnimation));
        RaisePropertyChanged(nameof(PointsForPlot));
    }

    private void RaiseCommandsState()
    {
        PlayPauseCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        StepPreviousCommand.RaiseCanExecuteChanged();
        StepNextCommand.RaiseCanExecuteChanged();
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
            RaiseTelemetry();
            return;
        }

        var sample = _pathService.Evaluate(_timeline, _elapsedSec, SmoothMotion);
        _toolPosition = new Point3D(sample.Position.X, sample.Position.Y, sample.Position.Z);
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

    private int FindCurrentStep()
    {
        if (_stepTimes.Count <= 1) return 0;
        for (var i = 0; i < _stepTimes.Count - 1; i++)
            if (_elapsedSec < _stepTimes[i + 1] - 1e-6)
                return i;
        return _stepTimes.Count - 1;
    }

    private void RaiseTelemetry()
    {
        RaisePropertyChanged(nameof(ToolR));
        RaisePropertyChanged(nameof(ToolZ));
        RaisePropertyChanged(nameof(ToolX));
        RaisePropertyChanged(nameof(ToolY));
        RaisePropertyChanged(nameof(ToolXRaw));
        RaisePropertyChanged(nameof(ToolYRaw));
        RaisePropertyChanged(nameof(ToolZRaw));
        RaisePropertyChanged(nameof(CurrentAlfa));
        RaisePropertyChanged(nameof(CurrentBetta));
        RaisePropertyChanged(nameof(CurrentTargetX));
        RaisePropertyChanged(nameof(CurrentTargetZ));
        RaisePropertyChanged(nameof(CurrentSegmentIndex));
        RaisePropertyChanged(nameof(CurrentSegmentT));
        RaisePropertyChanged(nameof(CurrentXPuls));
        RaisePropertyChanged(nameof(CurrentYPuls));
        RaisePropertyChanged(nameof(CurrentZPuls));
        RaisePropertyChanged(nameof(ProgressPercent));
        RaisePropertyChanged(nameof(CurrentStepIndex));
        RaisePropertyChanged(nameof(TotalSteps));
    }

    private IList<RecipePoint> GetAnimationPoints()
    {
        var all = _editor.Points.ToList();
        if (all.Count < 2)
            return all;

        if (_includeSafePoints)
        {
            var activeRobot = all.Where(p => p.Act && !p.Hidden && HasRobotGeometry(p)).ToList();
            if (activeRobot.Count >= 2)
                return activeRobot;

            var activeRobotIncludingHidden = all.Where(p => p.Act && HasRobotGeometry(p)).ToList();
            if (activeRobotIncludingHidden.Count >= 2)
                return activeRobotIncludingHidden;
        }

        // Excel-compatible mode: animate only working cleaning points (Safe=0).
        var workingRobot = all.Where(p => p.Act && !p.Safe && !p.Hidden && HasRobotGeometry(p)).ToList();
        if (workingRobot.Count >= 2)
            return workingRobot;

        var workingRobotIncludingHidden = all.Where(p => p.Act && !p.Safe && HasRobotGeometry(p)).ToList();
        if (workingRobotIncludingHidden.Count >= 2)
            return workingRobotIncludingHidden;

        var working = all.Where(p => p.Act && !p.Safe).ToList();
        if (working.Count >= 2)
            return working;

        var fallbackRobot = all.Where(p => p.Act && !p.Hidden && HasRobotGeometry(p)).ToList();
        if (fallbackRobot.Count >= 2)
            return fallbackRobot;

        var fallbackRobotIncludingHidden = all.Where(p => p.Act && HasRobotGeometry(p)).ToList();
        if (fallbackRobotIncludingHidden.Count >= 2)
            return fallbackRobotIncludingHidden;

        var fallbackRenderable = all.Where(p => p.Act && !p.Hidden && IsRenderable(p)).ToList();
        if (fallbackRenderable.Count >= 2)
            return fallbackRenderable;

        return all.Where(p => p.Act && !p.Hidden).ToList() is { Count: >= 2 } activeVisible
            ? activeVisible
            : all;
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
    private static bool IsRenderable(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.RCrd) > eps
            || Math.Abs(p.ZCrd) > eps
            || Math.Abs(p.Xr0 + p.DX) > eps
            || Math.Abs(p.Zr0 + p.DZ) > eps;
    }

    private void OnEditorPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (var item in e.OldItems.OfType<RecipePoint>())
                item.PropertyChanged -= OnPointPropertyChanged;

        if (e.NewItems is not null)
            foreach (var item in e.NewItems.OfType<RecipePoint>())
                item.PropertyChanged += OnPointPropertyChanged;

        RecalculateTimeline();
    }

    private void HookPointHandlers(ObservableCollection<RecipePoint> points)
    {
        foreach (var p in points)
            p.PropertyChanged += OnPointPropertyChanged;
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecipePoint.DX) or nameof(RecipePoint.DY) or nameof(RecipePoint.DZ) or nameof(RecipePoint.NozzleSpeedMmMin) or nameof(RecipePoint.Alfa) or nameof(RecipePoint.Betta) or nameof(RecipePoint.Act) or nameof(RecipePoint.Safe) or nameof(RecipePoint.Hidden))
            RecalculateTimeline();
    }

    private void UpdateNozzleAngleWarning(IList<RecipePoint> points)
    {
        var diagnostics = NozzleOrientationPolicy.AnalyzePoints(points, AppSettings);
        var limits = NozzleOrientationPolicy.GetLimits(AppSettings);
        _nozzleAngleWarning = NozzleOrientationPolicy.BuildWarningText(diagnostics, limits);
        RaisePropertyChanged(nameof(NozzleAngleWarning));
        RaisePropertyChanged(nameof(HasNozzleAngleWarning));
    }

    private readonly record struct Point3D(double X, double Y, double Z);
}
