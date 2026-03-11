using System;
using System.Globalization;
using System.Linq;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public static class RecipeCalculator
{
    // Excel template uses literal 3.14 in nozzle speed formula: R * 2 * 3.14 * ω.
    private const double ExcelPi = 3.14;

    public static void Recalculate(RecipeDocument doc, AppSettings settings)
    {
        if (doc.Points.Count == 0)
            return;

        var calculationOriginMode = CalculationOriginModes.Normalize(settings.CalculationOriginMode);
        var aNozzleKinematicsMode = ANozzleKinematicsModes.Normalize(settings.ANozzleKinematicsMode);
        var velocityCalculationMode = VelocityCalculationModes.Normalize(settings.VelocityCalculationMode);
        var topLowPulseMode = TopLowPulseModes.Normalize(settings.TopLowPulseMode);
        var recommendedAlfaMode = RecommendedAlfaModes.Normalize(settings.RecommendedAlfaMode);

        // Sync recipe-level fields
        doc.RecipeCode = string.IsNullOrWhiteSpace(doc.RecipeCode)
            ? doc.Points[0].RecipeCode
            : doc.RecipeCode;

        doc.DClampForm = doc.Points[0].DClampForm;
        doc.DClampCont = doc.Points[0].DClampCont;
        doc.ContainerPresent = doc.Points[0].Container;

        // Ensure numbering
        for (var i = 0; i < doc.Points.Count; i++)
        {
            doc.Points[i].RecipeCode = doc.RecipeCode;
            doc.Points[i].NPoint = i + 1;
        }

        // Recommended alpha based on neighbour points (matches the formula from UI sheet)
        for (var i = 0; i < doc.Points.Count; i++)
        {
            if (i == 0)
            {
                doc.Points[i].RecommendedAlfa = 0;
                continue;
            }

            var prev = doc.Points[i - 1];
            var cur = doc.Points[i];

            var dz = cur.ZCrd - prev.ZCrd;
            var dr = prev.RCrd - cur.RCrd;

            cur.RecommendedAlfa = CalculateRecommendedAlfa(recommendedAlfaMode, dz, dr);
        }

        // Derived UI values (t, V, recommended ice rate)
        for (var i = 0; i < doc.Points.Count; i++)
        {
            var p = doc.Points[i];
            p.TimeSec = p.SpeedTable == 0 ? 0 : 60.0 / p.SpeedTable;
            var velocity = p.RCrd * 2.0 * ExcelPi * p.SpeedTable;
            p.NozzleSpeedMmMin = velocityCalculationMode == VelocityCalculationModes.CurrentRounded
                ? Math.Round(velocity, 0, MidpointRounding.AwayFromZero)
                : velocity;
        }

        var baseIce = doc.Points[0].IceRate;
        var baseV = doc.Points[0].NozzleSpeedMmMin;
        for (var i = 0; i < doc.Points.Count; i++)
        {
            var p = doc.Points[i];
            var rec = baseV == 0 ? 0 : baseIce * p.NozzleSpeedMmMin / baseV;
            p.RecommendedIceRate = Math.Round(rec, 1, MidpointRounding.AwayFromZero);
        }

        // CALC/SAVE outputs
        var originPoint = calculationOriginMode == CalculationOriginModes.CurrentFirstWorking
            ? doc.Points.FirstOrDefault(pt => pt.Act && !pt.Safe) ?? doc.Points[0]
            : doc.Points[0];
        var firstRowANozzle = doc.Points[0].ANozzle;
        var originANozzle = aNozzleKinematicsMode == ANozzleKinematicsModes.CurrentPerPoint
            ? originPoint.ANozzle
            : firstRowANozzle;
        var originLn1 = settings.Lz + originANozzle;
        var xr0Base = Round1(originPoint.RCrd - originLn1);
        var yx0Base = 0d;
        var zr0Base = Round1(originPoint.ZCrd);

        // Excel stores increments between consecutive robot points;
        // first working point is measured from the base origin.
        var prevXr = xr0Base;
        var prevYx = yx0Base;
        var prevZr = zr0Base;
        var prevAlfa = 0d;
        var prevBetta = 0d;

        foreach (var p in doc.Points)
        {
            var ln = settings.Lz;
            var la = aNozzleKinematicsMode == ANozzleKinematicsModes.CurrentPerPoint
                ? p.ANozzle
                : firstRowANozzle;
            var ln1 = ln + la;

            // Совместимость с Excel CALC/SAVE: используем угол Alfa без инверсии знака.
            // Иначе расчетные Xr/Zr и график будут расходиться с workbook.
            var alfaRad = p.Alfa * Math.PI / 180.0;
            var bettaRad = p.Betta * Math.PI / 180.0;

            var sinA = Math.Sin(alfaRad);
            var cosA = Math.Cos(alfaRad);
            var sinB = Math.Sin(bettaRad);
            var cosB = Math.Cos(bettaRad);

            var r0 = p.RCrd;
            var z0 = p.ZCrd;

            var xp = p.Place == 0 ? r0 : -r0;
            var zp = p.Place == 0 ? z0 : settings.HZone - z0;

            // NOTE:
            // Xr0/Yx0/Zr0 and (Xr,Yx,Zr) are expected in the same local frame as Excel CALC/SAVE.
            // Manipulator base constants (Xm/Ym/Zm) are station reference constants, but should not
            // shift these plotted/exported local coordinates.
            var xr = Round1(xp - cosB * cosA * ln1);
            var yx = Round1(-sinB * ln1);
            // For mirrored (top) rows Excel flips the angular Z contribution sign.
            var zrOffset = cosB * sinA * ln1;
            var zr = p.Place == 0
                ? Round1(zp + zrOffset)
                : Round1(zp - zrOffset);

            var dx = 0d;
            var dy = 0d;
            var dz = 0d;
            var dA = 0d;
            var aB = 0d;

            if (p.Safe)
            {
                p.Xr0 = 0;
                p.Yx0 = 0;
                p.Zr0 = 0;
                p.DX = 0;
                p.DY = 0;
                p.DZ = 0;
                p.DA = 0;
                p.AB = 0;
                p.XPuls = 0;
                p.YPuls = 0;
                p.ZPuls = 0;
                p.APuls = 0;
                p.BPuls = 0;
                p.TopPuls = 0;
                p.TopHz = 0;
                p.LowPuls = 0;
                p.LowHz = 0;
                p.ClampPuls = 0;
                continue;
            }

            p.Xr0 = xr0Base;
            p.Yx0 = yx0Base;
            p.Zr0 = zr0Base;

            dx = Round1(xr - prevXr);
            dy = Round1(yx - prevYx);
            dz = Round1(zr - prevZr);
            dA = Round1(p.Alfa - prevAlfa);
            aB = Round1(p.Betta - prevBetta);

            prevXr = xr;
            prevYx = yx;
            prevZr = zr;
            prevAlfa = p.Alfa;
            prevBetta = p.Betta;

            p.DX = dx;
            p.DY = dy;
            p.DZ = dz;

            p.DA = dA;
            p.AB = aB;

            p.XPuls = dx * settings.PulseX;
            p.YPuls = dy * settings.PulseY;
            p.ZPuls = dz * settings.PulseZ;
            p.APuls = p.DA * settings.PulseA;
            p.BPuls = p.AB * settings.PulseB;

            p.TopPuls = topLowPulseMode == TopLowPulseModes.CurrentIndependent
                ? settings.PulseTop
                : settings.PulseLow;
            p.TopHz = Math.Round(p.SpeedTable * p.TopPuls / 60.0, 0, MidpointRounding.AwayFromZero);
            p.LowPuls = settings.PulseLow;
            p.LowHz = Math.Round(p.SpeedTable * p.LowPuls / 60.0, 0, MidpointRounding.AwayFromZero);

            // Clamp pulses: if container == 0 -> form, else -> container (as in Excel)
            var clampDiam = p.Container ? p.DClampCont : p.DClampForm;
            p.ClampPuls = clampDiam * settings.PulseClamp;
        }
    }

    private static double CalculateRecommendedAlfa(string mode, double dz, double dr)
    {
        double deg;
        if (Math.Abs(dr) < 1e-9)
        {
            deg = 0;
        }
        else
        {
            deg = Math.Round(-Math.Atan(dz / dr) * 180.0 / ExcelPi, 0, MidpointRounding.AwayFromZero);
        }

        return mode == RecommendedAlfaModes.Minus90
            ? 90 - deg
            : deg + 90;
    }

    private static double Round1(double v)
        => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
