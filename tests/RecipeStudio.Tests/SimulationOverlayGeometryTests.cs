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
            new(new Point(30, 40), Safe: false),
            new(new Point(30, 40), Safe: true)
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

    [Theory]
    [InlineData(SimulationTargetDisplayModes.Original, true, false)]
    [InlineData(SimulationTargetDisplayModes.Mirrored, false, true)]
    [InlineData(SimulationTargetDisplayModes.Full, true, true)]
    public void TargetDisplayFlags_RespectRequestedMode(string mode, bool expectedOriginal, bool expectedMirrored)
    {
        Assert.Equal(expectedOriginal, SimulationOverlayGeometry.ShouldDrawOriginalTarget(mode));
        Assert.Equal(expectedMirrored, SimulationOverlayGeometry.ShouldDrawMirroredTarget(mode));
    }

    [Fact]
    public void MirrorProfileDisplayPath_MirrorsEveryOverlayCollection()
    {
        var source = new ProfileDisplayPath(
            new List<ProfilePolylineData>
            {
                new(
                    "Group 1",
                    new List<Point> { new(-10, 20), new(-30, 40) },
                    new List<Point> { new(-10, 20), new(-20, 30), new(-30, 40) },
                    new List<int> { 1, 2 })
            },
            new List<ProfilePathNode>
            {
                new(
                    PathIndex: 0,
                    SourceIndex: 0,
                    NPoint: 10,
                    Place: 0,
                    GroupName: "Group 1",
                    A0: new Point(-10, 20),
                    A1: new Point(-15, 25),
                    B0: new Point(-18, 27),
                    ANozzle: 5,
                    AlfaDisplay: 30,
                    Beta: 12,
                    ArcLength: 0)
            },
            new List<Point> { new(-18, 27), new(-22, 31) },
            new List<int> { 10, 11 },
            new List<ProfileFrameOverlaySample>
            {
                new(new Point(-15, 25), new Point(-18, 27), 10, "Group 1")
            },
            TotalPathLength: 123,
            TotalDurationSec: 7);

        var mirrored = SimulationOverlayGeometry.MirrorProfileDisplayPath(source);

        Assert.Collection(
            mirrored.Polylines,
            polyline =>
            {
                Assert.Equal("Group 1", polyline.GroupName);
                Assert.Collection(
                    polyline.ControlPoints,
                    point => AssertPoint(new Point(10, 20), point),
                    point => AssertPoint(new Point(30, 40), point));
                Assert.Collection(
                    polyline.CurvePoints,
                    point => AssertPoint(new Point(10, 20), point),
                    point => AssertPoint(new Point(20, 30), point),
                    point => AssertPoint(new Point(30, 40), point));
                Assert.Equal(new[] { 1, 2 }, polyline.PointNumbers);
            });

        Assert.Collection(
            mirrored.PathNodes,
            node =>
            {
                Assert.Equal(10, node.NPoint);
                AssertPoint(new Point(10, 20), node.A0);
                AssertPoint(new Point(15, 25), node.A1);
                AssertPoint(new Point(18, 27), node.B0);
            });

        Assert.Collection(
            mirrored.B0PolylinePoints,
            point => AssertPoint(new Point(18, 27), point),
            point => AssertPoint(new Point(22, 31), point));
        Assert.Equal(new[] { 10, 11 }, mirrored.B0PointNumbers);

        Assert.Collection(
            mirrored.FrameSamples,
            sample =>
            {
                AssertPoint(new Point(15, 25), sample.A1);
                AssertPoint(new Point(18, 27), sample.B0);
                Assert.Equal(10, sample.NPoint);
            });

        Assert.Equal(source.TotalPathLength, mirrored.TotalPathLength, 6);
        Assert.Equal(source.TotalDurationSec, mirrored.TotalDurationSec, 6);
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
