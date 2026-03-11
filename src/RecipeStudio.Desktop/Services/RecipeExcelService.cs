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

    private sealed record ImportSheetContext(IXLWorksheet Worksheet, int HeaderRow, int DataStartRow);
    private static readonly string[] WorkbookCalcColumns = new[] { "n_point" }
        .Concat(RecipeFieldCatalog.CalcColumns)
        .ToArray();

    // Column keys used in Excel files.
    public static readonly string[] Columns = RecipeFieldCatalog.ExcelColumns;

    public void Export(RecipeDocument doc, string path, AppSettings? settings = null)
    {
        if (doc.Points.Count == 0)
            throw new InvalidOperationException("Recipe has no points to export.");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var wb = new XLWorkbook();
        settings ??= new AppSettings();
        var exportMode = ExcelExportModes.Normalize(settings.ExcelExportMode);

        if (exportMode == ExcelExportModes.FlatPoints)
            ExportFlatWorkbook(wb, doc);
        else
            ExportWorkbookLayout(wb, doc, settings);

        wb.SaveAs(path);
    }

    private static void ExportFlatWorkbook(XLWorkbook wb, RecipeDocument doc)
    {
        var ws = wb.AddWorksheet("Points");
        WriteHeaders(ws, Columns, headerRow: 1);

        var row = 2;
        foreach (var point in GetPreparedPoints(doc))
        {
            WriteRecipePointRow(ws, Columns, row, point);
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void ExportWorkbookLayout(XLWorkbook wb, RecipeDocument doc, AppSettings settings)
    {
        var points = GetPreparedPoints(doc).ToList();

        var ui = wb.AddWorksheet("UI");
        WriteHeaders(ui, Columns, headerRow: 1);
        WriteWorkbookDisplayHeaders(ui, Columns, headerRow: 2, GetWorkbookUiDisplayHeader);
        for (var i = 0; i < points.Count; i++)
            WriteRecipePointRow(ui, Columns, i + 3, points[i]);
        ui.SheetView.FreezeRows(2);
        ui.Columns().AdjustToContents();

        var calc = wb.AddWorksheet("CALC");
        WriteHeaders(calc, WorkbookCalcColumns, headerRow: 1);
        for (var i = 0; i < points.Count; i++)
            WriteCalcRow(calc, i + 2, points[i]);
        calc.SheetView.FreezeRows(1);
        calc.Columns().AdjustToContents();

        var save = wb.AddWorksheet("SAVE");
        WriteHeaders(save, RecipeFieldCatalog.TsvColumns, headerRow: 1);
        for (var i = 0; i < points.Count; i++)
            WriteRecipePointRow(save, RecipeFieldCatalog.TsvColumns, i + 2, points[i]);
        save.SheetView.FreezeRows(1);
        save.Columns().AdjustToContents();

        var constants = wb.AddWorksheet("CONST");
        constants.Cell(1, 1).Value = "name";
        constants.Cell(1, 2).Value = "value";
        var rows = BuildConstRows(settings);
        for (var i = 0; i < rows.Count; i++)
        {
            constants.Cell(i + 2, 1).Value = rows[i].Key;
            constants.Cell(i + 2, 2).Value = rows[i].Value;
        }

        constants.SheetView.FreezeRows(1);
        constants.Columns().AdjustToContents();
    }

    private static IEnumerable<RecipePoint> GetPreparedPoints(RecipeDocument doc)
    {
        foreach (var point in doc.Points.OrderBy(x => x.NPoint))
        {
            point.RecipeCode = doc.RecipeCode;
            point.DClampForm = doc.DClampForm;
            point.DClampCont = doc.DClampCont;
            point.Container = doc.ContainerPresent;
            yield return point;
        }
    }

    private static void WriteHeaders(IXLWorksheet ws, IReadOnlyList<string> columns, int headerRow)
    {
        for (var c = 0; c < columns.Count; c++)
            ws.Cell(headerRow, c + 1).Value = columns[c];
    }

    private static void WriteWorkbookDisplayHeaders(IXLWorksheet ws, IReadOnlyList<string> columns, int headerRow, Func<string, string> map)
    {
        for (var c = 0; c < columns.Count; c++)
            ws.Cell(headerRow, c + 1).Value = map(columns[c]);
    }

    private static void WriteRecipePointRow(IXLWorksheet ws, IReadOnlyList<string> columns, int row, RecipePoint p)
    {
        Write(ws, columns, row, "recipe_code", p.RecipeCode);
        Write(ws, columns, row, "n_point", p.NPoint);
        Write(ws, columns, row, "Act", p.Act ? 1 : 0);
        Write(ws, columns, row, "Safe", p.Safe ? 1 : 0);
        Write(ws, columns, row, "r_crd", p.RCrd);
        Write(ws, columns, row, "z_crd", p.ZCrd);
        Write(ws, columns, row, "place", p.Place);
        Write(ws, columns, row, "hidden", p.Hidden ? 1 : 0);
        Write(ws, columns, row, "a_nozzle", p.ANozzle);
        Write(ws, columns, row, "recommended_alfa", p.RecommendedAlfa);
        Write(ws, columns, row, "alfa_crd", p.Alfa);
        Write(ws, columns, row, "betta_crd", p.Betta);
        Write(ws, columns, row, "speed_table", p.SpeedTable);
        Write(ws, columns, row, "time_sec", p.TimeSec);
        Write(ws, columns, row, "v_mm_min", p.NozzleSpeedMmMin);
        Write(ws, columns, row, "recommended_ice_rate", p.RecommendedIceRate);
        Write(ws, columns, row, "ice_rate", p.IceRate);
        Write(ws, columns, row, "ice_grind", p.IceGrind);
        Write(ws, columns, row, "air_pressure", p.AirPressure);
        Write(ws, columns, row, "air_temp", p.AirTemp);
        Write(ws, columns, row, "container", p.Container ? 1 : 0);
        Write(ws, columns, row, "d_clamp_form", p.DClampForm);
        Write(ws, columns, row, "d_clamp_cont", p.DClampCont);
        Write(ws, columns, row, "description", p.Description ?? "");
        Write(ws, columns, row, "Xr0", p.Xr0);
        Write(ws, columns, row, "Yx0", p.Yx0);
        Write(ws, columns, row, "Zr0", p.Zr0);
        Write(ws, columns, row, "dX", p.DX);
        Write(ws, columns, row, "dY", p.DY);
        Write(ws, columns, row, "dZ", p.DZ);
        Write(ws, columns, row, "dA", p.DA);
        Write(ws, columns, row, "aB", p.AB);
        Write(ws, columns, row, "Xpuls", p.XPuls);
        Write(ws, columns, row, "Ypuls", p.YPuls);
        Write(ws, columns, row, "Zpuls", p.ZPuls);
        Write(ws, columns, row, "Apuls", p.APuls);
        Write(ws, columns, row, "Bpuls", p.BPuls);
        Write(ws, columns, row, "Top_puls", p.TopPuls);
        Write(ws, columns, row, "Top_Hz", p.TopHz);
        Write(ws, columns, row, "Low_puls", p.LowPuls);
        Write(ws, columns, row, "Low_Hz", p.LowHz);
        Write(ws, columns, row, "Clamp_puls", p.ClampPuls);
    }

    private static void WriteCalcRow(IXLWorksheet ws, int row, RecipePoint p)
    {
        Write(ws, WorkbookCalcColumns, row, "n_point", p.NPoint);
        Write(ws, WorkbookCalcColumns, row, "Xr0", p.Xr0);
        Write(ws, WorkbookCalcColumns, row, "Yx0", p.Yx0);
        Write(ws, WorkbookCalcColumns, row, "Zr0", p.Zr0);
        Write(ws, WorkbookCalcColumns, row, "dX", p.DX);
        Write(ws, WorkbookCalcColumns, row, "dY", p.DY);
        Write(ws, WorkbookCalcColumns, row, "dZ", p.DZ);
        Write(ws, WorkbookCalcColumns, row, "dA", p.DA);
        Write(ws, WorkbookCalcColumns, row, "aB", p.AB);
        Write(ws, WorkbookCalcColumns, row, "Xpuls", p.XPuls);
        Write(ws, WorkbookCalcColumns, row, "Ypuls", p.YPuls);
        Write(ws, WorkbookCalcColumns, row, "Zpuls", p.ZPuls);
        Write(ws, WorkbookCalcColumns, row, "Apuls", p.APuls);
        Write(ws, WorkbookCalcColumns, row, "Bpuls", p.BPuls);
        Write(ws, WorkbookCalcColumns, row, "Top_puls", p.TopPuls);
        Write(ws, WorkbookCalcColumns, row, "Top_Hz", p.TopHz);
        Write(ws, WorkbookCalcColumns, row, "Low_puls", p.LowPuls);
        Write(ws, WorkbookCalcColumns, row, "Low_Hz", p.LowHz);
        Write(ws, WorkbookCalcColumns, row, "Clamp_puls", p.ClampPuls);
    }

    private static List<(string Key, double Value)> BuildConstRows(AppSettings settings)
    {
        return new List<(string Key, double Value)>
        {
            ("H_zone", settings.HZone),
            ("H_cont", settings.HContMax),
            ("H_bok", settings.HBokMax),
            ("H_freeZ", settings.HFreeZ),
            ("Xm", settings.Xm),
            ("Ym", settings.Ym),
            ("Zm", settings.Zm),
            ("Lz", settings.Lz),
            ("vPulse_X", settings.PulseX),
            ("vPulse_Y", settings.PulseY),
            ("vPulse_Z", settings.PulseZ),
            ("vPulse_A", settings.PulseA),
            ("vPulse_B", settings.PulseB),
            ("vPulse_TOP", settings.PulseTop),
            ("vPulse_LOW", settings.PulseLow),
            ("vPulse_clamp", settings.PulseClamp),
        };
    }

    private static string GetWorkbookUiDisplayHeader(string key)
    {
        return key switch
        {
            "recipe_code" => "#",
            "n_point" => "Point",
            "Act" => "A",
            "Safe" => "Safe",
            "r_crd" => "R",
            "z_crd" => "Z",
            "place" => "Top",
            "hidden" => "Micro",
            "a_nozzle" => "a",
            "recommended_alfa" => "a rec",
            "alfa_crd" => "alfa",
            "betta_crd" => "beta",
            "speed_table" => "Speed",
            "time_sec" => "Time",
            "v_mm_min" => "Vel",
            "recommended_ice_rate" => "Flow",
            "ice_rate" => "Ice.R",
            "ice_grind" => "Ice.G",
            "air_pressure" => "Air.P",
            "air_temp" => "Air.T",
            "container" => "Container",
            "d_clamp_form" => "Clamp.F",
            "d_clamp_cont" => "Clamp.C",
            "description" => "Description",
            _ => key
        };
    }

    public RecipeDocument Import(string path)
        => ImportWithReport(path).Document;

    public RecipeImportResult ImportWithReport(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Excel file not found", path);

        using var wb = new XLWorkbook(path);
        var importSheet = ResolveImportSheet(wb);
        var ws = importSheet.Worksheet;

        var rawHeaders = ReadHeaders(ws, importSheet.HeaderRow);
        var (headerMap, duplicateHeaders) = BuildHeaderMap(rawHeaders);

        var aliasHits = NormalizeAliases(headerMap);
        var report = BuildReport(rawHeaders, duplicateHeaders, headerMap, aliasHits);

        var doc = new RecipeDocument();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = importSheet.DataStartRow; r <= lastRow; r++)
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

        if (string.IsNullOrWhiteSpace(doc.RecipeCode))
            doc.RecipeCode = Path.GetFileNameWithoutExtension(path);

        return new RecipeImportResult(doc, report);
    }

    private static ImportSheetContext ResolveImportSheet(XLWorkbook workbook)
    {
        var uiSheet = workbook.Worksheets.FirstOrDefault(IsWorkbookUiSheet);
        if (uiSheet is not null)
            return new ImportSheetContext(uiSheet, HeaderRow: 1, DataStartRow: 3);

        return new ImportSheetContext(workbook.Worksheets.First(), HeaderRow: 1, DataStartRow: 2);
    }

    private static bool IsWorkbookUiSheet(IXLWorksheet worksheet)
    {
        if (!string.Equals(worksheet.Name, "UI", StringComparison.OrdinalIgnoreCase))
            return false;

        var headers = ReadHeaders(worksheet, headerRow: 1);
        var headerSet = new HashSet<string>(
            headers.Where(h => !string.IsNullOrWhiteSpace(h)),
            StringComparer.OrdinalIgnoreCase);

        return headerSet.Contains("recipe_code") &&
               headerSet.Contains("n_point") &&
               RecipeFieldCatalog.RequiredImportColumns.All(headerSet.Contains);
    }

    private static List<string> ReadHeaders(IXLWorksheet ws, int headerRow)
    {
        var row = ws.Row(headerRow);
        var maxCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        var headers = new List<string>(Math.Max(1, maxCol));

        for (var c = 1; c <= maxCol; c++)
        {
            headers.Add(row.Cell(c).GetString().Trim());
        }

        return headers;
    }

    private static (Dictionary<string, int> Map, List<string> DuplicateHeaders) BuildHeaderMap(IReadOnlyList<string> rawHeaders)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rawHeaders.Count; i++)
        {
            var header = rawHeaders[i];
            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (map.ContainsKey(header))
                duplicates.Add(header);

            // Last duplicate wins (matches typical Excel “rightmost correction” expectation).
            map[header] = i + 1;
        }

        return (map, duplicates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static RecipeImportReport BuildReport(
        IReadOnlyList<string> rawHeaders,
        IReadOnlyList<string> duplicateHeaders,
        IReadOnlyDictionary<string, int> normalizedMap,
        IReadOnlyList<RecipeImportAliasHit> aliasHits)
    {
        var canonical = new HashSet<string>(Columns, StringComparer.OrdinalIgnoreCase);
        var aliasNames = new HashSet<string>(RecipeFieldCatalog.ExcelAliases.Keys, StringComparer.OrdinalIgnoreCase);

        var unknownHeaders = rawHeaders
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Where(h => !canonical.Contains(h) && !aliasNames.Contains(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingColumns = RecipeFieldCatalog.RequiredImportColumns
            .Where(req => !normalizedMap.ContainsKey(req))
            .ToList();

        return new RecipeImportReport(unknownHeaders, missingColumns, aliasHits, duplicateHeaders);
    }

    private static List<RecipeImportAliasHit> NormalizeAliases(Dictionary<string, int> map)
    {
        var hits = new List<RecipeImportAliasHit>();

        foreach (var (alias, canonical) in RecipeFieldCatalog.ExcelAliases)
        {
            if (map.ContainsKey(canonical))
                continue;

            if (!map.TryGetValue(alias, out var idx))
                continue;

            map[canonical] = idx;
            hits.Add(new RecipeImportAliasHit(alias, canonical, idx));
        }

        return hits;
    }

    private static void Write(IXLWorksheet ws, IReadOnlyList<string> columns, int row, string key, object? value)
    {
        var col = -1;
        for (var i = 0; i < columns.Count; i++)
        {
            if (!string.Equals(columns[i], key, StringComparison.Ordinal))
                continue;

            col = i;
            break;
        }

        if (col < 0)
            return;

        var cell = ws.Cell(row, col + 1);

        if (value is null)
        {
            cell.Clear();
            return;
        }

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

public sealed record RecipeImportAliasHit(string Alias, string Canonical, int ColumnIndex);

public sealed class RecipeImportReport
{
    public RecipeImportReport(
        IReadOnlyList<string> unknownHeaders,
        IReadOnlyList<string> missingRequiredColumns,
        IReadOnlyList<RecipeImportAliasHit> aliasHits,
        IReadOnlyList<string>? duplicateHeaders = null)
    {
        UnknownHeaders = unknownHeaders;
        MissingRequiredColumns = missingRequiredColumns;
        AliasHits = aliasHits;
        DuplicateHeaders = duplicateHeaders ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> UnknownHeaders { get; }
    public IReadOnlyList<string> MissingRequiredColumns { get; }
    public IReadOnlyList<RecipeImportAliasHit> AliasHits { get; }
    public IReadOnlyList<string> DuplicateHeaders { get; }

    public bool HasIssues => UnknownHeaders.Count > 0 || MissingRequiredColumns.Count > 0 || DuplicateHeaders.Count > 0;
}

public sealed record RecipeImportResult(RecipeDocument Document, RecipeImportReport Report);
