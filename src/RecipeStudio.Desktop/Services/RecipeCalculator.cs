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

            // Safe rows in Excel CALC are reference/clearance points:
            // they keep their own base and do not apply robot deltas.
            var useOwnBase = p.Safe;
            var xr0 = useOwnBase ? Round1(r0 - ln1) : xr0Base;
            var yx0 = useOwnBase ? 0d : yx0Base;
            var zr0 = useOwnBase ? Round1(z0) : zr0Base;

            var dx = useOwnBase ? 0d : Round1(xr - xr0Base);
            var dy = useOwnBase ? 0d : Round1(yx - yx0Base);
            var dz = useOwnBase ? 0d : Round1(zr - zr0Base);

            p.Xr0 = xr0;
            p.Yx0 = yx0;
            p.Zr0 = zr0;

            p.DX = dx;
            p.DY = dy;
            p.DZ = dz;

            p.DA = p.Alfa;
            p.AB = p.Betta;

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
