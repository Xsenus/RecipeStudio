using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecipeStudio.Desktop.Services;

public sealed class AppSettings
{
    public string RecipesFolder { get; set; } = "";
    public bool AutoCreateSampleRecipeOnEmpty { get; set; } = false;

    // Constants from CONST sheet (default values from the provided workbook)
    public double HZone { get; set; } = 1200;
    public double HContMax { get; set; } = 500;
    public double HBokMax { get; set; } = 100;
    public double HFreeZ { get; set; } = 800;

    public double Xm { get; set; } = -2456;
    public double Ym { get; set; } = -1223;
    public double Zm { get; set; } = 423;

    public double Lz { get; set; } = 250;
    public string NozzleOrientationMode { get; set; } = NozzleOrientationModes.PhysicalAngles;
    public double AlfaMinDeg { get; set; } = -90;
    public double AlfaMaxDeg { get; set; } = 90;
    public double BettaMinDeg { get; set; } = -15;
    public double BettaMaxDeg { get; set; } = 15;

    public double PulseX { get; set; } = 50;
    public double PulseY { get; set; } = 50;
    public double PulseZ { get; set; } = 50;
    public double PulseA { get; set; } = 5;
    public double PulseB { get; set; } = 5;
    public double PulseTop { get; set; } = 514 * 200;
    public double PulseLow { get; set; } = 514 * 200;
    public double PulseClamp { get; set; } = 50;

    // Plot settings
    public double PlotStrokeThickness { get; set; } = 3;
    public double PlotOpacity { get; set; } = 0.55; // requested: configurable in 0.05..0.90
    public double PlotPointRadius { get; set; } = 4;
    public bool PlotShowPolyline { get; set; } = true;
    public bool PlotShowSmooth { get; set; } = false;
    public bool PlotShowTargetPoints { get; set; } = true;
    public bool PlotEnableDrag { get; set; } = true;

    // Plot colors (hex: #RRGGBB or #AARRGGBB)
    public string PlotColorWorkingZone { get; set; } = "#22C55E";
    public string PlotColorSafetyZone { get; set; } = "#9CA3AF";
    public string PlotColorRobotPath { get; set; } = "#F59E0B";
    public string PlotColorPairLinks { get; set; } = "#FB923C";
    public string PlotColorTool { get; set; } = "#EF4444";

    public int SmoothSegmentsPerSpan { get; set; } = 16;

    public WindowPlacementSettings WindowPlacement { get; set; } = new();
    public EditorPanelsSettings EditorPanels { get; set; } = new();
    public SimulationPanelsSettings SimulationPanels { get; set; } = new();
    public List<EditorGridColumnWidthSettings> EditorGridColumns { get; set; } = new();

    // Backward compatibility: old numeric-only format.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? EditorGridColumnWidths { get; set; }

    // Logging
    public bool LoggingEnabled { get; set; } = true;
    public int LogRetentionDays { get; set; } = 14;
    public string LogsFolder { get; set; } = "";
    public string LogMode { get; set; } = LogSeverity.Info;
}

public static class LogSeverity
{
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

public sealed class WindowPlacementSettings
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool IsMaximized { get; set; }
}

public sealed class EditorPanelsSettings
{
    public PanelPlacementSettings Parameters { get; set; } = new();
    public PanelPlacementSettings Visualization { get; set; } = new();
    public PanelPlacementSettings SelectedPoint { get; set; } = new();
}

public sealed class PanelPlacementSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class SimulationPanelsSettings
{
    public PanelPlacementSettings Plot { get; set; } = new();
    public PanelPlacementSettings Telemetry { get; set; } = new();
    public PanelPlacementSettings TopView { get; set; } = new() { IsVisible = false };
    public PanelPlacementSettings View2D { get; set; } = new() { IsVisible = false };
    public PanelPlacementSettings View2DFact { get; set; } = new() { IsVisible = false };
    public PanelPlacementSettings View2DPair { get; set; } = new() { IsVisible = true };
    public PanelPlacementSettings View3D { get; set; } = new() { IsVisible = false };
    public SimulationPanelsAccessSettings Access { get; set; } = new();
    public Simulation2DCalibrationSettings Calibration2D { get; set; } = new();
}

public sealed class SimulationPanelsAccessSettings
{
    public bool Plot { get; set; } = true;
    public bool Telemetry { get; set; } = true;
    public bool TopView { get; set; } = false;
    public bool View2D { get; set; } = false;
    public bool View2DFact { get; set; } = false;
    public bool View2DPair { get; set; } = true;
    public bool View3D { get; set; } = false;
    public bool ShowCalibrationControls { get; set; } = false;
}

public sealed class Simulation2DCalibrationSettings
{
    public double ReferenceHeightMm { get; set; } = 1361.8696;
    public double VerticalOffsetMm { get; set; } = 195.0;
    public double HorizontalOffsetMm { get; set; } = -55.0;
    public double PartWidthScalePercent { get; set; } = 98.0;
    public double ManipulatorAnchorX { get; set; } = 0.04;
    public double ManipulatorAnchorY { get; set; } = 0.90;
    public bool ReversePath { get; set; }
}

public sealed class EditorGridColumnWidthSettings
{
    public string Name { get; set; } = "";
    public double Width { get; set; }
}
