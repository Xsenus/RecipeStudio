using System.Collections.Generic;
using Avalonia;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class SimulationOverlayGeometryTests
{
    [Fact]
    public void ResolvePlotMarkerGeometry_KeepsTargetAnchored_WhenPhysicalOrientationEnabled()
    {
        var marker = SimulationOverlayGeometry.ResolvePlotMarkerGeometry(
            toolPosition: new Point(-120, 40),
            targetPosition: new Point(-80, 55),
            rawToolWorld: new Point(120, 40),
            rawTargetWorld: new Point(80, 55),
            invertHorizontal: true,
            usePhysicalOrientation: true,
            physicalDirection: new Point(0.4, -0.9));

        AssertPoint(new Point(-120, 40), marker.ToolPoint);
        AssertPoint(new Point(-80, 55), marker.TargetPoint);
        AssertPoint(new Point(-0.4, -0.9), marker.Direction);
    }

    [Fact]
    public void ResolvePairOverlayGeometry_KeepsPointOneOnTarget_AndMovesOnlyNozzleTip()
    {
        var marker = new PlotMarkerGeometry(
            ToolPoint: new Point(-120, 40),
            TargetPoint: new Point(-80, 55),
            Direction: new Point(-0.6, -0.8));

        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            verticalOffsetMm: 25,
            usePhysicalOrientation: true,
            nozzleLengthMm: 100);

        AssertPoint(new Point(-80, 80), pair.TargetPoint);
        AssertPoint(new Point(-120, 65), pair.ToolPoint);
        AssertPoint(new Point(-180, -15), pair.NozzleTipPoint);
    }

    [Theory]
    [InlineData(SimulationTargetDisplayModes.Original, 2, -10, 20, 30, 40)]
    [InlineData(SimulationTargetDisplayModes.Mirrored, 2, 210, 20, 170, 40)]
    [InlineData(SimulationTargetDisplayModes.Full, 4, -10, 20, 30, 40)]
    public void BuildDisplayedTargetPoints_RespectsRequestedDisplayMode(
        string mode,
        int expectedCount,
        double firstX,
        double firstY,
        double secondX,
        double secondY)
    {
        var source = new List<Point>
        {
            new(-10, 20),
            new(30, 40)
        };

        var displayed = SimulationOverlayGeometry.BuildDisplayedTargetPoints(source, mirrorAxisX: 100, mode);

        Assert.Equal(expectedCount, displayed.Count);
        Assert.Contains(displayed, point => SamePoint(point, new Point(firstX, firstY)));
        Assert.Contains(displayed, point => SamePoint(point, new Point(secondX, secondY)));
    }

    private static void AssertPoint(Point expected, Point actual)
    {
        Assert.True(
            SamePoint(expected, actual),
            $"Expected ({expected.X:0.###}, {expected.Y:0.###}), got ({actual.X:0.###}, {actual.Y:0.###}).");
    }

    private static bool SamePoint(Point a, Point b)
        => System.Math.Abs(a.X - b.X) <= 1e-6 && System.Math.Abs(a.Y - b.Y) <= 1e-6;
}
