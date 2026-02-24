using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class SimulationViewModel : ViewModelBase
{
    private readonly EditorViewModel _editor;
    private readonly DispatcherTimer _timer;

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

    // Extra factor to keep playback readable in UI (physical durations can be too long).
    private const double PlaybackScale = 4.0;

    public SimulationViewModel(EditorViewModel editor)
    {
        _editor = editor;
        _editor.Points.CollectionChanged += OnEditorPointsChanged;
        HookPointHandlers(_editor.Points);

        PlayPauseCommand = new RelayCommand(TogglePlay, () => _editor.HasDocument && _editor.Points.Count > 1);
        StopCommand = new RelayCommand(Stop, () => _editor.HasDocument);
        StepPreviousCommand = new RelayCommand(StepPrevious, () => _editor.HasDocument && _editor.Points.Count > 1);
        StepNextCommand = new RelayCommand(StepNext, () => _editor.HasDocument && _editor.Points.Count > 1);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, __) => Tick();

        RecalculateTimeline();
    }

    public ObservableCollection<RecipePoint> Points => _editor.Points;
    public AppSettings AppSettings => _editor.AppSettings;
    public string RecipePath => _editor.FilePath;

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand StepPreviousCommand { get; }
    public RelayCommand StepNextCommand { get; }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set => SetProperty(ref _showGrid, value);
    }

    public bool ShowPairLinks
    {
        get => _showPairLinks;
        set => SetProperty(ref _showPairLinks, value);
    }

    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => SetProperty(ref _speedMultiplier, Math.Clamp(value, 0.2, 12));
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public int CurrentStepIndex => FindCurrentStep();
    public int TotalSteps => Math.Max(0, Points.Count - 1);

    public double ToolR => Math.Round(Math.Sqrt(_toolPosition.X * _toolPosition.X + _toolPosition.Y * _toolPosition.Y), 1);
    public double ToolZ => Math.Round(_toolPosition.Z, 1);
    public double ToolX => Math.Round(_toolPosition.X, 1);
    public double ToolY => Math.Round(_toolPosition.Y, 1);

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

    public int CurrentXPuls => (int)Math.Round(ToolX * AppSettings.PulseX, 0, MidpointRounding.AwayFromZero);
    public int CurrentYPuls => (int)Math.Round(ToolY * AppSettings.PulseY, 0, MidpointRounding.AwayFromZero);
    public int CurrentZPuls => (int)Math.Round(ToolZ * AppSettings.PulseZ, 0, MidpointRounding.AwayFromZero);

    public int ProgressPercent => (int)Math.Round(Progress * 100, 0, MidpointRounding.AwayFromZero);

    private void TogglePlay()
    {
        if (Points.Count < 2)
            return;

        if (IsPlaying)
        {
            IsPlaying = false;
            _timer.Stop();
            return;
        }

        if (Progress >= 1)
        {
            _elapsedSec = 0;
            UpdateFromElapsed();
        }

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
        if (_stepTimes.Count < 2)
            return;

        IsPlaying = false;
        _timer.Stop();

        var idx = FindCurrentStep();
        var next = Math.Clamp(idx + 1, 0, _stepTimes.Count - 1);
        _elapsedSec = _stepTimes[next];
        UpdateFromElapsed();
    }

    private void StepPrevious()
    {
        if (_stepTimes.Count < 2)
            return;

        IsPlaying = false;
        _timer.Stop();

        var idx = FindCurrentStep();
        var prev = Math.Clamp(idx - 1, 0, _stepTimes.Count - 1);
        _elapsedSec = _stepTimes[prev];
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
        _totalDurationSec = 0;
        _stepTimes.Clear();
        _stepTimes.Add(0);

        if (Points.Count < 2)
        {
            UpdateFromElapsed();
            RaiseCommandsState();
            return;
        }

        for (var i = 0; i < Points.Count - 1; i++)
        {
            var a = GetRobotPosition(Points[i]);
            var b = GetRobotPosition(Points[i + 1]);
            var len = Distance(a, b);
            var speedMmSec = Math.Max(1, Points[i].NozzleSpeedMmMin / 60.0);
            var segDuration = len / speedMmSec / PlaybackScale;
            _totalDurationSec += segDuration;
            _stepTimes.Add(_totalDurationSec);
        }

        _elapsedSec = Math.Min(_elapsedSec, _totalDurationSec);
        UpdateFromElapsed();
        RaiseCommandsState();
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
        if (Points.Count == 0)
        {
            _toolPosition = default;
            Progress = 0;
            RaiseTelemetry();
            return;
        }

        if (Points.Count == 1 || _totalDurationSec <= 1e-6)
        {
            _toolPosition = GetRobotPosition(Points[0]);
            CurrentAlfa = Points[0].Alfa;
            CurrentBetta = Points[0].Betta;
            Progress = 0;
            RaiseTelemetry();
            return;
        }

        var t = Math.Clamp(_elapsedSec, 0, _totalDurationSec);
        Progress = _totalDurationSec <= 0 ? 0 : t / _totalDurationSec;

        double acc = 0;
        for (var i = 0; i < Points.Count - 1; i++)
        {
            var a = GetRobotPosition(Points[i]);
            var b = GetRobotPosition(Points[i + 1]);
            var len = Distance(a, b);
            var speedMmSec = Math.Max(1, Points[i].NozzleSpeedMmMin / 60.0);
            var segDuration = len / speedMmSec / PlaybackScale;
            var next = acc + segDuration;

            if (t <= next || i == Points.Count - 2)
            {
                var local = segDuration <= 1e-9 ? 1 : (t - acc) / segDuration;
                local = Math.Clamp(local, 0, 1);
                _toolPosition = Lerp(a, b, local);
                CurrentAlfa = Lerp(Points[i].Alfa, Points[i + 1].Alfa, local);
                CurrentBetta = Lerp(Points[i].Betta, Points[i + 1].Betta, local);
                RaiseTelemetry();
                return;
            }

            acc = next;
        }
    }

    private int FindCurrentStep()
    {
        if (_stepTimes.Count <= 1)
            return 0;

        for (var i = 0; i < _stepTimes.Count - 1; i++)
        {
            if (_elapsedSec < _stepTimes[i + 1] - 1e-6)
                return i;
        }

        return _stepTimes.Count - 1;
    }

    private void RaiseTelemetry()
    {
        RaisePropertyChanged(nameof(ToolR));
        RaisePropertyChanged(nameof(ToolZ));
        RaisePropertyChanged(nameof(ToolX));
        RaisePropertyChanged(nameof(ToolY));
        RaisePropertyChanged(nameof(CurrentAlfa));
        RaisePropertyChanged(nameof(CurrentBetta));
        RaisePropertyChanged(nameof(CurrentXPuls));
        RaisePropertyChanged(nameof(CurrentYPuls));
        RaisePropertyChanged(nameof(CurrentZPuls));
        RaisePropertyChanged(nameof(ProgressPercent));
        RaisePropertyChanged(nameof(CurrentStepIndex));
        RaisePropertyChanged(nameof(TotalSteps));
    }

    private static Point3D GetRobotPosition(RecipePoint p)
        => new(p.Xr0 + p.DX, p.Yx0 + p.DY, p.Zr0 + p.DZ);

    private static double Distance(Point3D a, Point3D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dz = b.Z - a.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Point3D Lerp(Point3D a, Point3D b, double t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;

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
        if (e.PropertyName is nameof(RecipePoint.DX) or nameof(RecipePoint.DY) or nameof(RecipePoint.DZ) or nameof(RecipePoint.NozzleSpeedMmMin) or nameof(RecipePoint.Alfa) or nameof(RecipePoint.Betta))
            RecalculateTimeline();
    }

    private readonly record struct Point3D(double X, double Y, double Z);
}
