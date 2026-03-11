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
            TargetPoint: new Point(0, 40),
            Direction: new Point(1, 0));

        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            verticalOffsetMm: 25,
            usePhysicalOrientation: true,
            nozzleLengthMm: 100);

        AssertPoint(new Point(0, 65), pair.TargetPoint);
        AssertPoint(new Point(-120, 65), pair.ToolPoint);
        AssertPoint(new Point(-20, 65), pair.NozzleTipPoint);
    }

    [Fact]
    public void ResolvePairOverlayGeometry_LeavesGapEqualToANozzle_WhenTargetIsFartherThanL()
    {
        var marker = new PlotMarkerGeometry(
            ToolPoint: new Point(0, 0),
            TargetPoint: new Point(120, 0),
            Direction: new Point(1, 0));

        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            verticalOffsetMm: 0,
            usePhysicalOrientation: true,
            nozzleLengthMm: 100);

        var visibleLength = pair.NozzleTipPoint.X - pair.ToolPoint.X;
        var remainingGap = pair.TargetPoint.X - pair.NozzleTipPoint.X;

        Assert.Equal(100, visibleLength, 6);
        Assert.Equal(20, remainingGap, 6);
    }

    [Fact]
    public void ResolvePairOverlayGeometry_PreservesPhysicalDirection_ForVisibleNozzle()
    {
        var marker = new PlotMarkerGeometry(
            ToolPoint: new Point(0, 0),
            TargetPoint: new Point(0, 120),
            Direction: new Point(1, 0));

        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            verticalOffsetMm: 0,
            usePhysicalOrientation: true,
            nozzleLengthMm: 100);

        AssertPoint(new Point(100, 0), pair.NozzleTipPoint);
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

    [Fact]
    public void BuildDisplayedTargetPoints_ForStyledPoints_PreservesOrderAcrossOverlappingSafeAndWorkPoints()
    {
        var source = new List<StyledTargetPoint>
        {
            new(new Point(30, 40), safe: false),
            new(new Point(30, 40), safe: true)
        };

        var displayed = SimulationOverlayGeometry.BuildDisplayedTargetPoints(
            source,
            mirrorAxisX: 100,
            SimulationTargetDisplayModes.Full);

        Assert.Collection(
            displayed,
            point =>
            {
                Assert.False(point.Safe);
                AssertPoint(new Point(30, 40), point.Position);
            },
            point =>
            {
                Assert.False(point.Safe);
                AssertPoint(new Point(170, 40), point.Position);
            },
            point =>
            {
                Assert.True(point.Safe);
                AssertPoint(new Point(30, 40), point.Position);
            },
            point =>
            {
                Assert.True(point.Safe);
                AssertPoint(new Point(170, 40), point.Position);
            });
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
