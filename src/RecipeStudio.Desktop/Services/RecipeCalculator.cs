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

            double deg;
            if (Math.Abs(dr) < 1e-9)
            {
                deg = 0;
            }
            else
            {
                deg = -Math.Atan(dz / dr) * 180.0 / ExcelPi;
            }

            cur.RecommendedAlfa = Math.Round(deg, 0, MidpointRounding.AwayFromZero) + 90;
        }

        // Derived UI values (t, V, recommended ice rate)
        for (var i = 0; i < doc.Points.Count; i++)
        {
            var p = doc.Points[i];
            p.TimeSec = p.SpeedTable == 0 ? 0 : 60.0 / p.SpeedTable;
            p.NozzleSpeedMmMin = Math.Round(p.RCrd * 2.0 * ExcelPi * p.SpeedTable, 0, MidpointRounding.AwayFromZero);
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
        var originPoint = doc.Points.FirstOrDefault(pt => pt.Act && !pt.Safe) ?? doc.Points[0];
        var originLn1 = settings.Lz + originPoint.ANozzle;
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
            var la = p.ANozzle;
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
            var zr = Round1(zp + cosB * sinA * ln1);

            var dx = 0d;
            var dy = 0d;
            var dz = 0d;
            var dA = 0d;
            var aB = 0d;

            if (!p.Safe)
            {
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
            }
            else
            {
                // Safe reference rows stay zeroed in exported CALC/SAVE block.
                p.Xr0 = 0;
                p.Yx0 = 0;
                p.Zr0 = 0;
            }

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

            p.TopPuls = settings.PulseTop;
            p.TopHz = Math.Round(p.SpeedTable * p.TopPuls / 60.0, 0, MidpointRounding.AwayFromZero);
            p.LowPuls = settings.PulseLow;
            p.LowHz = Math.Round(p.SpeedTable * p.LowPuls / 60.0, 0, MidpointRounding.AwayFromZero);

            // Clamp pulses: if container == 0 -> form, else -> container (as in Excel)
            var clampDiam = p.Container ? p.DClampCont : p.DClampForm;
            p.ClampPuls = clampDiam * settings.PulseClamp;
        }
    }

    private static double Round1(double v)
        => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
