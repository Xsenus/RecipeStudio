using RecipeStudio.Desktop.Controls;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class SimulationSpriteAnchorsTests
{
    [Fact]
    public void DefaultAnchors_MatchMeasuredSpritePivotMarkers()
    {
        Assert.Equal(SimulationSpriteAnchors.ManipulatorPivotAnchorX, SimulationBlueprint2DControl.DefaultManipulatorAnchorX, 6);
        Assert.Equal(SimulationSpriteAnchors.ManipulatorPivotAnchorY, SimulationBlueprint2DControl.DefaultManipulatorAnchorY, 6);
        Assert.Equal(SimulationSpriteAnchors.NozzleTipAnchorX, SimulationBlueprint2DControl.DefaultNozzleAnchorX, 6);
        Assert.Equal(SimulationSpriteAnchors.NozzlePivotAnchorY, SimulationBlueprint2DControl.DefaultNozzleAnchorY, 6);

        var calibration = new Simulation2DCalibrationSettings();
        Assert.Equal(SimulationSpriteAnchors.ManipulatorPivotAnchorX, calibration.ManipulatorAnchorX, 6);
        Assert.Equal(SimulationSpriteAnchors.ManipulatorPivotAnchorY, calibration.ManipulatorAnchorY, 6);
    }

    [Fact]
    public void UsesLegacyManipulatorPivot_DetectsOldSavedCalibration()
    {
        Assert.True(SimulationSpriteAnchors.UsesLegacyManipulatorPivot(0.04, 0.90));
        Assert.False(SimulationSpriteAnchors.UsesLegacyManipulatorPivot(
            SimulationSpriteAnchors.ManipulatorPivotAnchorX,
            SimulationSpriteAnchors.ManipulatorPivotAnchorY));
    }

    [Fact]
    public void Version2Anchors_MatchMeasuredSpritePivotMarkers()
    {
        Assert.Equal(13.0 / 676.0, SimulationSpriteAnchors.GetManipulatorPivotAnchorX(SimulationSpriteVersions.Version2), 6);
        Assert.Equal(595.0 / 606.0, SimulationSpriteAnchors.GetManipulatorPivotAnchorY(SimulationSpriteVersions.Version2), 6);
        Assert.Equal(135.0 / 147.0, SimulationSpriteAnchors.GetNozzlePivotAnchorX(SimulationSpriteVersions.Version2), 6);
        Assert.Equal(12.0 / 24.0, SimulationSpriteAnchors.GetNozzlePivotAnchorY(SimulationSpriteVersions.Version2), 6);
    }

    [Fact]
    public void UsesDefaultManipulatorPivot_DetectsKnownDefaults_ForBothVersions()
    {
        Assert.True(SimulationSpriteAnchors.UsesDefaultManipulatorPivot(
            SimulationSpriteAnchors.ManipulatorPivotAnchorX,
            SimulationSpriteAnchors.ManipulatorPivotAnchorY));
        Assert.True(SimulationSpriteAnchors.UsesDefaultManipulatorPivot(
            SimulationSpriteAnchors.GetManipulatorPivotAnchorX(SimulationSpriteVersions.Version2),
            SimulationSpriteAnchors.GetManipulatorPivotAnchorY(SimulationSpriteVersions.Version2)));
        Assert.False(SimulationSpriteAnchors.UsesDefaultManipulatorPivot(0.123, 0.456));
    }
}
