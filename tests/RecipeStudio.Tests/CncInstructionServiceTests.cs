using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class CncInstructionServiceTests
{
    [Fact]
    public void BuildRows_ProducesWorkbookLikeValues_ForWorkingRows()
    {
        var doc = new RecipeDocument { RecipeCode = "T1" };
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T1",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 364,
            ZCrd = 354,
            ANozzle = 20,
            Alfa = -30,
            Betta = 12,
            SpeedTable = 3,
            IceRate = 100,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600
        });
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T1",
            NPoint = 2,
            Act = true,
            Safe = false,
            RCrd = 377,
            ZCrd = 337,
            ANozzle = 20,
            Alfa = -15,
            Betta = 13,
            SpeedTable = 3,
            IceRate = 103.6,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600
        });

        var rows = CncInstructionService.BuildRows(doc, new AppSettings());

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal("1", first["n_point"]);
        Assert.Equal("0", first["Safe"]);
        Assert.Equal("94", first["Xr0"]);
        Assert.Equal("354", first["Zr0"]);
        Assert.Equal("135,3", first["Xr"]);
        Assert.Equal("41,3", first["dX"]);
        Assert.Equal("5140", first["Top_Hz"]);
        Assert.Equal("80000", first["Clamp_puls"]);

        var second = rows[1];
        Assert.Equal("-12,4", second["dX"]);
        Assert.Equal("46,9", second["dZ"]);
        Assert.Equal("5", second["Bpuls"]);
    }

    [Fact]
    public void BuildRows_LeavesWorkbookStyleBlanks_ForSafeRows()
    {
        var doc = new RecipeDocument { RecipeCode = "T2" };
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T2",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 364,
            ZCrd = 354,
            ANozzle = 20,
            Alfa = -30,
            Betta = 12,
            SpeedTable = 3,
            IceRate = 100
        });
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T2",
            NPoint = 32,
            Act = true,
            Safe = true,
            RCrd = 364,
            ZCrd = 374,
            Place = 0
        });

        var rows = CncInstructionService.BuildRows(doc, new AppSettings());

        Assert.Equal(2, rows.Count);

        var safe = rows[1];
        Assert.Equal("32", safe["n_point"]);
        Assert.Equal("1", safe["Safe"]);
        Assert.Equal("0", safe["place"]);
        Assert.Equal("364", safe["Xp"]);
        Assert.Equal("374", safe["Zp"]);
        Assert.Equal(string.Empty, safe["Xm"]);
        Assert.Equal(string.Empty, safe["Xr0"]);
        Assert.Equal(string.Empty, safe["Clamp_puls"]);
    }
}
