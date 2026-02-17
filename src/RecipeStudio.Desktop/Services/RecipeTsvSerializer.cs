using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed class RecipeTsvSerializer
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    // Canonical column order (matches the provided H340_KAMA_1.csv file)
    public static readonly string[] Columns =
    {
        "recipe_code",
        "n_point",
        "Act",
        "Safe",
        "r_crd",
        "z_crd",
        "place",
        "hidden",
        "a_nozzle",
        "alfa_crd",
        "betta_crd",
        "speed_table",
        "ice_rate",
        "ice_grind",
        "air_pressure",
        "air_temp",
        "container",
        "d_clamp_form",
        "d_clamp_cont",
        "description",

        // SAVE/CALC
        "Xr0",
        "Yx0",
        "Zr0",
        "dX",
        "dY",
        "dZ",
        "dA",
        "aB",
        "Xpuls",
        "Ypuls",
        "Zpuls",
        "Apuls",
        "Bpuls",
        "Top_puls",
        "Top_Hz",
        "Low_puls",
        "Low_Hz",
        "Clamp_puls",
    };

    public RecipeDocument Load(string path)
    {
        var doc = new RecipeDocument();
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            return doc;

        var header = SplitLine(lines[0]);
        var colIndex = header
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var row = SplitLine(lines[i]);
            var p = new RecipePoint
            {
                RecipeCode = Get(row, colIndex, "recipe_code") ?? "",
                NPoint = GetInt(row, colIndex, "n_point"),
                Act = GetInt(row, colIndex, "Act") != 0,
                Safe = GetInt(row, colIndex, "Safe") != 0,
                RCrd = GetDouble(row, colIndex, "r_crd"),
                ZCrd = GetDouble(row, colIndex, "z_crd"),
                Place = GetInt(row, colIndex, "place"),
                Hidden = GetInt(row, colIndex, "hidden") != 0,
                ANozzle = GetDouble(row, colIndex, "a_nozzle"),
                Alfa = GetDouble(row, colIndex, "alfa_crd"),
                Betta = GetDouble(row, colIndex, "betta_crd"),
                SpeedTable = GetDouble(row, colIndex, "speed_table"),
                IceRate = GetDouble(row, colIndex, "ice_rate"),
                IceGrind = GetDouble(row, colIndex, "ice_grind"),
                AirPressure = GetDouble(row, colIndex, "air_pressure"),
                AirTemp = GetDouble(row, colIndex, "air_temp"),
                Container = GetInt(row, colIndex, "container") != 0,
                DClampForm = GetDouble(row, colIndex, "d_clamp_form"),
                DClampCont = GetDouble(row, colIndex, "d_clamp_cont"),
                Description = Get(row, colIndex, "description"),

                // previously saved calculated values
                Xr0 = GetDouble(row, colIndex, "Xr0"),
                Yx0 = GetDouble(row, colIndex, "Yx0"),
                Zr0 = GetDouble(row, colIndex, "Zr0"),
                DX = GetDouble(row, colIndex, "dX"),
                DY = GetDouble(row, colIndex, "dY"),
                DZ = GetDouble(row, colIndex, "dZ"),
                DA = GetDouble(row, colIndex, "dA"),
                AB = GetDouble(row, colIndex, "aB"),
                XPuls = GetDouble(row, colIndex, "Xpuls"),
                YPuls = GetDouble(row, colIndex, "Ypuls"),
                ZPuls = GetDouble(row, colIndex, "Zpuls"),
                APuls = GetDouble(row, colIndex, "Apuls"),
                BPuls = GetDouble(row, colIndex, "Bpuls"),
                TopPuls = GetDouble(row, colIndex, "Top_puls"),
                TopHz = GetDouble(row, colIndex, "Top_Hz"),
                LowPuls = GetDouble(row, colIndex, "Low_puls"),
                LowHz = GetDouble(row, colIndex, "Low_Hz"),
                ClampPuls = GetDouble(row, colIndex, "Clamp_puls"),
            };

            if (string.IsNullOrWhiteSpace(doc.RecipeCode))
            {
                doc.RecipeCode = p.RecipeCode;
                doc.DClampForm = p.DClampForm;
                doc.DClampCont = p.DClampCont;
                doc.ContainerPresent = p.Container;
            }

            doc.Points.Add(p);
        }

        return doc;
    }

    public void Save(RecipeDocument doc, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var sw = new StreamWriter(path);
        sw.WriteLine(string.Join('\t', Columns));

        foreach (var p in doc.Points.OrderBy(x => x.NPoint))
        {
            // Keep recipe-level fields in sync
            p.RecipeCode = doc.RecipeCode;
            p.DClampForm = doc.DClampForm;
            p.DClampCont = doc.DClampCont;
            p.Container = doc.ContainerPresent;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["recipe_code"] = p.RecipeCode,
                ["n_point"] = p.NPoint.ToString(Ru),
                ["Act"] = (p.Act ? 1 : 0).ToString(Ru),
                ["Safe"] = (p.Safe ? 1 : 0).ToString(Ru),
                ["r_crd"] = Format(p.RCrd),
                ["z_crd"] = Format(p.ZCrd),
                ["place"] = p.Place.ToString(Ru),
                ["hidden"] = (p.Hidden ? 1 : 0).ToString(Ru),
                ["a_nozzle"] = Format(p.ANozzle),
                ["alfa_crd"] = Format(p.Alfa),
                ["betta_crd"] = Format(p.Betta),
                ["speed_table"] = Format(p.SpeedTable),
                ["ice_rate"] = Format(p.IceRate),
                ["ice_grind"] = Format(p.IceGrind),
                ["air_pressure"] = Format(p.AirPressure),
                ["air_temp"] = Format(p.AirTemp),
                ["container"] = (p.Container ? 1 : 0).ToString(Ru),
                ["d_clamp_form"] = Format(p.DClampForm),
                ["d_clamp_cont"] = Format(p.DClampCont),
                ["description"] = p.Description ?? "",

                ["Xr0"] = Format(p.Xr0),
                ["Yx0"] = Format(p.Yx0),
                ["Zr0"] = Format(p.Zr0),
                ["dX"] = Format(p.DX),
                ["dY"] = Format(p.DY),
                ["dZ"] = Format(p.DZ),
                ["dA"] = Format(p.DA),
                ["aB"] = Format(p.AB),
                ["Xpuls"] = Format(p.XPuls),
                ["Ypuls"] = Format(p.YPuls),
                ["Zpuls"] = Format(p.ZPuls),
                ["Apuls"] = Format(p.APuls),
                ["Bpuls"] = Format(p.BPuls),
                ["Top_puls"] = Format(p.TopPuls),
                ["Top_Hz"] = Format(p.TopHz),
                ["Low_puls"] = Format(p.LowPuls),
                ["Low_Hz"] = Format(p.LowHz),
                ["Clamp_puls"] = Format(p.ClampPuls),
            };

            var row = Columns.Select(c => values.TryGetValue(c, out var v) ? v : "");
            sw.WriteLine(string.Join('\t', row));
        }
    }

    private static string[] SplitLine(string line)
        => line.Split('\t');

    private static string? Get(string[] row, Dictionary<string, int> idx, string key)
    {
        if (!idx.TryGetValue(key, out var i)) return null;
        if (i < 0 || i >= row.Length) return null;
        return row[i];
    }

    private static int GetInt(string[] row, Dictionary<string, int> idx, string key)
    {
        var s = Get(row, idx, key);
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (int.TryParse(s, NumberStyles.Integer, Ru, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, Ru, out var dv)) return (int)Math.Round(dv);
        return 0;
    }

    private static double GetDouble(string[] row, Dictionary<string, int> idx, string key)
    {
        var s = Get(row, idx, key);
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (double.TryParse(s, NumberStyles.Any, Ru, out var v)) return v;

        // sometimes users paste values with '.' even in RU locale
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2))
            return v2;

        return 0;
    }

    private static string Format(double value)
        => value.ToString("0.###", Ru);
}
