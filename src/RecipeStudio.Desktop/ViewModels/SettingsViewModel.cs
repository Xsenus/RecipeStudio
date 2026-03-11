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
    private bool _isExcelCompatibilityVisible;

    public SettingsViewModel(SettingsService settings, Action onChanged, Func<bool> createSampleRecipe)
    {
        _settings = settings;
        _onChanged = onChanged;
        _createSampleRecipe = createSampleRecipe;

        SaveCommand = new RelayCommand(Save);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        CreateSampleRecipeCommand = new RelayCommand(() => RequestCreateSampleRecipe?.Invoke());
        ToggleExcelCompatibilityCommand = new RelayCommand(ToggleExcelCompatibilityVisibility);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetToDefaultsCommand { get; }
    public RelayCommand CreateSampleRecipeCommand { get; }
    public RelayCommand ToggleExcelCompatibilityCommand { get; }

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
    public bool IsExcelCompatibilityVisible
    {
        get => _isExcelCompatibilityVisible;
        private set => SetProperty(ref _isExcelCompatibilityVisible, value);
    }

    public string CurrentSectionTitle => SelectedSection switch
    {
        SectionStorage => "Хранилище рецептов",
        SectionMachine => "Параметры станка",
        SectionCoefficients => "Коэффициенты и импульсы",
        SectionGraphics => "Графика",
        SectionSimulation => "Симуляция",
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

    public IReadOnlyList<RecommendedAlfaModeOption> RecommendedAlfaModeOptions { get; } = new[]
    {
        new RecommendedAlfaModeOption(
            RecommendedAlfaModes.Plus90,
            "Формула +90",
            "RecommendedAlfa считается как round(-atan(dZ/dR)) + 90. Это текущее поведение по умолчанию."),
        new RecommendedAlfaModeOption(
            RecommendedAlfaModes.Minus90,
            "Формула 90-",
            "RecommendedAlfa считается как 90 - round(-atan(dZ/dR)). Подходит для альтернативной трактовки угла.")
    };

    public string RecommendedAlfaMode
    {
        get => RecommendedAlfaModes.Normalize(_settings.Settings.RecommendedAlfaMode);
        set
        {
            _settings.Settings.RecommendedAlfaMode = RecommendedAlfaModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(RecommendedAlfaModeDescription));
            RaisePropertyChanged(nameof(RecommendedAlfaCompatibilityDescription));
        }
    }

    public string RecommendedAlfaModeDescription
        => RecommendedAlfaModeOptions.FirstOrDefault(x => x.Value == RecommendedAlfaMode)?.Description
           ?? "RecommendedAlfa считается по формуле round(-atan(dZ/dR)) + 90.";

    public IReadOnlyList<CompatibilityModeOption> RecommendedAlfaCompatibilityOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            RecommendedAlfaModes.Plus90,
            "Как в Excel",
            "RecommendedAlfa считается по формуле round(-atan(dZ/dR)) + 90. Этот режим включен по умолчанию."),
        new CompatibilityModeOption(
            RecommendedAlfaModes.Minus90,
            "Как у нас",
            "RecommendedAlfa считается по формуле 90 - round(-atan(dZ/dR)). Это альтернативная локальная трактовка угла.")
    };

    public string RecommendedAlfaCompatibilityDescription
        => RecommendedAlfaCompatibilityOptions.FirstOrDefault(x => x.Value == RecommendedAlfaMode)?.Description
           ?? "RecommendedAlfa считается по формуле round(-atan(dZ/dR)) + 90.";

    public void ToggleExcelCompatibilityVisibility()
        => IsExcelCompatibilityVisible = !IsExcelCompatibilityVisible;

    public IReadOnlyList<CompatibilityModeOption> CalculationOriginModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            CalculationOriginModes.ExcelFirstRow,
            "Как в Excel",
            "База Xr0/Yx0/Zr0 берется всегда из первой строки рецепта. Это повторяет workbook и может изменить dX/dY/dZ, если в начале есть служебные строки."),
        new CompatibilityModeOption(
            CalculationOriginModes.CurrentFirstWorking,
            "Как у нас",
            "База расчета берется из первой рабочей точки с Act=1 и Safe=0. Это устойчивее к служебным строкам, но не совпадает один в один с workbook.")
    };

    public string CalculationOriginMode
    {
        get => CalculationOriginModes.Normalize(_settings.Settings.CalculationOriginMode);
        set
        {
            _settings.Settings.CalculationOriginMode = CalculationOriginModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CalculationOriginModeDescription));
        }
    }

    public string CalculationOriginModeDescription
        => CalculationOriginModeOptions.FirstOrDefault(x => x.Value == CalculationOriginMode)?.Description
           ?? "База Xr0/Yx0/Zr0 берется из первой строки рецепта.";

    public IReadOnlyList<CompatibilityModeOption> ANozzleKinematicsModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            ANozzleKinematicsModes.ExcelFirstRow,
            "Как в Excel",
            "Во всей кинематике используется a_nozzle первой строки. Это повторяет CALC workbook."),
        new CompatibilityModeOption(
            ANozzleKinematicsModes.CurrentPerPoint,
            "Как у нас",
            "Каждая точка использует свой a_nozzle. Это позволяет менять длину сопла по траектории.")
    };

    public string ANozzleKinematicsMode
    {
        get => ANozzleKinematicsModes.Normalize(_settings.Settings.ANozzleKinematicsMode);
        set
        {
            _settings.Settings.ANozzleKinematicsMode = ANozzleKinematicsModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ANozzleKinematicsModeDescription));
        }
    }

    public string ANozzleKinematicsModeDescription
        => ANozzleKinematicsModeOptions.FirstOrDefault(x => x.Value == ANozzleKinematicsMode)?.Description
           ?? "Во всей кинематике используется a_nozzle первой строки.";

    public IReadOnlyList<CompatibilityModeOption> VelocityCalculationModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            VelocityCalculationModes.ExcelExact,
            "Как в Excel",
            "Vel считается без округления. Это точнее повторяет UI workbook и влияет на точность рекомендованного Flow."),
        new CompatibilityModeOption(
            VelocityCalculationModes.CurrentRounded,
            "Как у нас",
            "Vel округляется до целого мм/мин. Это повторяет текущее поведение приложения.")
    };

    public string VelocityCalculationMode
    {
        get => VelocityCalculationModes.Normalize(_settings.Settings.VelocityCalculationMode);
        set
        {
            _settings.Settings.VelocityCalculationMode = VelocityCalculationModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(VelocityCalculationModeDescription));
        }
    }

    public string VelocityCalculationModeDescription
        => VelocityCalculationModeOptions.FirstOrDefault(x => x.Value == VelocityCalculationMode)?.Description
           ?? "Vel считается без округления.";

    public IReadOnlyList<CompatibilityModeOption> TopLowPulseModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            TopLowPulseModes.ExcelLinked,
            "Как в Excel",
            "Top_puls принудительно берется из Low_puls. Отдельный PulseTop в расчете не используется."),
        new CompatibilityModeOption(
            TopLowPulseModes.CurrentIndependent,
            "Как у нас",
            "Top_puls и Low_puls считаются независимо по PulseTop и PulseLow.")
    };

    public string TopLowPulseMode
    {
        get => TopLowPulseModes.Normalize(_settings.Settings.TopLowPulseMode);
        set
        {
            _settings.Settings.TopLowPulseMode = TopLowPulseModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(TopLowPulseModeDescription));
        }
    }

    public string TopLowPulseModeDescription
        => TopLowPulseModeOptions.FirstOrDefault(x => x.Value == TopLowPulseMode)?.Description
           ?? "Top_puls берется из Low_puls.";

    public IReadOnlyList<CompatibilityModeOption> ExcelExportModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            ExcelExportModes.Workbook,
            "Как в Excel",
            "Экспортирует книгу UI/CALC/CONST/SAVE. Подходит для обмена с исходным workbook."),
        new CompatibilityModeOption(
            ExcelExportModes.FlatPoints,
            "Как у нас",
            "Экспортирует один плоский лист Points с данными рецепта.")
    };

    public string ExcelExportMode
    {
        get => ExcelExportModes.Normalize(_settings.Settings.ExcelExportMode);
        set
        {
            _settings.Settings.ExcelExportMode = ExcelExportModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ExcelExportModeDescription));
        }
    }

    public string ExcelExportModeDescription
        => ExcelExportModeOptions.FirstOrDefault(x => x.Value == ExcelExportMode)?.Description
           ?? "Экспортирует книгу UI/CALC/CONST/SAVE.";

    public IReadOnlyList<CompatibilityModeOption> RecommendedFlowBulkModeOptions { get; } = new[]
    {
        new CompatibilityModeOption(
            RecommendedFlowBulkModes.Disabled,
            "Как в Excel",
            "Массовое применение рекомендованного расхода отключено. В workbook такого действия нет."),
        new CompatibilityModeOption(
            RecommendedFlowBulkModes.IceRateHeader,
            "Как у нас",
            "По клику на хедер Ice.G можно подтвердить заполнение IceRate текущими значениями RecommendedIceRate для всех точек.")
    };

    public string RecommendedFlowBulkMode
    {
        get => RecommendedFlowBulkModes.Normalize(_settings.Settings.RecommendedFlowBulkMode);
        set
        {
            _settings.Settings.RecommendedFlowBulkMode = RecommendedFlowBulkModes.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(RecommendedFlowBulkModeDescription));
        }
    }

    public string RecommendedFlowBulkModeDescription
        => RecommendedFlowBulkModeOptions.FirstOrDefault(x => x.Value == RecommendedFlowBulkMode)?.Description
           ?? "Массовое применение рекомендованного расхода отключено.";

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

    public IReadOnlyList<SimulationSpriteVersionOption> SimulationSpriteVersionOptions { get; } = new[]
    {
        new SimulationSpriteVersionOption(SimulationSpriteVersions.Version2, "v2"),
        new SimulationSpriteVersionOption(SimulationSpriteVersions.Version1, "v1")
    };

    public string SimSpriteVersion
    {
        get => SimulationSpriteVersions.Normalize(_settings.Settings.SimulationPanels.SpriteVersion);
        set
        {
            _settings.Settings.SimulationPanels.SpriteVersion = SimulationSpriteVersions.Normalize(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SimSpriteVersionDescription));
        }
    }

    public string SimSpriteVersionDescription
        => SimSpriteVersion == SimulationSpriteVersions.Version1
            ? "Используются изображения из папки v1."
            : "Используются изображения из папки v2. Это значение по умолчанию.";

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

    public bool SimShowView2DPairRedLink
    {
        get => _settings.Settings.SimulationPanels.View2DPairShowRedLink;
        set
        {
            _settings.Settings.SimulationPanels.View2DPairShowRedLink = value;
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

    public bool SimShowCalibrationControls
    {
        get => SimulationPanelsAccess.ShowCalibrationControls;
        set
        {
            SimulationPanelsAccess.ShowCalibrationControls = value;
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
                _settings.Settings.CalculationOriginMode = defaults.CalculationOriginMode;
                _settings.Settings.ANozzleKinematicsMode = defaults.ANozzleKinematicsMode;
                _settings.Settings.VelocityCalculationMode = defaults.VelocityCalculationMode;
                _settings.Settings.TopLowPulseMode = defaults.TopLowPulseMode;
                _settings.Settings.ExcelExportMode = defaults.ExcelExportMode;
                _settings.Settings.RecommendedFlowBulkMode = defaults.RecommendedFlowBulkMode;
                _settings.Settings.RecommendedAlfaMode = defaults.RecommendedAlfaMode;
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
                RaisePropertyChanged(nameof(CalculationOriginMode));
                RaisePropertyChanged(nameof(CalculationOriginModeDescription));
                RaisePropertyChanged(nameof(ANozzleKinematicsMode));
                RaisePropertyChanged(nameof(ANozzleKinematicsModeDescription));
                RaisePropertyChanged(nameof(VelocityCalculationMode));
                RaisePropertyChanged(nameof(VelocityCalculationModeDescription));
                RaisePropertyChanged(nameof(TopLowPulseMode));
                RaisePropertyChanged(nameof(TopLowPulseModeDescription));
                RaisePropertyChanged(nameof(ExcelExportMode));
                RaisePropertyChanged(nameof(ExcelExportModeDescription));
                RaisePropertyChanged(nameof(RecommendedFlowBulkMode));
                RaisePropertyChanged(nameof(RecommendedFlowBulkModeDescription));
                RaisePropertyChanged(nameof(RecommendedAlfaMode));
                RaisePropertyChanged(nameof(RecommendedAlfaModeDescription));
                RaisePropertyChanged(nameof(RecommendedAlfaCompatibilityDescription));
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
                    View3D = defaults.SimulationPanels.Access.View3D,
                    ShowCalibrationControls = defaults.SimulationPanels.Access.ShowCalibrationControls
                };
                _settings.Settings.SimulationPanels.Calibration2D = new Simulation2DCalibrationSettings
                {
                    ReferenceHeightMm = defaults.SimulationPanels.Calibration2D.ReferenceHeightMm,
                    VerticalOffsetMm = defaults.SimulationPanels.Calibration2D.VerticalOffsetMm,
                    HorizontalOffsetMm = defaults.SimulationPanels.Calibration2D.HorizontalOffsetMm,
                    PartWidthScalePercent = defaults.SimulationPanels.Calibration2D.PartWidthScalePercent,
                    ManipulatorAnchorX = defaults.SimulationPanels.Calibration2D.ManipulatorAnchorX,
                    ManipulatorAnchorY = defaults.SimulationPanels.Calibration2D.ManipulatorAnchorY,
                    ReversePath = defaults.SimulationPanels.Calibration2D.ReversePath
                };
                _settings.Settings.SimulationPanels.TargetViewMirrored = defaults.SimulationPanels.TargetViewMirrored;
                _settings.Settings.SimulationPanels.PlotTargetDisplayMode = defaults.SimulationPanels.PlotTargetDisplayMode;
                _settings.Settings.SimulationPanels.PlotTargetDisplaySide = defaults.SimulationPanels.PlotTargetDisplaySide;
                _settings.Settings.SimulationPanels.View2DPairTargetDisplayMode = defaults.SimulationPanels.View2DPairTargetDisplayMode;
                _settings.Settings.SimulationPanels.View2DPairTargetDisplaySide = defaults.SimulationPanels.View2DPairTargetDisplaySide;
                _settings.Settings.SimulationPanels.View2DPairShowRedLink = defaults.SimulationPanels.View2DPairShowRedLink;
                _settings.Settings.SimulationPanels.SpriteVersion = defaults.SimulationPanels.SpriteVersion;
                RaisePropertyChanged(nameof(SimAllowPlot));
                RaisePropertyChanged(nameof(SimAllowTelemetry));
                RaisePropertyChanged(nameof(SimAllowTopView));
                RaisePropertyChanged(nameof(SimAllowView2D));
                RaisePropertyChanged(nameof(SimAllowView2DFact));
                RaisePropertyChanged(nameof(SimAllowView2DPair));
                RaisePropertyChanged(nameof(SimShowView2DPairRedLink));
                RaisePropertyChanged(nameof(SimAllowView3D));
                RaisePropertyChanged(nameof(SimShowCalibrationControls));
                RaisePropertyChanged(nameof(SimSpriteVersion));
                RaisePropertyChanged(nameof(SimSpriteVersionDescription));
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
        defaults.SimulationPanels.Calibration2D = new Simulation2DCalibrationSettings();
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
                target.CalculationOriginMode = source.CalculationOriginMode;
                target.ANozzleKinematicsMode = source.ANozzleKinematicsMode;
                target.VelocityCalculationMode = source.VelocityCalculationMode;
                target.TopLowPulseMode = source.TopLowPulseMode;
                target.ExcelExportMode = source.ExcelExportMode;
                target.RecommendedFlowBulkMode = source.RecommendedFlowBulkMode;
                target.RecommendedAlfaMode = source.RecommendedAlfaMode;
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
                source.SimulationPanels.Calibration2D ??= new Simulation2DCalibrationSettings();
                target.SimulationPanels ??= new SimulationPanelsSettings();
                target.SimulationPanels.Access ??= new SimulationPanelsAccessSettings();
                target.SimulationPanels.Calibration2D ??= new Simulation2DCalibrationSettings();
                target.SimulationPanels.Access.Plot = source.SimulationPanels.Access.Plot;
                target.SimulationPanels.Access.Telemetry = source.SimulationPanels.Access.Telemetry;
                target.SimulationPanels.Access.TopView = source.SimulationPanels.Access.TopView;
                target.SimulationPanels.Access.View2D = source.SimulationPanels.Access.View2D;
                target.SimulationPanels.Access.View2DFact = source.SimulationPanels.Access.View2DFact;
                target.SimulationPanels.Access.View2DPair = source.SimulationPanels.Access.View2DPair;
                target.SimulationPanels.Access.View3D = source.SimulationPanels.Access.View3D;
                target.SimulationPanels.Access.ShowCalibrationControls = source.SimulationPanels.Access.ShowCalibrationControls;
                target.SimulationPanels.Calibration2D.ReferenceHeightMm = source.SimulationPanels.Calibration2D.ReferenceHeightMm;
                target.SimulationPanels.Calibration2D.VerticalOffsetMm = source.SimulationPanels.Calibration2D.VerticalOffsetMm;
                target.SimulationPanels.Calibration2D.HorizontalOffsetMm = source.SimulationPanels.Calibration2D.HorizontalOffsetMm;
                target.SimulationPanels.Calibration2D.PartWidthScalePercent = source.SimulationPanels.Calibration2D.PartWidthScalePercent;
                target.SimulationPanels.Calibration2D.ManipulatorAnchorX = source.SimulationPanels.Calibration2D.ManipulatorAnchorX;
                target.SimulationPanels.Calibration2D.ManipulatorAnchorY = source.SimulationPanels.Calibration2D.ManipulatorAnchorY;
                target.SimulationPanels.Calibration2D.ReversePath = source.SimulationPanels.Calibration2D.ReversePath;
                target.SimulationPanels.TargetViewMirrored = source.SimulationPanels.TargetViewMirrored;
                target.SimulationPanels.PlotTargetDisplayMode = source.SimulationPanels.PlotTargetDisplayMode;
                target.SimulationPanels.PlotTargetDisplaySide = source.SimulationPanels.PlotTargetDisplaySide;
                target.SimulationPanels.View2DPairTargetDisplayMode = source.SimulationPanels.View2DPairTargetDisplayMode;
                target.SimulationPanels.View2DPairTargetDisplaySide = source.SimulationPanels.View2DPairTargetDisplaySide;
                target.SimulationPanels.View2DPairShowRedLink = source.SimulationPanels.View2DPairShowRedLink;
                target.SimulationPanels.SpriteVersion = SimulationSpriteVersions.Normalize(source.SimulationPanels.SpriteVersion);
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
        RaisePropertyChanged(nameof(CalculationOriginMode));
        RaisePropertyChanged(nameof(CalculationOriginModeDescription));
        RaisePropertyChanged(nameof(ANozzleKinematicsMode));
        RaisePropertyChanged(nameof(ANozzleKinematicsModeDescription));
        RaisePropertyChanged(nameof(VelocityCalculationMode));
        RaisePropertyChanged(nameof(VelocityCalculationModeDescription));
        RaisePropertyChanged(nameof(TopLowPulseMode));
        RaisePropertyChanged(nameof(TopLowPulseModeDescription));
        RaisePropertyChanged(nameof(ExcelExportMode));
        RaisePropertyChanged(nameof(ExcelExportModeDescription));
        RaisePropertyChanged(nameof(RecommendedFlowBulkMode));
        RaisePropertyChanged(nameof(RecommendedFlowBulkModeDescription));
        RaisePropertyChanged(nameof(RecommendedAlfaMode));
        RaisePropertyChanged(nameof(RecommendedAlfaModeDescription));
        RaisePropertyChanged(nameof(RecommendedAlfaCompatibilityDescription));
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
        RaisePropertyChanged(nameof(SimShowView2DPairRedLink));
        RaisePropertyChanged(nameof(SimAllowView3D));
        RaisePropertyChanged(nameof(SimShowCalibrationControls));
        RaisePropertyChanged(nameof(SimSpriteVersion));
        RaisePropertyChanged(nameof(SimSpriteVersionDescription));

        RaisePropertyChanged(nameof(LoggingEnabled));
        RaisePropertyChanged(nameof(LogRetentionDays));
        RaisePropertyChanged(nameof(LogsFolder));
        RaisePropertyChanged(nameof(LogMode));
        RaisePropertyChanged(nameof(LogModeDescription));
        RaisePropertyChanged(nameof(LogFilePath));
        RaisePropertyChanged(nameof(SettingsFilePath));
    }

    public sealed record NozzleOrientationModeOption(string Value, string Label, string Description);
    public sealed record CompatibilityModeOption(string Value, string Label, string Description);
    public sealed record RecommendedAlfaModeOption(string Value, string Label, string Description);
    public sealed record SimulationSpriteVersionOption(string Value, string Label);
}
