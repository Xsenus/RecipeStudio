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
    public const string SectionSimulation = "simulation";
    public const string SectionLogging = "logging";

    private readonly SettingsService _settings;
    private readonly Action _onChanged;
    private readonly Func<bool> _createSampleRecipe;

    private string _selectedSection = SectionStorage;

    public SettingsViewModel(SettingsService settings, Action onChanged, Func<bool> createSampleRecipe)
    {
        _settings = settings;
        _onChanged = onChanged;
        _createSampleRecipe = createSampleRecipe;

        SaveCommand = new RelayCommand(Save);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        CreateSampleRecipeCommand = new RelayCommand(() => RequestCreateSampleRecipe?.Invoke());
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetToDefaultsCommand { get; }
    public RelayCommand CreateSampleRecipeCommand { get; }

    public event Action? RequestCreateSampleRecipe;
    public event Action<string>? SettingsSaved;

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
                RaisePropertyChanged(nameof(IsSimulationSection));
                RaisePropertyChanged(nameof(IsLoggingSection));
                RaisePropertyChanged(nameof(CurrentSectionTitle));
            }
        }
    }

    public bool IsStorageSection => SelectedSection == SectionStorage;
    public bool IsMachineSection => SelectedSection == SectionMachine;
    public bool IsCoefficientsSection => SelectedSection == SectionCoefficients;
    public bool IsGraphicsSection => SelectedSection == SectionGraphics;
    public bool IsSimulationSection => SelectedSection == SectionSimulation;
    public bool IsLoggingSection => SelectedSection == SectionLogging;

    public string CurrentSectionTitle => SelectedSection switch
    {
        SectionStorage => "Хранилище рецептов",
        SectionMachine => "Параметры станка",
        SectionCoefficients => "Коэффициенты и импульсы",
        SectionGraphics => "Графика",
        SectionSimulation => "Окна симуляции",
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


    public bool AutoCreateSampleRecipeOnEmpty
    {
        get => _settings.Settings.AutoCreateSampleRecipeOnEmpty;
        set
        {
            _settings.Settings.AutoCreateSampleRecipeOnEmpty = value;
            RaisePropertyChanged();
        }
    }
    // Machine / constants
    public double HZone { get => _settings.Settings.HZone; set { _settings.Settings.HZone = value; RaisePropertyChanged(); } }
    public double HContMax { get => _settings.Settings.HContMax; set { _settings.Settings.HContMax = value; RaisePropertyChanged(); } }
    public double HBokMax { get => _settings.Settings.HBokMax; set { _settings.Settings.HBokMax = value; RaisePropertyChanged(); } }
    public double HFreeZ { get => _settings.Settings.HFreeZ; set { _settings.Settings.HFreeZ = value; RaisePropertyChanged(); } }
    public double Xm { get => _settings.Settings.Xm; set { _settings.Settings.Xm = value; RaisePropertyChanged(); } }
    public double Ym { get => _settings.Settings.Ym; set { _settings.Settings.Ym = value; RaisePropertyChanged(); } }
    public double Zm { get => _settings.Settings.Zm; set { _settings.Settings.Zm = value; RaisePropertyChanged(); } }
    public double Lz { get => _settings.Settings.Lz; set { _settings.Settings.Lz = value; RaisePropertyChanged(); } }
    public IReadOnlyList<NozzleOrientationModeOption> NozzleOrientationModeOptions { get; } = new[]
    {
        new NozzleOrientationModeOption(
            NozzleOrientationModes.PhysicalAngles,
            "Физика (A/B)",
            "Сопло ориентируется строго по углам A/B с учетом заданных диапазонов."),
        new NozzleOrientationModeOption(
            NozzleOrientationModes.TargetTracking,
            "Наведение на цель (legacy)",
            "Сопло автоматически доворачивается к текущей целевой точке траектории.")
    };

    public string NozzleOrientationMode
    {
        get => NozzleOrientationModes.Normalize(_settings.Settings.NozzleOrientationMode);
        set
        {
            _settings.Settings.NozzleOrientationMode = NozzleOrientationModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(NozzleOrientationModeDescription));
        }
    }

    public string NozzleOrientationModeDescription
        => NozzleOrientationModeOptions.FirstOrDefault(x => x.Value == NozzleOrientationMode)?.Description
           ?? "Сопло ориентируется строго по углам A/B.";

    public double AlfaMinDeg { get => _settings.Settings.AlfaMinDeg; set { _settings.Settings.AlfaMinDeg = value; RaisePropertyChanged(); } }
    public double AlfaMaxDeg { get => _settings.Settings.AlfaMaxDeg; set { _settings.Settings.AlfaMaxDeg = value; RaisePropertyChanged(); } }
    public double BettaMinDeg { get => _settings.Settings.BettaMinDeg; set { _settings.Settings.BettaMinDeg = value; RaisePropertyChanged(); } }
    public double BettaMaxDeg { get => _settings.Settings.BettaMaxDeg; set { _settings.Settings.BettaMaxDeg = value; RaisePropertyChanged(); } }

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

    public string PlotColorWorkingZone { get => _settings.Settings.PlotColorWorkingZone; set { _settings.Settings.PlotColorWorkingZone = value; RaisePropertyChanged(); } }
    public string PlotColorSafetyZone { get => _settings.Settings.PlotColorSafetyZone; set { _settings.Settings.PlotColorSafetyZone = value; RaisePropertyChanged(); } }
    public string PlotColorRobotPath { get => _settings.Settings.PlotColorRobotPath; set { _settings.Settings.PlotColorRobotPath = value; RaisePropertyChanged(); } }
    public string PlotColorPairLinks { get => _settings.Settings.PlotColorPairLinks; set { _settings.Settings.PlotColorPairLinks = value; RaisePropertyChanged(); } }
    public string PlotColorTool { get => _settings.Settings.PlotColorTool; set { _settings.Settings.PlotColorTool = value; RaisePropertyChanged(); } }

    public int SmoothSegmentsPerSpan
    {
        get => _settings.Settings.SmoothSegmentsPerSpan;
        set
        {
            _settings.Settings.SmoothSegmentsPerSpan = Math.Clamp(value, 4, 64);
            RaisePropertyChanged();
        }
    }

    private SimulationPanelsAccessSettings SimulationPanelsAccess =>
        _settings.Settings.SimulationPanels.Access ??= new SimulationPanelsAccessSettings();

    public bool SimAllowPlot
    {
        get => SimulationPanelsAccess.Plot;
        set
        {
            SimulationPanelsAccess.Plot = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowTelemetry
    {
        get => SimulationPanelsAccess.Telemetry;
        set
        {
            SimulationPanelsAccess.Telemetry = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowTopView
    {
        get => SimulationPanelsAccess.TopView;
        set
        {
            SimulationPanelsAccess.TopView = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowView2D
    {
        get => SimulationPanelsAccess.View2D;
        set
        {
            SimulationPanelsAccess.View2D = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowView2DFact
    {
        get => SimulationPanelsAccess.View2DFact;
        set
        {
            SimulationPanelsAccess.View2DFact = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowView2DPair
    {
        get => SimulationPanelsAccess.View2DPair;
        set
        {
            SimulationPanelsAccess.View2DPair = value;
            RaisePropertyChanged();
        }
    }

    public bool SimAllowView3D
    {
        get => SimulationPanelsAccess.View3D;
        set
        {
            SimulationPanelsAccess.View3D = value;
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
            RaisePropertyChanged(nameof(LogModeDescription));
        }
    }

    public string LogFilePath => Path.Combine(_settings.Settings.LogsFolder, $"{DateTime.Now:dd.MM.yyyy}.log");

    public string LogModeDescription => LogMode switch
    {
        LogSeverity.Info => "Info: пишутся все сообщения (инфо, предупреждения и ошибки).",
        LogSeverity.Warning => "Warning: пишутся только предупреждения и ошибки.",
        LogSeverity.Error => "Error: пишутся только ошибки.",
        _ => "Info: пишутся все сообщения (инфо, предупреждения и ошибки)."
    };

    public bool CreateSampleRecipe()
    {
        return _createSampleRecipe();
    }

    public void ResetSelectedSectionToDefaults()
    {
        var defaults = CreateDefaultSettings();

        switch (SelectedSection)
        {
            case SectionStorage:
                _settings.Settings.RecipesFolder = defaults.RecipesFolder;
                _settings.Settings.AutoCreateSampleRecipeOnEmpty = defaults.AutoCreateSampleRecipeOnEmpty;
                RaisePropertyChanged(nameof(RecipesFolder));
                RaisePropertyChanged(nameof(DatabaseFilePath));
                RaisePropertyChanged(nameof(AutoCreateSampleRecipeOnEmpty));
                break;

            case SectionMachine:
                _settings.Settings.HZone = defaults.HZone;
                _settings.Settings.HContMax = defaults.HContMax;
                _settings.Settings.HBokMax = defaults.HBokMax;
                _settings.Settings.HFreeZ = defaults.HFreeZ;
                _settings.Settings.Xm = defaults.Xm;
                _settings.Settings.Ym = defaults.Ym;
                _settings.Settings.Zm = defaults.Zm;
                _settings.Settings.Lz = defaults.Lz;
                _settings.Settings.NozzleOrientationMode = defaults.NozzleOrientationMode;
                _settings.Settings.AlfaMinDeg = defaults.AlfaMinDeg;
                _settings.Settings.AlfaMaxDeg = defaults.AlfaMaxDeg;
                _settings.Settings.BettaMinDeg = defaults.BettaMinDeg;
                _settings.Settings.BettaMaxDeg = defaults.BettaMaxDeg;
                RaisePropertyChanged(nameof(HZone));
                RaisePropertyChanged(nameof(HContMax));
                RaisePropertyChanged(nameof(HBokMax));
                RaisePropertyChanged(nameof(HFreeZ));
                RaisePropertyChanged(nameof(Xm));
                RaisePropertyChanged(nameof(Ym));
                RaisePropertyChanged(nameof(Zm));
                RaisePropertyChanged(nameof(Lz));
                RaisePropertyChanged(nameof(NozzleOrientationMode));
                RaisePropertyChanged(nameof(NozzleOrientationModeDescription));
                RaisePropertyChanged(nameof(AlfaMinDeg));
                RaisePropertyChanged(nameof(AlfaMaxDeg));
                RaisePropertyChanged(nameof(BettaMinDeg));
                RaisePropertyChanged(nameof(BettaMaxDeg));
                break;

            case SectionCoefficients:
                _settings.Settings.PulseX = defaults.PulseX;
                _settings.Settings.PulseY = defaults.PulseY;
                _settings.Settings.PulseZ = defaults.PulseZ;
                _settings.Settings.PulseA = defaults.PulseA;
                _settings.Settings.PulseB = defaults.PulseB;
                _settings.Settings.PulseTop = defaults.PulseTop;
                _settings.Settings.PulseLow = defaults.PulseLow;
                _settings.Settings.PulseClamp = defaults.PulseClamp;
                RaisePropertyChanged(nameof(PulseX));
                RaisePropertyChanged(nameof(PulseY));
                RaisePropertyChanged(nameof(PulseZ));
                RaisePropertyChanged(nameof(PulseA));
                RaisePropertyChanged(nameof(PulseB));
                RaisePropertyChanged(nameof(PulseTop));
                RaisePropertyChanged(nameof(PulseLow));
                RaisePropertyChanged(nameof(PulseClamp));
                break;

            case SectionGraphics:
                _settings.Settings.PlotOpacity = defaults.PlotOpacity;
                _settings.Settings.PlotStrokeThickness = defaults.PlotStrokeThickness;
                _settings.Settings.PlotPointRadius = defaults.PlotPointRadius;
                _settings.Settings.PlotShowPolyline = defaults.PlotShowPolyline;
                _settings.Settings.PlotShowSmooth = defaults.PlotShowSmooth;
                _settings.Settings.PlotShowTargetPoints = defaults.PlotShowTargetPoints;
                _settings.Settings.PlotEnableDrag = defaults.PlotEnableDrag;
                _settings.Settings.PlotColorWorkingZone = defaults.PlotColorWorkingZone;
                _settings.Settings.PlotColorSafetyZone = defaults.PlotColorSafetyZone;
                _settings.Settings.PlotColorRobotPath = defaults.PlotColorRobotPath;
                _settings.Settings.PlotColorPairLinks = defaults.PlotColorPairLinks;
                _settings.Settings.PlotColorTool = defaults.PlotColorTool;
                _settings.Settings.SmoothSegmentsPerSpan = defaults.SmoothSegmentsPerSpan;
                RaisePropertyChanged(nameof(PlotOpacity));
                RaisePropertyChanged(nameof(PlotOpacityDisplay));
                RaisePropertyChanged(nameof(PlotStrokeThickness));
                RaisePropertyChanged(nameof(PlotPointRadius));
                RaisePropertyChanged(nameof(PlotShowPolyline));
                RaisePropertyChanged(nameof(PlotShowSmooth));
                RaisePropertyChanged(nameof(PlotShowTargetPoints));
                RaisePropertyChanged(nameof(PlotEnableDrag));
                RaisePropertyChanged(nameof(PlotColorWorkingZone));
                RaisePropertyChanged(nameof(PlotColorSafetyZone));
                RaisePropertyChanged(nameof(PlotColorRobotPath));
                RaisePropertyChanged(nameof(PlotColorPairLinks));
                RaisePropertyChanged(nameof(PlotColorTool));
                RaisePropertyChanged(nameof(SmoothSegmentsPerSpan));
                break;

            case SectionSimulation:
                _settings.Settings.SimulationPanels.Access = new SimulationPanelsAccessSettings
                {
                    Plot = defaults.SimulationPanels.Access.Plot,
                    Telemetry = defaults.SimulationPanels.Access.Telemetry,
                    TopView = defaults.SimulationPanels.Access.TopView,
                    View2D = defaults.SimulationPanels.Access.View2D,
                    View2DFact = defaults.SimulationPanels.Access.View2DFact,
                    View2DPair = defaults.SimulationPanels.Access.View2DPair,
                    View3D = defaults.SimulationPanels.Access.View3D
                };
                RaisePropertyChanged(nameof(SimAllowPlot));
                RaisePropertyChanged(nameof(SimAllowTelemetry));
                RaisePropertyChanged(nameof(SimAllowTopView));
                RaisePropertyChanged(nameof(SimAllowView2D));
                RaisePropertyChanged(nameof(SimAllowView2DFact));
                RaisePropertyChanged(nameof(SimAllowView2DPair));
                RaisePropertyChanged(nameof(SimAllowView3D));
                break;

            case SectionLogging:
                _settings.Settings.LoggingEnabled = defaults.LoggingEnabled;
                _settings.Settings.LogRetentionDays = defaults.LogRetentionDays;
                _settings.Settings.LogsFolder = defaults.LogsFolder;
                _settings.Settings.LogMode = defaults.LogMode;
                RaisePropertyChanged(nameof(LoggingEnabled));
                RaisePropertyChanged(nameof(LogRetentionDays));
                RaisePropertyChanged(nameof(LogsFolder));
                RaisePropertyChanged(nameof(LogMode));
                RaisePropertyChanged(nameof(LogModeDescription));
                RaisePropertyChanged(nameof(LogFilePath));
                break;
        }
    }

    private void Save()
    {
        _settings.SaveSection(target => CopySelectedSection(_settings.Settings, target));
        SettingsSaved?.Invoke(CurrentSectionTitle);
        _onChanged();
        RaisePropertyChanged(nameof(SettingsFilePath));
        RaisePropertyChanged(nameof(DatabaseFilePath));
        RaisePropertyChanged(nameof(LogFilePath));
    }

    private void ResetToDefaults()
    {
        ResetSelectedSectionToDefaults();
    }

    private AppSettings CreateDefaultSettings()
    {
        var defaults = new AppSettings
        {
            RecipesFolder = Path.Combine(_settings.AppDataRoot, "recipes"),
            LogsFolder = Path.Combine(_settings.AppDataRoot, "logs"),
            LogMode = LogSeverity.Info
        };

        defaults.SimulationPanels.Access = new SimulationPanelsAccessSettings();
        return defaults;
    }

    private void CopySelectedSection(AppSettings source, AppSettings target)
    {
        switch (SelectedSection)
        {
            case SectionStorage:
                target.RecipesFolder = source.RecipesFolder;
                target.AutoCreateSampleRecipeOnEmpty = source.AutoCreateSampleRecipeOnEmpty;
                break;

            case SectionMachine:
                target.HZone = source.HZone;
                target.HContMax = source.HContMax;
                target.HBokMax = source.HBokMax;
                target.HFreeZ = source.HFreeZ;
                target.Xm = source.Xm;
                target.Ym = source.Ym;
                target.Zm = source.Zm;
                target.Lz = source.Lz;
                target.NozzleOrientationMode = source.NozzleOrientationMode;
                target.AlfaMinDeg = source.AlfaMinDeg;
                target.AlfaMaxDeg = source.AlfaMaxDeg;
                target.BettaMinDeg = source.BettaMinDeg;
                target.BettaMaxDeg = source.BettaMaxDeg;
                break;

            case SectionCoefficients:
                target.PulseX = source.PulseX;
                target.PulseY = source.PulseY;
                target.PulseZ = source.PulseZ;
                target.PulseA = source.PulseA;
                target.PulseB = source.PulseB;
                target.PulseTop = source.PulseTop;
                target.PulseLow = source.PulseLow;
                target.PulseClamp = source.PulseClamp;
                break;

            case SectionGraphics:
                target.PlotOpacity = source.PlotOpacity;
                target.PlotStrokeThickness = source.PlotStrokeThickness;
                target.PlotPointRadius = source.PlotPointRadius;
                target.PlotShowPolyline = source.PlotShowPolyline;
                target.PlotShowSmooth = source.PlotShowSmooth;
                target.PlotShowTargetPoints = source.PlotShowTargetPoints;
                target.PlotEnableDrag = source.PlotEnableDrag;
                target.PlotColorWorkingZone = source.PlotColorWorkingZone;
                target.PlotColorSafetyZone = source.PlotColorSafetyZone;
                target.PlotColorRobotPath = source.PlotColorRobotPath;
                target.PlotColorPairLinks = source.PlotColorPairLinks;
                target.PlotColorTool = source.PlotColorTool;
                target.SmoothSegmentsPerSpan = source.SmoothSegmentsPerSpan;
                break;

            case SectionSimulation:
                source.SimulationPanels ??= new SimulationPanelsSettings();
                source.SimulationPanels.Access ??= new SimulationPanelsAccessSettings();
                target.SimulationPanels ??= new SimulationPanelsSettings();
                target.SimulationPanels.Access ??= new SimulationPanelsAccessSettings();
                target.SimulationPanels.Access.Plot = source.SimulationPanels.Access.Plot;
                target.SimulationPanels.Access.Telemetry = source.SimulationPanels.Access.Telemetry;
                target.SimulationPanels.Access.TopView = source.SimulationPanels.Access.TopView;
                target.SimulationPanels.Access.View2D = source.SimulationPanels.Access.View2D;
                target.SimulationPanels.Access.View2DFact = source.SimulationPanels.Access.View2DFact;
                target.SimulationPanels.Access.View2DPair = source.SimulationPanels.Access.View2DPair;
                target.SimulationPanels.Access.View3D = source.SimulationPanels.Access.View3D;
                break;

            case SectionLogging:
                target.LoggingEnabled = source.LoggingEnabled;
                target.LogRetentionDays = source.LogRetentionDays;
                target.LogsFolder = source.LogsFolder;
                target.LogMode = source.LogMode;
                break;
        }
    }

    private void RaiseAllSettingsPropertiesChanged()
    {
        RaisePropertyChanged(nameof(RecipesFolder));
        RaisePropertyChanged(nameof(DatabaseFilePath));
        RaisePropertyChanged(nameof(AutoCreateSampleRecipeOnEmpty));

        RaisePropertyChanged(nameof(HZone));
        RaisePropertyChanged(nameof(HContMax));
        RaisePropertyChanged(nameof(HBokMax));
        RaisePropertyChanged(nameof(HFreeZ));
        RaisePropertyChanged(nameof(Xm));
        RaisePropertyChanged(nameof(Ym));
        RaisePropertyChanged(nameof(Zm));
        RaisePropertyChanged(nameof(Lz));
        RaisePropertyChanged(nameof(NozzleOrientationMode));
        RaisePropertyChanged(nameof(NozzleOrientationModeDescription));
        RaisePropertyChanged(nameof(AlfaMinDeg));
        RaisePropertyChanged(nameof(AlfaMaxDeg));
        RaisePropertyChanged(nameof(BettaMinDeg));
        RaisePropertyChanged(nameof(BettaMaxDeg));

        RaisePropertyChanged(nameof(PulseX));
        RaisePropertyChanged(nameof(PulseY));
        RaisePropertyChanged(nameof(PulseZ));
        RaisePropertyChanged(nameof(PulseA));
        RaisePropertyChanged(nameof(PulseB));
        RaisePropertyChanged(nameof(PulseTop));
        RaisePropertyChanged(nameof(PulseLow));
        RaisePropertyChanged(nameof(PulseClamp));

        RaisePropertyChanged(nameof(PlotOpacity));
        RaisePropertyChanged(nameof(PlotOpacityDisplay));
        RaisePropertyChanged(nameof(PlotStrokeThickness));
        RaisePropertyChanged(nameof(PlotPointRadius));
        RaisePropertyChanged(nameof(PlotShowPolyline));
        RaisePropertyChanged(nameof(PlotShowSmooth));
        RaisePropertyChanged(nameof(PlotShowTargetPoints));
        RaisePropertyChanged(nameof(PlotEnableDrag));
        RaisePropertyChanged(nameof(PlotColorWorkingZone));
        RaisePropertyChanged(nameof(PlotColorSafetyZone));
        RaisePropertyChanged(nameof(PlotColorRobotPath));
        RaisePropertyChanged(nameof(PlotColorPairLinks));
        RaisePropertyChanged(nameof(PlotColorTool));
        RaisePropertyChanged(nameof(SmoothSegmentsPerSpan));

        RaisePropertyChanged(nameof(SimAllowPlot));
        RaisePropertyChanged(nameof(SimAllowTelemetry));
        RaisePropertyChanged(nameof(SimAllowTopView));
        RaisePropertyChanged(nameof(SimAllowView2D));
        RaisePropertyChanged(nameof(SimAllowView2DFact));
        RaisePropertyChanged(nameof(SimAllowView2DPair));
        RaisePropertyChanged(nameof(SimAllowView3D));

        RaisePropertyChanged(nameof(LoggingEnabled));
        RaisePropertyChanged(nameof(LogRetentionDays));
        RaisePropertyChanged(nameof(LogsFolder));
        RaisePropertyChanged(nameof(LogMode));
        RaisePropertyChanged(nameof(LogModeDescription));
        RaisePropertyChanged(nameof(LogFilePath));
        RaisePropertyChanged(nameof(SettingsFilePath));
    }

    public sealed record NozzleOrientationModeOption(string Value, string Label, string Description);
}
