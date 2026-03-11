using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed record CncInstructionColumn(string Key, string Header, double Width);

public static class CncInstructionService
{
    private const double ExcelTrigPi = 3.14159;
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<CncInstructionColumn> Columns { get; } = new[]
    {
        new CncInstructionColumn("n_point", "n_point", 70),
        new CncInstructionColumn("Safe", "Safe", 55),
        new CncInstructionColumn("Xm", "Xm", 80),
        new CncInstructionColumn("Ym", "Ym", 80),
        new CncInstructionColumn("Zm", "Zm", 80),
        new CncInstructionColumn("Ln", "Ln", 70),
        new CncInstructionColumn("La", "La", 70),
        new CncInstructionColumn("Ln1", "Ln1", 75),
        new CncInstructionColumn("Alfa", "Alfa", 70),
        new CncInstructionColumn("SinA", "SinA", 95),
        new CncInstructionColumn("CosA", "CosA", 95),
        new CncInstructionColumn("Betta", "Betta", 70),
        new CncInstructionColumn("SinB", "SinB", 95),
        new CncInstructionColumn("CosB", "CosB", 95),
        new CncInstructionColumn("r0", "r0", 70),
        new CncInstructionColumn("z0", "z0", 70),
        new CncInstructionColumn("place", "place", 65),
        new CncInstructionColumn("Xp", "Xp", 70),
        new CncInstructionColumn("Zp", "Zp", 70),
        new CncInstructionColumn("Xr0", "Xr0", 70),
        new CncInstructionColumn("Yx0", "Yx0", 70),
        new CncInstructionColumn("Zr0", "Zr0", 70),
        new CncInstructionColumn("Xr", "Xr", 70),
        new CncInstructionColumn("Yx", "Yx", 70),
        new CncInstructionColumn("Zr", "Zr", 70),
        new CncInstructionColumn("dX", "dX", 70),
        new CncInstructionColumn("dY", "dY", 70),
        new CncInstructionColumn("dZ", "dZ", 70),
        new CncInstructionColumn("dA", "dA", 70),
        new CncInstructionColumn("aB", "aB", 70),
        new CncInstructionColumn("Xpuls", "Xpuls", 80),
        new CncInstructionColumn("Ypuls", "Ypuls", 80),
        new CncInstructionColumn("Zpuls", "Zpuls", 80),
        new CncInstructionColumn("Apuls", "Apuls", 80),
        new CncInstructionColumn("Bpuls", "Bpuls", 80),
        new CncInstructionColumn("speed_table", "speed_table", 95),
        new CncInstructionColumn("Top_puls", "Top_puls", 90),
        new CncInstructionColumn("Top_Hz", "Top_Hz", 85),
        new CncInstructionColumn("Low_puls", "Low_puls", 90),
        new CncInstructionColumn("Low_Hz", "Low_Hz", 85),
        new CncInstructionColumn("container", "container", 80),
        new CncInstructionColumn("d_clamp_form", "d_clamp_form", 105),
        new CncInstructionColumn("d_clamp_cont", "d_clamp_cont", 105),
        new CncInstructionColumn("Clamp_puls", "Clamp_puls", 105),
    };

    public static IReadOnlyList<CncInstructionRow> BuildRows(RecipeDocument doc, AppSettings settings)
    {
        if (doc.Points.Count == 0)
            return Array.Empty<CncInstructionRow>();

        var points = doc.Points
            .OrderBy(x => x.NPoint)
            .ToList();

        var calculationOriginMode = CalculationOriginModes.Normalize(settings.CalculationOriginMode);
        var aNozzleKinematicsMode = ANozzleKinematicsModes.Normalize(settings.ANozzleKinematicsMode);
        var topLowPulseMode = TopLowPulseModes.Normalize(settings.TopLowPulseMode);

        var originPoint = calculationOriginMode == CalculationOriginModes.CurrentFirstWorking
            ? points.FirstOrDefault(pt => pt.Act && !pt.Safe) ?? points[0]
            : points[0];

        var firstRowANozzle = points[0].ANozzle;
        var originANozzle = aNozzleKinematicsMode == ANozzleKinematicsModes.CurrentPerPoint
            ? originPoint.ANozzle
            : firstRowANozzle;

        var originLn1 = settings.Lz + originANozzle;
        var xr0Base = Round1(originPoint.RCrd - originLn1);
        var yx0Base = 0d;
        var zr0Base = Round1(originPoint.ZCrd);

        var prevXr = xr0Base;
        var prevYx = yx0Base;
        var prevZr = zr0Base;
        var prevAlfa = 0d;
        var prevBetta = 0d;

        var rows = new List<CncInstructionRow>(points.Count);
        foreach (var point in points)
        {
            var xp = point.Place == 0 ? point.RCrd : -point.RCrd;
            var zp = point.Place == 0 ? point.ZCrd : settings.HZone - point.ZCrd;

            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["n_point"] = point.NPoint.ToString(Ru),
                ["Safe"] = FormatFlag(point.Safe),
                ["place"] = point.Place.ToString(Ru),
                ["Xp"] = FormatNumber(xp, "0.###"),
                ["Zp"] = FormatNumber(zp, "0.###"),
            };

            if (point.Safe)
            {
                rows.Add(new CncInstructionRow(values));
                continue;
            }

            var ln = settings.Lz;
            var la = aNozzleKinematicsMode == ANozzleKinematicsModes.CurrentPerPoint
                ? point.ANozzle
                : firstRowANozzle;
            var ln1 = ln + la;

            var alfaRad = point.Alfa * ExcelTrigPi / 180.0;
            var bettaRad = point.Betta * ExcelTrigPi / 180.0;

            var sinA = Math.Sin(alfaRad);
            var cosA = Math.Cos(alfaRad);
            var sinB = Math.Sin(bettaRad);
            var cosB = Math.Cos(bettaRad);

            var xr = Round1(xp - cosB * cosA * ln1);
            var yx = Round1(-sinB * ln1);
            var zrOffset = cosB * sinA * ln1;
            var zr = point.Place == 0
                ? Round1(zp + zrOffset)
                : Round1(zp - zrOffset);

            var dx = Round1(xr - prevXr);
            var dy = Round1(yx - prevYx);
            var dz = Round1(zr - prevZr);
            var dA = Round1(point.Alfa - prevAlfa);
            var aB = Round1(point.Betta - prevBetta);

            prevXr = xr;
            prevYx = yx;
            prevZr = zr;
            prevAlfa = point.Alfa;
            prevBetta = point.Betta;

            var topPuls = topLowPulseMode == TopLowPulseModes.CurrentIndependent
                ? settings.PulseTop
                : settings.PulseLow;
            var lowPuls = settings.PulseLow;
            var topHz = Math.Round(point.SpeedTable * topPuls / 60.0, 0, MidpointRounding.AwayFromZero);
            var lowHz = Math.Round(point.SpeedTable * lowPuls / 60.0, 0, MidpointRounding.AwayFromZero);
            var clampPuls = (point.Container ? point.DClampCont : point.DClampForm) * settings.PulseClamp;

            values["Xm"] = FormatNumber(settings.Xm, "0.###");
            values["Ym"] = FormatNumber(settings.Ym, "0.###");
            values["Zm"] = FormatNumber(settings.Zm, "0.###");
            values["Ln"] = FormatNumber(ln, "0.###");
            values["La"] = FormatNumber(la, "0.###");
            values["Ln1"] = FormatNumber(ln1, "0.###");
            values["Alfa"] = FormatNumber(point.Alfa, "0.###");
            values["SinA"] = FormatNumber(sinA, "0.########");
            values["CosA"] = FormatNumber(cosA, "0.########");
            values["Betta"] = FormatNumber(point.Betta, "0.###");
            values["SinB"] = FormatNumber(sinB, "0.########");
            values["CosB"] = FormatNumber(cosB, "0.########");
            values["r0"] = FormatNumber(originPoint.RCrd, "0.###");
            values["z0"] = FormatNumber(originPoint.ZCrd, "0.###");
            values["Xr0"] = FormatNumber(xr0Base, "0.###");
            values["Yx0"] = FormatNumber(yx0Base, "0.###");
            values["Zr0"] = FormatNumber(zr0Base, "0.###");
            values["Xr"] = FormatNumber(xr, "0.###");
            values["Yx"] = FormatNumber(yx, "0.###");
            values["Zr"] = FormatNumber(zr, "0.###");
            values["dX"] = FormatNumber(dx, "0.###");
            values["dY"] = FormatNumber(dy, "0.###");
            values["dZ"] = FormatNumber(dz, "0.###");
            values["dA"] = FormatNumber(dA, "0.###");
            values["aB"] = FormatNumber(aB, "0.###");
            values["Xpuls"] = FormatNumber(dx * settings.PulseX, "0.###");
            values["Ypuls"] = FormatNumber(dy * settings.PulseY, "0.###");
            values["Zpuls"] = FormatNumber(dz * settings.PulseZ, "0.###");
            values["Apuls"] = FormatNumber(dA * settings.PulseA, "0.###");
            values["Bpuls"] = FormatNumber(aB * settings.PulseB, "0.###");
            values["speed_table"] = FormatNumber(point.SpeedTable, "0.###");
            values["Top_puls"] = FormatNumber(topPuls, "0.###");
            values["Top_Hz"] = FormatNumber(topHz, "0.###");
            values["Low_puls"] = FormatNumber(lowPuls, "0.###");
            values["Low_Hz"] = FormatNumber(lowHz, "0.###");
            values["container"] = FormatFlag(point.Container);
            values["d_clamp_form"] = FormatNumber(point.DClampForm, "0.###");
            values["d_clamp_cont"] = FormatNumber(point.DClampCont, "0.###");
            values["Clamp_puls"] = FormatNumber(clampPuls, "0.###");

            rows.Add(new CncInstructionRow(values));
        }

        return rows;
    }

    private static string FormatFlag(bool value)
        => value ? "1" : "0";

    private static string FormatNumber(double value, string format)
    {
        if (Math.Abs(value) < 1e-9)
            value = 0;

        return value.ToString(format, Ru);
    }

    private static double Round1(double value)
        => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
