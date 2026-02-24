using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class RecipeCalculatorTests
{
    [Fact]
    public void Recalculate_DoesNotApplyManipulatorBaseOffsets_ToRobotCoordinates()
    {
        var point = new RecipePoint
        {
            RecipeCode = "T1",
            NPoint = 1,
            RCrd = 420,
            ZCrd = 315,
            Place = 0,
            ANozzle = 22,
            Alfa = 15,
            Betta = 20,
            SpeedTable = 4,
            IceRate = 10,
            DClampForm = 800,
            DClampCont = 1600,
            Container = true
        };

        var docA = new RecipeDocument { RecipeCode = "T1" };
        docA.Points.Add(point.Clone());

        var docB = new RecipeDocument { RecipeCode = "T1" };
        docB.Points.Add(point.Clone());

        var settingsA = new AppSettings { Xm = -2456, Ym = -1223, Zm = 423 };
        var settingsB = new AppSettings { Xm = -2000, Ym = -1000, Zm = 500 };

        RecipeCalculator.Recalculate(docA, settingsA);
        RecipeCalculator.Recalculate(docB, settingsB);

        var a = docA.Points[0];
        var b = docB.Points[0];

        Assert.Equal(a.DX, b.DX);
        Assert.Equal(a.DY, b.DY);
        Assert.Equal(a.DZ, b.DZ);

        Assert.Equal(a.Xr0, b.Xr0);
        Assert.Equal(a.Yx0, b.Yx0);
        Assert.Equal(a.Zr0, b.Zr0);
    }
}
