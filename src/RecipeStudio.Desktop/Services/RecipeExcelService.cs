using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

/// <summary>
/// Import/export recipe points to/from Excel (.xlsx).
///
/// The exporter writes a single worksheet "Points" with a stable header.
/// The importer is header-driven (columns may be in any order).
/// </summary>
public sealed class RecipeExcelService
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    // Column keys used in Excel files.
    // Some names are compatible with the legacy TSV sample (e.g. r_crd, z_crd, alfa_crd...).
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

        // UI-calculated convenience
        "recommended_alfa",
        "alfa_crd",
        "betta_crd",
        "speed_table",
        "time_sec",
        "v_mm_min",
        "recommended_ice_rate",

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

    public void Export(RecipeDocument doc, string path)
    {
        if (doc.Points.Count == 0)
            throw new InvalidOperationException("Recipe has no points to export.");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Points");

        // Header
        for (var c = 0; c < Columns.Length; c++)
            ws.Cell(1, c + 1).Value = Columns[c];

        var row = 2;
        foreach (var p in doc.Points.OrderBy(x => x.NPoint))
        {
            // Keep recipe-level fields in sync
            p.RecipeCode = doc.RecipeCode;
            p.DClampForm = doc.DClampForm;
            p.DClampCont = doc.DClampCont;
            p.Container = doc.ContainerPresent;

            Write(ws, row, "recipe_code", p.RecipeCode);
            Write(ws, row, "n_point", p.NPoint);
            Write(ws, row, "Act", p.Act ? 1 : 0);
            Write(ws, row, "Safe", p.Safe ? 1 : 0);
            Write(ws, row, "r_crd", p.RCrd);
            Write(ws, row, "z_crd", p.ZCrd);
            Write(ws, row, "place", p.Place);
            Write(ws, row, "hidden", p.Hidden ? 1 : 0);
            Write(ws, row, "a_nozzle", p.ANozzle);

            Write(ws, row, "recommended_alfa", p.RecommendedAlfa);
            Write(ws, row, "alfa_crd", p.Alfa);
            Write(ws, row, "betta_crd", p.Betta);
            Write(ws, row, "speed_table", p.SpeedTable);
            Write(ws, row, "time_sec", p.TimeSec);
            Write(ws, row, "v_mm_min", p.NozzleSpeedMmMin);
            Write(ws, row, "recommended_ice_rate", p.RecommendedIceRate);

            Write(ws, row, "ice_rate", p.IceRate);
            Write(ws, row, "ice_grind", p.IceGrind);
            Write(ws, row, "air_pressure", p.AirPressure);
            Write(ws, row, "air_temp", p.AirTemp);
            Write(ws, row, "container", p.Container ? 1 : 0);
            Write(ws, row, "d_clamp_form", p.DClampForm);
            Write(ws, row, "d_clamp_cont", p.DClampCont);
            Write(ws, row, "description", p.Description ?? "");

            Write(ws, row, "Xr0", p.Xr0);
            Write(ws, row, "Yx0", p.Yx0);
            Write(ws, row, "Zr0", p.Zr0);
            Write(ws, row, "dX", p.DX);
            Write(ws, row, "dY", p.DY);
            Write(ws, row, "dZ", p.DZ);
            Write(ws, row, "dA", p.DA);
            Write(ws, row, "aB", p.AB);
            Write(ws, row, "Xpuls", p.XPuls);
            Write(ws, row, "Ypuls", p.YPuls);
            Write(ws, row, "Zpuls", p.ZPuls);
            Write(ws, row, "Apuls", p.APuls);
            Write(ws, row, "Bpuls", p.BPuls);
            Write(ws, row, "Top_puls", p.TopPuls);
            Write(ws, row, "Top_Hz", p.TopHz);
            Write(ws, row, "Low_puls", p.LowPuls);
            Write(ws, row, "Low_Hz", p.LowHz);
            Write(ws, row, "Clamp_puls", p.ClampPuls);

            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        wb.SaveAs(path);
    }

    public RecipeDocument Import(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Excel file not found", path);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();

        var headerRow = ws.Row(1);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 1; c <= ws.LastColumnUsed().ColumnNumber(); c++)
        {
            var name = headerRow.Cell(c).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;
            headerMap[name] = c;
        }

        // Accept some header aliases (common names / UI labels).
        NormalizeAliases(headerMap);

        var doc = new RecipeDocument();

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            if (row.CellsUsed().All(c => c.IsEmpty()))
                continue;

            var p = new RecipePoint
            {
                RecipeCode = GetString(row, headerMap, "recipe_code") ?? "",
                NPoint = GetInt(row, headerMap, "n_point"),
                Act = GetInt(row, headerMap, "Act") != 0,
                Safe = GetInt(row, headerMap, "Safe") != 0,

                RCrd = GetDouble(row, headerMap, "r_crd"),
                ZCrd = GetDouble(row, headerMap, "z_crd"),
                Place = GetInt(row, headerMap, "place"),
                Hidden = GetInt(row, headerMap, "hidden") != 0,

                ANozzle = GetDouble(row, headerMap, "a_nozzle"),
                RecommendedAlfa = GetDouble(row, headerMap, "recommended_alfa"),
                Alfa = GetDouble(row, headerMap, "alfa_crd"),
                Betta = GetDouble(row, headerMap, "betta_crd"),

                SpeedTable = GetDouble(row, headerMap, "speed_table"),
                TimeSec = GetDouble(row, headerMap, "time_sec"),
                NozzleSpeedMmMin = GetDouble(row, headerMap, "v_mm_min"),

                RecommendedIceRate = GetDouble(row, headerMap, "recommended_ice_rate"),
                IceRate = GetDouble(row, headerMap, "ice_rate"),
                IceGrind = GetDouble(row, headerMap, "ice_grind"),
                AirPressure = GetDouble(row, headerMap, "air_pressure"),
                AirTemp = GetDouble(row, headerMap, "air_temp"),

                Container = GetInt(row, headerMap, "container") != 0,
                DClampForm = GetDouble(row, headerMap, "d_clamp_form"),
                DClampCont = GetDouble(row, headerMap, "d_clamp_cont"),
                Description = GetString(row, headerMap, "description"),

                Xr0 = GetDouble(row, headerMap, "Xr0"),
                Yx0 = GetDouble(row, headerMap, "Yx0"),
                Zr0 = GetDouble(row, headerMap, "Zr0"),
                DX = GetDouble(row, headerMap, "dX"),
                DY = GetDouble(row, headerMap, "dY"),
                DZ = GetDouble(row, headerMap, "dZ"),
                DA = GetDouble(row, headerMap, "dA"),
                AB = GetDouble(row, headerMap, "aB"),
                XPuls = GetDouble(row, headerMap, "Xpuls"),
                YPuls = GetDouble(row, headerMap, "Ypuls"),
                ZPuls = GetDouble(row, headerMap, "Zpuls"),
                APuls = GetDouble(row, headerMap, "Apuls"),
                BPuls = GetDouble(row, headerMap, "Bpuls"),
                TopPuls = GetDouble(row, headerMap, "Top_puls"),
                TopHz = GetDouble(row, headerMap, "Top_Hz"),
                LowPuls = GetDouble(row, headerMap, "Low_puls"),
                LowHz = GetDouble(row, headerMap, "Low_Hz"),
                ClampPuls = GetDouble(row, headerMap, "Clamp_puls"),
            };

            if (string.IsNullOrWhiteSpace(doc.RecipeCode))
            {
                doc.RecipeCode = p.RecipeCode;
                doc.ContainerPresent = p.Container;
                doc.DClampForm = p.DClampForm;
                doc.DClampCont = p.DClampCont;
            }

            doc.Points.Add(p);
        }

        // If file didn't contain recipe_code in rows, use filename.
        if (string.IsNullOrWhiteSpace(doc.RecipeCode))
            doc.RecipeCode = Path.GetFileNameWithoutExtension(path);

        return doc;
    }

    private static void Write(IXLWorksheet ws, int row, string key, object? value)
    {
        var col = Array.IndexOf(Columns, key);
        if (col < 0)
            return;

        var cell = ws.Cell(row, col + 1);

        if (value is null)
        {
            cell.Clear();
            return;
        }

        // ClosedXML 0.102+ uses XLCellValue for IXLCell.Value.
        // Assign strongly typed values to keep numeric types numeric.
        switch (value)
        {
            case string s:
                cell.Value = s;
                break;
            case bool b:
                cell.Value = b;
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case float f:
                cell.Value = (double)f;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal m:
                cell.Value = (double)m;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static void NormalizeAliases(Dictionary<string, int> map)
    {
        // Allow some variations from older exports or user-renames.
        // Example: "α rec" or "alfa_rec" -> recommended_alfa
        CopyAlias(map, "α rec", "recommended_alfa");
        CopyAlias(map, "alfa_rec", "recommended_alfa");
        CopyAlias(map, "time", "time_sec");
        CopyAlias(map, "t_sec", "time_sec");
        CopyAlias(map, "v", "v_mm_min");
        CopyAlias(map, "V_mm_min", "v_mm_min");
        CopyAlias(map, "ice_rate_rec", "recommended_ice_rate");
    }

    private static void CopyAlias(Dictionary<string, int> map, string alias, string canonical)
    {
        if (map.ContainsKey(canonical))
            return;
        if (map.TryGetValue(alias, out var idx))
            map[canonical] = idx;
    }

    private static string? GetString(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col))
            return null;

        var s = row.Cell(col).GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static int GetInt(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col))
            return 0;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return 0;

        if (cell.DataType == XLDataType.Number)
            return (int)Math.Round(cell.GetDouble(), 0, MidpointRounding.AwayFromZero);

        var s = cell.GetString();
        if (int.TryParse(s, NumberStyles.Integer, Ru, out var v))
            return v;
        if (double.TryParse(s, NumberStyles.Any, Ru, out var dv))
            return (int)Math.Round(dv, 0, MidpointRounding.AwayFromZero);
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var dv2))
            return (int)Math.Round(dv2, 0, MidpointRounding.AwayFromZero);
        return 0;
    }

    private static double GetDouble(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col))
            return 0;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return 0;

        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble();

        // ClosedXML often returns text even for numeric-looking cells depending on formatting.
        var s = cell.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        if (double.TryParse(s, NumberStyles.Any, Ru, out var v))
            return v;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2))
            return v2;
        return 0;
    }
}
