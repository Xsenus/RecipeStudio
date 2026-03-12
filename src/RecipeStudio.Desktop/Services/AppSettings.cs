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
    public string CalculationOriginMode { get; set; } = CalculationOriginModes.ExcelFirstRow;
    public string ANozzleKinematicsMode { get; set; } = ANozzleKinematicsModes.ExcelFirstRow;
    public string VelocityCalculationMode { get; set; } = VelocityCalculationModes.ExcelExact;
    public string TopLowPulseMode { get; set; } = TopLowPulseModes.ExcelLinked;
    public string ExcelExportMode { get; set; } = ExcelExportModes.Workbook;
    public string RecommendedFlowBulkMode { get; set; } = RecommendedFlowBulkModes.Disabled;
    public string RecommendedAlfaMode { get; set; } = RecommendedAlfaModes.Plus90;
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
    public string PlotColorProfileGroup1 { get; set; } = "#2E5C93";
    public string PlotColorProfileGroup2 { get; set; } = "#7E879C";
    public string PlotColorProfileGroup3 { get; set; } = "#7E879C";
    public string PlotColorProfileGroup4 { get; set; } = "#2E5C93";
    public string PlotColorProfileB0Path { get; set; } = "#F59E0B";
    public string PlotColorProfileSegmentA { get; set; } = "#2E5C93";
    public string PlotColorProfileSegmentB { get; set; } = "#EF4444";
    public bool PlotProfileUsePythonViewport { get; set; } = false;
    public double PlotProfileViewportMinX { get; set; } = -400;
    public double PlotProfileViewportMinY { get; set; } = 0;
    public double PlotProfileViewportWidth { get; set; } = 800;
    public double PlotProfileViewportHeight { get; set; } = 1200;
    public bool PlotProfileShowGroup1 { get; set; } = true;
    public bool PlotProfileShowGroup2 { get; set; } = true;
    public bool PlotProfileShowGroup3 { get; set; } = true;
    public bool PlotProfileShowGroup4 { get; set; } = true;
    public bool PlotProfileShowGroupCurves { get; set; } = true;
    public bool PlotProfileShowGroupPoints { get; set; } = true;
    public bool PlotProfileShowGroupPointLabels { get; set; } = true;
    public bool PlotProfileShowBSegmentFootprints { get; set; } = true;
    public bool PlotProfileShowA1FrameCloud { get; set; } = true;
    public bool PlotProfileShowB0FrameCloud { get; set; } = true;
    public bool PlotProfileShowA1Points { get; set; } = true;
    public bool PlotProfileShowA1Labels { get; set; } = true;
    public bool PlotProfileShowB0PathLine { get; set; } = false;
    public bool PlotProfileShowB0Points { get; set; } = true;
    public bool PlotProfileShowB0Labels { get; set; } = true;
    public bool PlotProfileInfoBoxVisible { get; set; } = true;
    public string PlotProfileInfoBoxBackground { get; set; } = "#F3F4F6";
    public string PlotProfileInfoBoxBorder { get; set; } = "#4B5563";
    public string PlotProfileInfoBoxTextColor { get; set; } = "#111827";
    public double PlotProfileInfoBoxOpacity { get; set; } = 0.92;
    public double PlotProfileInfoBoxFontSize { get; set; } = 12;
    public string PlotProfileInfoBoxFontFamily { get; set; } = "Segoe UI";
    public bool PlotProfileInfoBoxFollowA0 { get; set; } = true;
    public double PlotProfileInfoBoxManualX { get; set; } = 0.62;
    public double PlotProfileInfoBoxManualY { get; set; } = 0.46;

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
    public PanelPlacementSettings Pair2D { get; set; } = new();
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
    public bool TargetViewMirrored { get; set; } = true;
    public string PlotTargetDisplayMode { get; set; } = SimulationTargetDisplayModes.Full;
    public string PlotTargetDisplaySide { get; set; } = SimulationTargetDisplayModes.Original;
    public string View2DPairTargetDisplayMode { get; set; } = SimulationTargetDisplayModes.Full;
    public string View2DPairTargetDisplaySide { get; set; } = SimulationTargetDisplayModes.Original;
    public bool View2DPairShowRedLink { get; set; } = true;
    public string SpriteVersion { get; set; } = SimulationSpriteVersions.Version2;
    public SimulationPanelsAccessSettings Access { get; set; } = new();
    public Simulation2DCalibrationSettings Calibration2D { get; set; } = new();
}

public static class SimulationTargetDisplayModes
{
    public const string Original = "original";
    public const string Mirrored = "mirrored";
    public const string Full = "full";

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Original, System.StringComparison.OrdinalIgnoreCase))
            return Original;

        if (string.Equals(value, Mirrored, System.StringComparison.OrdinalIgnoreCase))
            return Mirrored;

        return Full;
    }

    public static string NormalizeCoverage(string? value)
        => Normalize(value) == Full ? Full : Original;

    public static string NormalizeSide(string? value, string? mode = null)
    {
        if (string.Equals(value, Mirrored, System.StringComparison.OrdinalIgnoreCase))
            return Mirrored;

        if (string.Equals(value, Original, System.StringComparison.OrdinalIgnoreCase))
            return Original;

        return Normalize(mode) == Mirrored ? Mirrored : Original;
    }
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
    public double ManipulatorAnchorX { get; set; } = SimulationSpriteAnchors.ManipulatorPivotAnchorX;
    public double ManipulatorAnchorY { get; set; } = SimulationSpriteAnchors.ManipulatorPivotAnchorY;
    public bool ReversePath { get; set; }
}

public sealed class EditorGridColumnWidthSettings
{
    public string Name { get; set; } = "";
    public double Width { get; set; }
}
