using System.IO;
using System.Linq;
using System;
using Avalonia;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class ProfileViewGeometryTests
{
    private static readonly string SamplePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "RecipeStudio.Desktop",
            "Assets",
            "Samples",
            "H340_KAMA_1.csv"));

    [Fact]
    public void Build_CreatesPythonLikePathFromGroup1AndGroup4Only()
    {
        var points = new[]
        {
            new RecipePoint { NPoint = 1, Place = 0, RCrd = 364, ZCrd = 354, ANozzle = 20, Alfa = -30, Betta = 12, Act = true },
            new RecipePoint { NPoint = 2, Place = 1, RCrd = 334, ZCrd = 365, ANozzle = 20, Alfa = 90, Betta = 0, Act = true },
            new RecipePoint { NPoint = 3, Place = 1, RCrd = 253, ZCrd = 347, ANozzle = 25, Alfa = 45, Betta = 0, Act = true }
        };

        var service = new ProfileDisplayPathService();
        var path = service.Build(points, new AppSettings { HFreeZ = 800, Lz = 250 });

        Assert.Equal(4, path.Polylines.Count);
        Assert.Collection(
            path.PathNodes,
            node =>
            {
                Assert.Equal(1, node.NPoint);
                Assert.Equal(ProfileDisplayPathService.Group1Name, node.GroupName);
                AssertPoint(new Point(-364, 354), node.A0);
                AssertPoint(new Point(-130.173, 219), node.B0);
            },
            node =>
            {
                Assert.Equal(2, node.NPoint);
                Assert.Equal(ProfileDisplayPathService.Group4Name, node.GroupName);
                AssertPoint(new Point(334, 1165), node.A0);
                Assert.Equal(-90, node.AlfaDisplay, 6);
            },
            node =>
            {
                Assert.Equal(3, node.NPoint);
                Assert.Equal(ProfileDisplayPathService.Group4Name, node.GroupName);
                AssertPoint(new Point(253, 1147), node.A0);
                Assert.Equal(-45, node.AlfaDisplay, 6);
                Assert.Equal(25, node.ANozzle, 6);
            });
    }

    [Fact]
    public void EvaluateByProgress_InterpolatesDisplayPathAndKeepsNearestNodeSelection()
    {
        var points = new[]
        {
            new RecipePoint { NPoint = 1, Place = 0, RCrd = 100, ZCrd = 100, ANozzle = 20, Alfa = 0, Betta = 5, Act = true },
            new RecipePoint { NPoint = 2, Place = 0, RCrd = 0, ZCrd = 100, ANozzle = 40, Alfa = 90, Betta = 15, Act = true }
        };

        var service = new ProfileDisplayPathService();
        var path = service.Build(points, new AppSettings { HFreeZ = 800, Lz = 250 });
        var sample = service.EvaluateByProgress(path, 0.5);

        Assert.True(sample.IsValid);
        Assert.Equal(0.5, sample.Progress, 6);
        AssertPoint(new Point(-50, 100), sample.A0);
        Assert.Equal(30, sample.ANozzle, 6);
        Assert.Equal(45, sample.AlfaDisplay, 6);
        Assert.Equal(10, sample.Beta, 6);
        Assert.Equal(0, sample.SegmentIndex);
        Assert.Equal(0.5, sample.SegmentT, 6);
        Assert.Equal(1, sample.NPoint);
    }

    [Fact]
    public void Build_CreatesSmoothedGroupCurvesAndPreviewFrameCloud()
    {
        var points = new[]
        {
            new RecipePoint { NPoint = 1, Place = 0, RCrd = 364, ZCrd = 354, ANozzle = 20, Alfa = -30, Betta = 12, Act = true },
            new RecipePoint { NPoint = 2, Place = 0, RCrd = 377, ZCrd = 337, ANozzle = 20, Alfa = -15, Betta = 13, Act = true },
            new RecipePoint { NPoint = 3, Place = 0, RCrd = 382, ZCrd = 315, ANozzle = 20, Alfa = 0, Betta = 43, Act = true }
        };

        var service = new ProfileDisplayPathService();
        var path = service.Build(points, new AppSettings { HFreeZ = 800, Lz = 250 });

        Assert.Equal(ProfileDisplayPathService.DefaultPreviewFrameCount, path.FrameSamples.Count);
        Assert.Equal(3, path.Polylines[0].ControlPoints.Count);
        Assert.Equal(250, path.Polylines[0].CurvePoints.Count);
        Assert.Equal(3, path.PathNodes.Count);
    }

    [Fact]
    public void Build_UsesNegativeAlphaForTopGroupPathNodes()
    {
        var points = new[]
        {
            new RecipePoint { NPoint = 27, Place = 1, RCrd = 253, ZCrd = 347, ANozzle = 20, Alfa = 45, Betta = 0, Act = true },
            new RecipePoint { NPoint = 28, Place = 1, RCrd = 242, ZCrd = 329, ANozzle = 20, Alfa = 45, Betta = 0, Act = true },
            new RecipePoint { NPoint = 31, Place = 1, RCrd = 181, ZCrd = 328, ANozzle = 20, Alfa = 87, Betta = 0, Act = true }
        };

        var service = new ProfileDisplayPathService();
        var path = service.Build(points, new AppSettings { HFreeZ = 800, Lz = 250 });

        Assert.Collection(
            path.PathNodes,
            node =>
            {
                Assert.Equal(27, node.NPoint);
                Assert.Equal(-45, node.AlfaDisplay, 6);
                AssertPoint(new Point(267.142, 1132.858), node.A1);
                AssertPoint(new Point(443.919, 956.081), node.B0);
            },
            node =>
            {
                Assert.Equal(28, node.NPoint);
                Assert.Equal(-45, node.AlfaDisplay, 6);
                AssertPoint(new Point(256.142, 1114.858), node.A1);
                AssertPoint(new Point(432.919, 938.081), node.B0);
            },
            node =>
            {
                Assert.Equal(31, node.NPoint);
                Assert.Equal(-87, node.AlfaDisplay, 6);
                AssertPoint(new Point(182.047, 1108.027), node.A1);
                AssertPoint(new Point(195.131, 858.37), node.B0);
            });
    }

    [Fact]
    public void ResolvePairGeometry_Rebuilds2DProfileFromTargetAlphaANozzleAndLz()
    {
        var geometry = ProfileViewGeometry.ResolvePairGeometry(
            targetPoint: new Point(-364, 354),
            alfaDeg: -30,
            place: 0,
            invertHorizontal: true,
            aNozzleMm: 20,
            nozzleLengthMm: 250);

        AssertPoint(new Point(-364, 354), geometry.TargetPoint);
        AssertPoint(new Point(-346.679, 344), geometry.NozzleTipPoint);
        AssertPoint(new Point(-130.173, 219), geometry.ToolPoint);
    }

    [Fact]
    public void ResolvePairGeometry_MirrorsAlphaForTopRowsIn2DView()
    {
        var geometry = ProfileViewGeometry.ResolvePairGeometry(
            targetPoint: new Point(253, 917),
            alfaDeg: 45,
            place: 1,
            invertHorizontal: true,
            aNozzleMm: 20,
            nozzleLengthMm: 250);

        AssertPoint(new Point(267.142, 902.858), geometry.NozzleTipPoint);
        AssertPoint(new Point(443.919, 726.081), geometry.ToolPoint);
    }

    [Fact]
    public void ResolveInterpolatedANozzle_UsesFirstRowInExcelFirstRowMode()
    {
        var settings = new AppSettings
        {
            ANozzleKinematicsMode = ANozzleKinematicsModes.ExcelFirstRow
        };

        var source = new[]
        {
            new RecipePoint { ANozzle = 20 },
            new RecipePoint { ANozzle = 55 }
        };

        var value = ProfileViewGeometry.ResolveInterpolatedANozzle(source, settings, segmentIndex: 0, segmentT: 0.8);

        Assert.Equal(20, value, 6);
    }

    [Fact]
    public void BundledSample_UsesPythonReferenceTopPoints()
    {
        var serializer = new RecipeTsvSerializer();
        var doc = serializer.Load(SamplePath);
        var path = new ProfileDisplayPathService().Build(doc.Points, new AppSettings { HFreeZ = 800, Lz = 250 });

        var point23 = Assert.Single(path.PathNodes, node => node.NPoint == 23);
        AssertPoint(new Point(334, 1165), point23.A0);

        var expectedZ = new[] { 365d, 365d, 362d, 356d, 347d, 329d, 327d, 327d, 328d };
        var actualZ = doc.Points
            .Where(point => point.NPoint >= 23 && point.NPoint <= 31)
            .Select(point => point.ZCrd)
            .ToArray();

        Assert.Equal(expectedZ, actualZ);
    }

    private static void AssertPoint(Point expected, Point actual)
    {
        Assert.True(
            SamePoint(expected, actual),
            $"Expected ({expected.X:0.###}, {expected.Y:0.###}), got ({actual.X:0.###}, {actual.Y:0.###}).");
    }

    private static bool SamePoint(Point a, Point b)
        => System.Math.Abs(a.X - b.X) <= 1e-3 && System.Math.Abs(a.Y - b.Y) <= 1e-3;
}
