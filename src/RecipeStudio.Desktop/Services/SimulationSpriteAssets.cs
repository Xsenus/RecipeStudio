using System;

namespace RecipeStudio.Desktop.Services;

public static class SimulationSpriteVersions
{
    public const string Version1 = "v1";
    public const string Version2 = "v2";

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Version1, StringComparison.OrdinalIgnoreCase))
            return Version1;

        return Version2;
    }
}

public static class SimulationSpriteAssets
{
    private const string RootUri = "avares://RecipeStudio.Desktop/Assets/Images";

    public static string PartUri => $"{RootUri}/H340_KAMA.fw.png";

    public static string GetManipulatorUri(string? spriteVersion)
        => $"{RootUri}/{SimulationSpriteVersions.Normalize(spriteVersion)}/manipulator.fw.png";

    public static string GetNozzleUri(string? spriteVersion)
        => $"{RootUri}/{SimulationSpriteVersions.Normalize(spriteVersion)}/soplo.fw.png";
}
