namespace RecipeStudio.Desktop.Services;

public sealed class AppSettings
{
    public string RecipesFolder { get; set; } = "";

    // Constants from CONST sheet (default values from the provided workbook)
    public double HZone { get; set; } = 1200;
    public double HContMax { get; set; } = 500;
    public double HBokMax { get; set; } = 100;

    public double Xm { get; set; } = -2456;
    public double Ym { get; set; } = -1223;
    public double Zm { get; set; } = 423;

    public double Lz { get; set; } = 250;

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
    public bool PlotShowSmooth { get; set; } = true;
    public bool PlotShowTargetPoints { get; set; } = true;
    public bool PlotEnableDrag { get; set; } = true;

    public int SmoothSegmentsPerSpan { get; set; } = 16;
}
