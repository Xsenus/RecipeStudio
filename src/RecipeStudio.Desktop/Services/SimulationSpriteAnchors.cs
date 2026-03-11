using System;

namespace RecipeStudio.Desktop.Services;

public static class SimulationSpriteAnchors
{
    // Anchors are measured from the black pivot markers baked into the PNG assets.
    public const double NozzleTipAnchorX = 0.04;
    public const double NozzlePivotAnchorX = 177.5 / 193.0;
    public const double NozzlePivotAnchorY = 14.95 / 30.0;

    public const double ManipulatorPivotAnchorX = 14.5 / 370.0;
    public const double ManipulatorPivotAnchorY = 248.5 / 262.0;

    public const double V2NozzlePivotAnchorX = 135.0 / 147.0;
    public const double V2NozzlePivotAnchorY = 12.0 / 24.0;
    public const double V2ManipulatorPivotAnchorX = 13.0 / 676.0;
    public const double V2ManipulatorPivotAnchorY = 595.0 / 606.0;

    public const double LegacyManipulatorPivotAnchorX = 0.04;
    public const double LegacyManipulatorPivotAnchorY = 0.90;

    public static double GetNozzleTipAnchorX(string? spriteVersion)
        => NozzleTipAnchorX;

    public static double GetNozzlePivotAnchorX(string? spriteVersion)
        => SimulationSpriteVersions.Normalize(spriteVersion) == SimulationSpriteVersions.Version2
            ? V2NozzlePivotAnchorX
            : NozzlePivotAnchorX;

    public static double GetNozzlePivotAnchorY(string? spriteVersion)
        => SimulationSpriteVersions.Normalize(spriteVersion) == SimulationSpriteVersions.Version2
            ? V2NozzlePivotAnchorY
            : NozzlePivotAnchorY;

    public static double GetManipulatorPivotAnchorX(string? spriteVersion)
        => SimulationSpriteVersions.Normalize(spriteVersion) == SimulationSpriteVersions.Version2
            ? V2ManipulatorPivotAnchorX
            : ManipulatorPivotAnchorX;

    public static double GetManipulatorPivotAnchorY(string? spriteVersion)
        => SimulationSpriteVersions.Normalize(spriteVersion) == SimulationSpriteVersions.Version2
            ? V2ManipulatorPivotAnchorY
            : ManipulatorPivotAnchorY;

    public static bool UsesManipulatorPivot(string? spriteVersion, double anchorX, double anchorY)
        => Math.Abs(anchorX - GetManipulatorPivotAnchorX(spriteVersion)) <= 1e-6
        && Math.Abs(anchorY - GetManipulatorPivotAnchorY(spriteVersion)) <= 1e-6;

    public static bool UsesDefaultManipulatorPivot(double anchorX, double anchorY)
        => UsesLegacyManipulatorPivot(anchorX, anchorY)
        || UsesManipulatorPivot(SimulationSpriteVersions.Version1, anchorX, anchorY)
        || UsesManipulatorPivot(SimulationSpriteVersions.Version2, anchorX, anchorY);

    public static bool UsesLegacyManipulatorPivot(double anchorX, double anchorY)
        => Math.Abs(anchorX - LegacyManipulatorPivotAnchorX) <= 1e-6
        && Math.Abs(anchorY - LegacyManipulatorPivotAnchorY) <= 1e-6;
}
