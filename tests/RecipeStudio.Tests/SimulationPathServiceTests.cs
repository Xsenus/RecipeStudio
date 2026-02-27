using System.Collections.Generic;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class SimulationPathServiceTests
{
    [Fact]
    public void Build_WithSmoothMotion_CreatesArcMap()
    {
        var service = new SimulationPathService();
        var points = new List<RecipePoint>
        {
            new() { Act = true, Xr0 = 0, Yx0 = 0, Zr0 = 0, NozzleSpeedMmMin = 120 },
            new() { Act = true, Xr0 = 20, Yx0 = 20, Zr0 = 40, NozzleSpeedMmMin = 120 },
            new() { Act = true, Xr0 = 40, Yx0 = 0, Zr0 = 80, NozzleSpeedMmMin = 120 }
        };

        var path = service.Build(points, smoothMotion: true);

        Assert.NotEmpty(path.Segments);
        Assert.NotEmpty(path.Segments[0].ArcMap);
        Assert.True(path.Segments[0].ArcMap[^1].Length > 0);
    }
    [Fact]
    public void BuildAbsolutePositions_UsesIncrementalDeltas_ForWorkbookStyleRows()
    {
        var points = new List<RecipePoint>
        {
            new() { Act = true, Safe = false, Xr0 = 94, Yx0 = 0, Zr0 = 354, DX = 41.3, DY = -56.1, DZ = -132 },
            new() { Act = true, Safe = false, Xr0 = 94, Yx0 = 0, Zr0 = 354, DX = -12.4, DY = -4.6, DZ = 46.9 },
            new() { Act = true, Safe = false, Xr0 = 94, Yx0 = 0, Zr0 = 354, DX = 61.6, DY = -123.4, DZ = 46.1 }
        };

        var absolute = RobotCoordinateResolver.BuildAbsolutePositions(points);

        Assert.Equal(3, absolute.Count);
        Assert.Equal(135.3f, absolute[0].X, 1);
        Assert.Equal(122.9f, absolute[1].X, 1);
        Assert.Equal(184.5f, absolute[2].X, 1);

        Assert.Equal(222f, absolute[0].Z, 1);
        Assert.Equal(268.9f, absolute[1].Z, 1);
        Assert.Equal(315f, absolute[2].Z, 1);
    }

}
