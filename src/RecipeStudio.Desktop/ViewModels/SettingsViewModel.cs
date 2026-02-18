using System;
using System.Collections.Generic;
using System.IO;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public const string SectionStorage = "storage";
    public const string SectionMachine = "machine";
    public const string SectionCoefficients = "coefficients";
    public const string SectionGraphics = "graphics";
    public const string SectionLogging = "logging";

    private readonly SettingsService _settings;
    private readonly Action _onChanged;

    private string _selectedSection = SectionStorage;

    public SettingsViewModel(SettingsService settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        SaveCommand = new RelayCommand(Save);
    }

    public RelayCommand SaveCommand { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                RaisePropertyChanged(nameof(IsStorageSection));
                RaisePropertyChanged(nameof(IsMachineSection));
                RaisePropertyChanged(nameof(IsCoefficientsSection));
                RaisePropertyChanged(nameof(IsGraphicsSection));
                RaisePropertyChanged(nameof(IsLoggingSection));
                RaisePropertyChanged(nameof(CurrentSectionTitle));
            }
        }
    }

    public bool IsStorageSection => SelectedSection == SectionStorage;
    public bool IsMachineSection => SelectedSection == SectionMachine;
    public bool IsCoefficientsSection => SelectedSection == SectionCoefficients;
    public bool IsGraphicsSection => SelectedSection == SectionGraphics;
    public bool IsLoggingSection => SelectedSection == SectionLogging;

    public string CurrentSectionTitle => SelectedSection switch
    {
        SectionStorage => "Хранилище рецептов",
        SectionMachine => "Параметры станка",
        SectionCoefficients => "Коэффициенты и импульсы",
        SectionGraphics => "Графика",
        SectionLogging => "Логирование",
        _ => "Настройки"
    };

    public string RecipesFolder
    {
        get => _settings.Settings.RecipesFolder;
        set
        {
            _settings.Settings.RecipesFolder = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(DatabaseFilePath));
        }
    }

    public string SettingsFilePath => _settings.SettingsPath;
    public string DatabaseFilePath => Path.Combine(_settings.Settings.RecipesFolder, "recipes.sqlite");

    // Machine / constants
    public double HZone { get => _settings.Settings.HZone; set { _settings.Settings.HZone = value; RaisePropertyChanged(); } }
    public double HContMax { get => _settings.Settings.HContMax; set { _settings.Settings.HContMax = value; RaisePropertyChanged(); } }
    public double HBokMax { get => _settings.Settings.HBokMax; set { _settings.Settings.HBokMax = value; RaisePropertyChanged(); } }
    public double Xm { get => _settings.Settings.Xm; set { _settings.Settings.Xm = value; RaisePropertyChanged(); } }
    public double Ym { get => _settings.Settings.Ym; set { _settings.Settings.Ym = value; RaisePropertyChanged(); } }
    public double Zm { get => _settings.Settings.Zm; set { _settings.Settings.Zm = value; RaisePropertyChanged(); } }
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
            RaisePropertyChanged(nameof(PlotOpacityDisplay));
        }
    }

    public string PlotOpacityDisplay => $"{PlotOpacity:0.00}";

    public double PlotStrokeThickness { get => _settings.Settings.PlotStrokeThickness; set { _settings.Settings.PlotStrokeThickness = value; RaisePropertyChanged(); } }
    public double PlotPointRadius { get => _settings.Settings.PlotPointRadius; set { _settings.Settings.PlotPointRadius = value; RaisePropertyChanged(); } }

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

    // Logging
    public bool LoggingEnabled
    {
        get => _settings.Settings.LoggingEnabled;
        set
        {
            _settings.Settings.LoggingEnabled = value;
            RaisePropertyChanged();
        }
    }

    public int LogRetentionDays
    {
        get => _settings.Settings.LogRetentionDays;
        set
        {
            _settings.Settings.LogRetentionDays = Math.Clamp(value, 1, 3650);
            RaisePropertyChanged();
        }
    }

    public string LogsFolder
    {
        get => _settings.Settings.LogsFolder;
        set
        {
            _settings.Settings.LogsFolder = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(LogFilePath));
        }
    }

    public IReadOnlyList<string> LogModes { get; } = new[] { LogSeverity.Info, LogSeverity.Warning, LogSeverity.Error };

    public string LogMode
    {
        get => _settings.Settings.LogMode;
        set
        {
            _settings.Settings.LogMode = value;
            RaisePropertyChanged();
        }
    }

    public string LogFilePath => Path.Combine(_settings.Settings.LogsFolder, $"{DateTime.Now:dd.MM.yyyy}.log");

    private void Save()
    {
        _settings.Save();
        _onChanged();
        RaisePropertyChanged(nameof(SettingsFilePath));
        RaisePropertyChanged(nameof(DatabaseFilePath));
        RaisePropertyChanged(nameof(LogFilePath));
    }
}
