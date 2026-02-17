using System;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly Action _onChanged;

    public SettingsViewModel(SettingsService settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        SaveCommand = new RelayCommand(Save);
    }

    public RelayCommand SaveCommand { get; }

    public string RecipesFolder
    {
        get => _settings.Settings.RecipesFolder;
        set
        {
            _settings.Settings.RecipesFolder = value;
            RaisePropertyChanged();
        }
    }

    // Constants
    public double HZone { get => _settings.Settings.HZone; set { _settings.Settings.HZone = value; RaisePropertyChanged(); } }
    public double Lz { get => _settings.Settings.Lz; set { _settings.Settings.Lz = value; RaisePropertyChanged(); } }

    public double PulseX { get => _settings.Settings.PulseX; set { _settings.Settings.PulseX = value; RaisePropertyChanged(); } }
    public double PulseY { get => _settings.Settings.PulseY; set { _settings.Settings.PulseY = value; RaisePropertyChanged(); } }
    public double PulseZ { get => _settings.Settings.PulseZ; set { _settings.Settings.PulseZ = value; RaisePropertyChanged(); } }
    public double PulseA { get => _settings.Settings.PulseA; set { _settings.Settings.PulseA = value; RaisePropertyChanged(); } }
    public double PulseB { get => _settings.Settings.PulseB; set { _settings.Settings.PulseB = value; RaisePropertyChanged(); } }
    public double PulseTop { get => _settings.Settings.PulseTop; set { _settings.Settings.PulseTop = value; RaisePropertyChanged(); } }
    public double PulseLow { get => _settings.Settings.PulseLow; set { _settings.Settings.PulseLow = value; RaisePropertyChanged(); } }
    public double PulseClamp { get => _settings.Settings.PulseClamp; set { _settings.Settings.PulseClamp = value; RaisePropertyChanged(); } }

    // Plot
    public double PlotOpacity
    {
        get => _settings.Settings.PlotOpacity;
        set
        {
            _settings.Settings.PlotOpacity = Math.Clamp(value, 0.05, 0.90);
            RaisePropertyChanged();
        }
    }

    public double PlotStrokeThickness { get => _settings.Settings.PlotStrokeThickness; set { _settings.Settings.PlotStrokeThickness = value; RaisePropertyChanged(); } }

    public bool PlotShowPolyline { get => _settings.Settings.PlotShowPolyline; set { _settings.Settings.PlotShowPolyline = value; RaisePropertyChanged(); } }
    public bool PlotShowSmooth { get => _settings.Settings.PlotShowSmooth; set { _settings.Settings.PlotShowSmooth = value; RaisePropertyChanged(); } }
    public bool PlotShowTargetPoints { get => _settings.Settings.PlotShowTargetPoints; set { _settings.Settings.PlotShowTargetPoints = value; RaisePropertyChanged(); } }
    public bool PlotEnableDrag { get => _settings.Settings.PlotEnableDrag; set { _settings.Settings.PlotEnableDrag = value; RaisePropertyChanged(); } }

    public int SmoothSegmentsPerSpan
    {
        get => _settings.Settings.SmoothSegmentsPerSpan;
        set
        {
            _settings.Settings.SmoothSegmentsPerSpan = Math.Clamp(value, 4, 64);
            RaisePropertyChanged();
        }
    }

    private void Save()
    {
        _settings.Save();
        _onChanged();
    }
}
