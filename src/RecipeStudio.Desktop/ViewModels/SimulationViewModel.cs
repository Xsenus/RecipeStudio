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
    private bool _showGrid = true;
    private bool _showPairLinks = true;
    private bool _includeSafePoints = true;
    private bool _smoothMotion = true;
    private SimulationPath _timeline = new(new List<PathWaypoint>(), new List<PathSegment>(), 0);

    public SimulationViewModel(EditorViewModel editor)
    {
        _editor = editor;
        _editor.Points.CollectionChanged += OnEditorPointsChanged;
        HookPointHandlers(_editor.Points);

        PlayPauseCommand = new RelayCommand(TogglePlay, () => _editor.HasDocument && GetAnimationPoints().Count > 1);
        StopCommand = new RelayCommand(Stop, () => _editor.HasDocument);
        StepPreviousCommand = new RelayCommand(StepPrevious, () => _editor.HasDocument && GetAnimationPoints().Count > 1);
        StepNextCommand = new RelayCommand(StepNext, () => _editor.HasDocument && GetAnimationPoints().Count > 1);

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

    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
    public bool ShowGrid { get => _showGrid; set => SetProperty(ref _showGrid, value); }
    public bool ShowPairLinks { get => _showPairLinks; set => SetProperty(ref _showPairLinks, value); }

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
    public int CurrentXPuls => (int)Math.Round(ToolX * AppSettings.PulseX, 0, MidpointRounding.AwayFromZero);
    public int CurrentYPuls => (int)Math.Round(ToolY * AppSettings.PulseY, 0, MidpointRounding.AwayFromZero);
    public int CurrentZPuls => (int)Math.Round(ToolZ * AppSettings.PulseZ, 0, MidpointRounding.AwayFromZero);
    public int ProgressPercent => (int)Math.Round(Progress * 100, 0, MidpointRounding.AwayFromZero);

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
        _timeline = _pathService.Build(points, SmoothMotion);
        _totalDurationSec = _timeline.TotalDurationSec;
        _stepTimes.Clear();
        _stepTimes.Add(0);
        foreach (var seg in _timeline.Segments)
            _stepTimes.Add(seg.EndSec);

        _elapsedSec = Math.Min(_elapsedSec, _totalDurationSec);
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
            Progress = 0;
            RaiseTelemetry();
            return;
        }

        var sample = _pathService.Evaluate(_timeline, _elapsedSec, SmoothMotion);
        _toolPosition = new Point3D(sample.Position.X, sample.Position.Y, sample.Position.Z);
        Progress = sample.Progress;
        CurrentAlfa = sample.Alfa;
        CurrentBetta = sample.Betta;
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
        RaisePropertyChanged(nameof(CurrentXPuls));
        RaisePropertyChanged(nameof(CurrentYPuls));
        RaisePropertyChanged(nameof(CurrentZPuls));
        RaisePropertyChanged(nameof(ProgressPercent));
        RaisePropertyChanged(nameof(CurrentStepIndex));
        RaisePropertyChanged(nameof(TotalSteps));
    }

    private IList<RecipePoint> GetAnimationPoints()
    {
        var working = _editor.Points.Where(p => p.Act && (!p.Safe || IncludeSafePoints)).ToList();
        if (working.Count >= 2) return working;
        var active = _editor.Points.Where(p => p.Act).ToList();
        if (active.Count >= 2) return active;
        return _editor.Points.ToList();
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
        if (e.PropertyName is nameof(RecipePoint.DX) or nameof(RecipePoint.DY) or nameof(RecipePoint.DZ) or nameof(RecipePoint.NozzleSpeedMmMin) or nameof(RecipePoint.Alfa) or nameof(RecipePoint.Betta) or nameof(RecipePoint.Act) or nameof(RecipePoint.Safe))
            RecalculateTimeline();
    }

    private readonly record struct Point3D(double X, double Y, double Z);
}
