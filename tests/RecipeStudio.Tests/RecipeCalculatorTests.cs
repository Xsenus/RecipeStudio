using System;
using System.IO;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class RecipeCalculatorTests
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

    [Fact]
    public void Recalculate_UsesOwnBaseAndZeroCalculatedOutputs_ForSafeRows()
    {
        var doc = new RecipeDocument { RecipeCode = "T4" };
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "T4",
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
            RecipeCode = "T4",
            NPoint = 32,
            Act = true,
            Safe = true,
            RCrd = 364,
            ZCrd = 374,
            ANozzle = 0,
            Alfa = 0,
            Betta = 0,
            SpeedTable = 0,
            IceRate = 0
        });

        RecipeCalculator.Recalculate(doc, new AppSettings { Lz = 250 });

        var safe = doc.Points[1];
        Assert.Equal(0, safe.Xr0);
        Assert.Equal(0, safe.Yx0);
        Assert.Equal(0, safe.Zr0);
        Assert.Equal(0, safe.DX);
        Assert.Equal(0, safe.DY);
        Assert.Equal(0, safe.DZ);
        Assert.Equal(0, safe.DA);
        Assert.Equal(0, safe.AB);
        Assert.Equal(0, safe.XPuls);
        Assert.Equal(0, safe.YPuls);
        Assert.Equal(0, safe.ZPuls);
        Assert.Equal(0, safe.APuls);
        Assert.Equal(0, safe.BPuls);
        Assert.Equal(0, safe.TopPuls);
        Assert.Equal(0, safe.TopHz);
        Assert.Equal(0, safe.LowPuls);
        Assert.Equal(0, safe.LowHz);
        Assert.Equal(0, safe.ClampPuls);
    }

    [Fact]
    public void Recalculate_MatchesBundledSampleSaveOutputs()
    {
        var serializer = new RecipeTsvSerializer();
        var expected = serializer.Load(SamplePath);
        var actual = serializer.Load(SamplePath);

        RecipeCalculator.Recalculate(actual, new AppSettings());

        Assert.Equal(expected.Points.Count, actual.Points.Count);

        for (var i = 0; i < expected.Points.Count; i++)
        {
            AssertCalculatedOutputsEqual(expected.Points[i], actual.Points[i]);
        }
    }

    [Fact]
    public void Recalculate_ComputesWorkbookUiDerivedValues_ForSampleRows()
    {
        var serializer = new RecipeTsvSerializer();
        var doc = serializer.Load(SamplePath);

        RecipeCalculator.Recalculate(doc, new AppSettings());

        var first = doc.Points[0];
        var second = doc.Points[1];

        Assert.Equal(20, first.TimeSec, precision: 6);
        Assert.Equal(20, second.TimeSec, precision: 6);
        Assert.Equal(100, first.RecommendedIceRate, precision: 6);
        Assert.Equal(103.6, second.RecommendedIceRate, precision: 6);
        Assert.Equal(37, second.RecommendedAlfa, precision: 6);
    }

    [Fact]
    public void Recalculate_UsesConfiguredRecommendedAlfaMode()
    {
        var plusDoc = new RecipeDocument { RecipeCode = "T5" };
        plusDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T5",
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
        plusDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T5",
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

        var minusDoc = new RecipeDocument { RecipeCode = "T5" };
        minusDoc.Points.Add(plusDoc.Points[0].Clone());
        minusDoc.Points.Add(plusDoc.Points[1].Clone());

        RecipeCalculator.Recalculate(plusDoc, new AppSettings
        {
            Lz = 250,
            RecommendedAlfaMode = RecommendedAlfaModes.Plus90
        });
        RecipeCalculator.Recalculate(minusDoc, new AppSettings
        {
            Lz = 250,
            RecommendedAlfaMode = RecommendedAlfaModes.Minus90
        });

        Assert.Equal(37, plusDoc.Points[1].RecommendedAlfa, precision: 6);
        Assert.Equal(143, minusDoc.Points[1].RecommendedAlfa, precision: 6);
    }

    [Fact]
    public void Recalculate_UsesConfiguredCalculationOriginMode()
    {
        var excelDoc = new RecipeDocument { RecipeCode = "T6" };
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T6",
            NPoint = 1,
            Act = false,
            Safe = true,
            RCrd = 500,
            ZCrd = 400,
            ANozzle = 20,
            SpeedTable = 3,
            IceRate = 100
        });
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T6",
            NPoint = 2,
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

        var currentDoc = new RecipeDocument { RecipeCode = "T6" };
        currentDoc.Points.Add(excelDoc.Points[0].Clone());
        currentDoc.Points.Add(excelDoc.Points[1].Clone());

        RecipeCalculator.Recalculate(excelDoc, new AppSettings
        {
            Lz = 250,
            CalculationOriginMode = CalculationOriginModes.ExcelFirstRow
        });
        RecipeCalculator.Recalculate(currentDoc, new AppSettings
        {
            Lz = 250,
            CalculationOriginMode = CalculationOriginModes.CurrentFirstWorking
        });

        Assert.Equal(230, excelDoc.Points[1].Xr0, precision: 6);
        Assert.Equal(94, currentDoc.Points[1].Xr0, precision: 6);
    }

    [Fact]
    public void Recalculate_UsesConfiguredANozzleKinematicsMode()
    {
        var excelDoc = new RecipeDocument { RecipeCode = "T7" };
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T7",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 300,
            ZCrd = 200,
            ANozzle = 20,
            SpeedTable = 3,
            IceRate = 100
        });
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T7",
            NPoint = 2,
            Act = true,
            Safe = false,
            RCrd = 310,
            ZCrd = 200,
            ANozzle = 50,
            SpeedTable = 3,
            IceRate = 100
        });

        var currentDoc = new RecipeDocument { RecipeCode = "T7" };
        currentDoc.Points.Add(excelDoc.Points[0].Clone());
        currentDoc.Points.Add(excelDoc.Points[1].Clone());

        RecipeCalculator.Recalculate(excelDoc, new AppSettings
        {
            Lz = 100,
            ANozzleKinematicsMode = ANozzleKinematicsModes.ExcelFirstRow
        });
        RecipeCalculator.Recalculate(currentDoc, new AppSettings
        {
            Lz = 100,
            ANozzleKinematicsMode = ANozzleKinematicsModes.CurrentPerPoint
        });

        Assert.Equal(10, excelDoc.Points[1].DX, precision: 6);
        Assert.Equal(-20, currentDoc.Points[1].DX, precision: 6);
    }

    [Fact]
    public void Recalculate_UsesConfiguredVelocityCalculationMode()
    {
        var excelDoc = new RecipeDocument { RecipeCode = "T8" };
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T8",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 364,
            ZCrd = 354,
            ANozzle = 20,
            SpeedTable = 3,
            IceRate = 100
        });

        var currentDoc = new RecipeDocument { RecipeCode = "T8" };
        currentDoc.Points.Add(excelDoc.Points[0].Clone());

        RecipeCalculator.Recalculate(excelDoc, new AppSettings
        {
            VelocityCalculationMode = VelocityCalculationModes.ExcelExact
        });
        RecipeCalculator.Recalculate(currentDoc, new AppSettings
        {
            VelocityCalculationMode = VelocityCalculationModes.CurrentRounded
        });

        Assert.Equal(6857.76, excelDoc.Points[0].NozzleSpeedMmMin, precision: 6);
        Assert.Equal(6858, currentDoc.Points[0].NozzleSpeedMmMin, precision: 6);
    }

    [Fact]
    public void Recalculate_UsesConfiguredTopLowPulseMode()
    {
        var excelDoc = new RecipeDocument { RecipeCode = "T9" };
        excelDoc.Points.Add(new RecipePoint
        {
            RecipeCode = "T9",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 300,
            ZCrd = 200,
            ANozzle = 20,
            SpeedTable = 3,
            IceRate = 100
        });

        var currentDoc = new RecipeDocument { RecipeCode = "T9" };
        currentDoc.Points.Add(excelDoc.Points[0].Clone());

        RecipeCalculator.Recalculate(excelDoc, new AppSettings
        {
            TopLowPulseMode = TopLowPulseModes.ExcelLinked,
            PulseTop = 100,
            PulseLow = 200
        });
        RecipeCalculator.Recalculate(currentDoc, new AppSettings
        {
            TopLowPulseMode = TopLowPulseModes.CurrentIndependent,
            PulseTop = 100,
            PulseLow = 200
        });

        Assert.Equal(200, excelDoc.Points[0].TopPuls, precision: 6);
        Assert.Equal(10, excelDoc.Points[0].TopHz, precision: 6);
        Assert.Equal(100, currentDoc.Points[0].TopPuls, precision: 6);
        Assert.Equal(5, currentDoc.Points[0].TopHz, precision: 6);
    }

    private static void AssertCalculatedOutputsEqual(RecipePoint expected, RecipePoint actual)
    {
        Assert.Equal(expected.Xr0, actual.Xr0, precision: 6);
        Assert.Equal(expected.Yx0, actual.Yx0, precision: 6);
        Assert.Equal(expected.Zr0, actual.Zr0, precision: 6);
        Assert.Equal(expected.DX, actual.DX, precision: 6);
        Assert.Equal(expected.DY, actual.DY, precision: 6);
        Assert.Equal(expected.DZ, actual.DZ, precision: 6);
        Assert.Equal(expected.DA, actual.DA, precision: 6);
        Assert.Equal(expected.AB, actual.AB, precision: 6);
        Assert.Equal(expected.XPuls, actual.XPuls, precision: 6);
        Assert.Equal(expected.YPuls, actual.YPuls, precision: 6);
        Assert.Equal(expected.ZPuls, actual.ZPuls, precision: 6);
        Assert.Equal(expected.APuls, actual.APuls, precision: 6);
        Assert.Equal(expected.BPuls, actual.BPuls, precision: 6);
        Assert.Equal(expected.TopPuls, actual.TopPuls, precision: 6);
        Assert.Equal(expected.TopHz, actual.TopHz, precision: 6);
        Assert.Equal(expected.LowPuls, actual.LowPuls, precision: 6);
        Assert.Equal(expected.LowHz, actual.LowHz, precision: 6);
        Assert.Equal(expected.ClampPuls, actual.ClampPuls, precision: 6);
    }

}
