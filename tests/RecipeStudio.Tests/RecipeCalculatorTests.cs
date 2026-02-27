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

    [Fact]
    public void Recalculate_UsesExcelAlfaSign_WhenComputingRobotZOffset()
    {
        var doc = new RecipeDocument { RecipeCode = "T2" };
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T2",
            NPoint = 1,
            RCrd = 300,
            ZCrd = 200,
            Place = 0,
            ANozzle = 20,
            Alfa = 30,
            Betta = 0,
            SpeedTable = 5,
            IceRate = 10
        });

        RecipeCalculator.Recalculate(doc, new AppSettings { Lz = 100 });

        var point = doc.Points[0];
        Assert.True(point.DZ > 0, "Positive alfa should tilt nozzle toward positive Z to match Excel CALC/SAVE formulas.");
    }

    [Fact]
    public void Recalculate_UsesFirstWorkingPoint_AsRobotOriginForAllRows()
    {
        var doc = new RecipeDocument { RecipeCode = "T3" };
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T3",
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
            RecipeCode = "T3",
            NPoint = 2,
            Act = true,
            Safe = false,
            RCrd = 377,
            ZCrd = 337,
            ANozzle = 20,
            Alfa = -15,
            Betta = 13,
            SpeedTable = 3,
            IceRate = 103.6
        });

        RecipeCalculator.Recalculate(doc, new AppSettings { Lz = 250 });

        var p1 = doc.Points[0];
        var p2 = doc.Points[1];

        Assert.Equal(94, p1.Xr0);
        Assert.Equal(354, p1.Zr0);
        Assert.Equal(p1.Xr0, p2.Xr0);
        Assert.Equal(p1.Zr0, p2.Zr0);

        Assert.Equal(-12.4, p2.DX);
        Assert.Equal(46.9, p2.DZ);
    }

}
