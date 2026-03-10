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

    public const double LegacyManipulatorPivotAnchorX = 0.04;
    public const double LegacyManipulatorPivotAnchorY = 0.90;

    public static bool UsesLegacyManipulatorPivot(double anchorX, double anchorY)
        => Math.Abs(anchorX - LegacyManipulatorPivotAnchorX) <= 1e-6
        && Math.Abs(anchorY - LegacyManipulatorPivotAnchorY) <= 1e-6;
}
