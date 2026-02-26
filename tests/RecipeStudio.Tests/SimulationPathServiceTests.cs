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
}
