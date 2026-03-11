using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class SimulationSpriteVersionsTests
{
    [Fact]
    public void Normalize_FallsBackToVersion2_ForEmptyOrUnknownValues()
    {
        Assert.Equal(SimulationSpriteVersions.Version2, SimulationSpriteVersions.Normalize(null));
        Assert.Equal(SimulationSpriteVersions.Version2, SimulationSpriteVersions.Normalize(string.Empty));
        Assert.Equal(SimulationSpriteVersions.Version2, SimulationSpriteVersions.Normalize("unexpected"));
    }

    [Fact]
    public void Normalize_AcceptsSupportedValues_CaseInsensitive()
    {
        Assert.Equal(SimulationSpriteVersions.Version1, SimulationSpriteVersions.Normalize("v1"));
        Assert.Equal(SimulationSpriteVersions.Version1, SimulationSpriteVersions.Normalize("V1"));
        Assert.Equal(SimulationSpriteVersions.Version2, SimulationSpriteVersions.Normalize("v2"));
        Assert.Equal(SimulationSpriteVersions.Version2, SimulationSpriteVersions.Normalize("V2"));
    }

    [Fact]
    public void DefaultSettings_UseVersion2SpriteAssets()
    {
        var settings = new AppSettings();

        Assert.Equal(SimulationSpriteVersions.Version2, settings.SimulationPanels.SpriteVersion);
        Assert.Equal(
            "avares://RecipeStudio.Desktop/Assets/Images/v2/manipulator.fw.png",
            SimulationSpriteAssets.GetManipulatorUri(settings.SimulationPanels.SpriteVersion));
        Assert.Equal(
            "avares://RecipeStudio.Desktop/Assets/Images/v2/soplo.fw.png",
            SimulationSpriteAssets.GetNozzleUri(settings.SimulationPanels.SpriteVersion));
        Assert.Equal(
            "avares://RecipeStudio.Desktop/Assets/Images/H340_KAMA.fw.png",
            SimulationSpriteAssets.PartUri);
    }
}
