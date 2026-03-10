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
}
