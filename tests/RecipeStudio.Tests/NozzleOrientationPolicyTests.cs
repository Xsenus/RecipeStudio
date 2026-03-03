using System.Collections.Generic;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class NozzleOrientationPolicyTests
{
    [Fact]
    public void Normalize_UnknownMode_FallsBackToPhysical()
    {
        var mode = NozzleOrientationModes.Normalize("unexpected");

        Assert.Equal(NozzleOrientationModes.PhysicalAngles, mode);
        Assert.True(NozzleOrientationPolicy.UsePhysicalOrientation(mode));
    }

    [Fact]
    public void ClampForPhysicalOrientation_UsesConfiguredLimits()
    {
        var settings = new AppSettings
        {
            AlfaMinDeg = -80,
            AlfaMaxDeg = 70,
            BettaMinDeg = -12,
            BettaMaxDeg = 14
        };

        var (alfa, betta) = NozzleOrientationPolicy.ClampForPhysicalOrientation(settings, 110, -20);

        Assert.Equal(70, alfa);
        Assert.Equal(-12, betta);
    }

    [Fact]
    public void AnalyzePoints_CountsOutOfRangeAngles_AndBuildsWarning()
    {
        var points = new List<RecipePoint>
        {
            new() { NPoint = 1, Alfa = 0, Betta = 0 },
            new() { NPoint = 2, Alfa = 95, Betta = 0 },
            new() { NPoint = 3, Alfa = 10, Betta = 20 },
            new() { NPoint = 4, Alfa = -120, Betta = -30 }
        };

        var settings = new AppSettings
        {
            AlfaMinDeg = -90,
            AlfaMaxDeg = 90,
            BettaMinDeg = -15,
            BettaMaxDeg = 15
        };

        var diagnostics = NozzleOrientationPolicy.AnalyzePoints(points, settings);
        var warning = NozzleOrientationPolicy.BuildWarningText(diagnostics, NozzleOrientationPolicy.GetLimits(settings));

        Assert.Equal(4, diagnostics.TotalPoints);
        Assert.Equal(3, diagnostics.OutOfRangeCount);
        Assert.Equal(2, diagnostics.AlfaOutOfRangeCount);
        Assert.Equal(2, diagnostics.BettaOutOfRangeCount);
        Assert.Contains("3", warning);
        Assert.Contains("A:", warning);
        Assert.Contains("B:", warning);
    }
}
