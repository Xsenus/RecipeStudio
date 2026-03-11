using System;
using System.IO;
using ClosedXML.Excel;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class RecipeExcelServiceTests
{
    [Fact]
    public void ImportWithReport_ResolvesKnownAliases_AndLoadsData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_alias_{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Points");
                ws.Cell(1, 1).Value = "recipe_code";
                ws.Cell(1, 2).Value = "n_point";
                ws.Cell(1, 3).Value = "r_crd";
                ws.Cell(1, 4).Value = "z_crd";
                ws.Cell(1, 5).Value = "a_nozz";
                ws.Cell(1, 6).Value = "alfa_crd";
                ws.Cell(1, 7).Value = "betta_c";
                ws.Cell(1, 8).Value = "speed_tab";
                ws.Cell(1, 9).Value = "ice_rate";

                ws.Cell(2, 1).Value = "H340_KAMA_1";
                ws.Cell(2, 2).Value = 1;
                ws.Cell(2, 3).Value = 364;
                ws.Cell(2, 4).Value = 354;
                ws.Cell(2, 5).Value = 20;
                ws.Cell(2, 6).Value = -30;
                ws.Cell(2, 7).Value = 12;
                ws.Cell(2, 8).Value = 3;
                ws.Cell(2, 9).Value = 100;

                wb.SaveAs(path);
            }

            var service = new RecipeExcelService();
            var result = service.ImportWithReport(path);

            Assert.Single(result.Document.Points);
            Assert.Equal(20, result.Document.Points[0].ANozzle);
            Assert.Equal(12, result.Document.Points[0].Betta);
            Assert.Equal(3, result.Document.Points[0].SpeedTable);
            Assert.True(result.Report.AliasHits.Count >= 2);
            Assert.Empty(result.Report.MissingRequiredColumns);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ImportWithReport_CollectsUnknownHeaders()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_unknown_{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Points");
                ws.Cell(1, 1).Value = "recipe_code";
                ws.Cell(1, 2).Value = "r_crd";
                ws.Cell(1, 3).Value = "z_crd";
                ws.Cell(1, 4).Value = "mystery_col";

                ws.Cell(2, 1).Value = "X";
                ws.Cell(2, 2).Value = 1;
                ws.Cell(2, 3).Value = 2;
                ws.Cell(2, 4).Value = 3;

                wb.SaveAs(path);
            }

            var service = new RecipeExcelService();
            var result = service.ImportWithReport(path);

            Assert.Contains("mystery_col", result.Report.UnknownHeaders);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }


    [Fact]
    public void ImportWithReport_HandlesDuplicateHeaders_WithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_dup_{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Points");
                ws.Cell(1, 1).Value = "recipe_code";
                ws.Cell(1, 2).Value = "r_crd";
                ws.Cell(1, 3).Value = "r_crd";
                ws.Cell(1, 4).Value = "z_crd";
                ws.Cell(1, 5).Value = "a_nozzle";
                ws.Cell(1, 6).Value = "alfa_crd";
                ws.Cell(1, 7).Value = "betta_crd";
                ws.Cell(1, 8).Value = "speed_table";
                ws.Cell(1, 9).Value = "ice_rate";

                ws.Cell(2, 1).Value = "X";
                ws.Cell(2, 2).Value = 111;
                ws.Cell(2, 3).Value = 222;
                ws.Cell(2, 4).Value = 10;
                ws.Cell(2, 5).Value = 20;
                ws.Cell(2, 6).Value = 30;
                ws.Cell(2, 7).Value = 40;
                ws.Cell(2, 8).Value = 5;
                ws.Cell(2, 9).Value = 7;

                wb.SaveAs(path);
            }

            var service = new RecipeExcelService();
            var result = service.ImportWithReport(path);

            Assert.Single(result.Document.Points);
            Assert.Equal(222, result.Document.Points[0].RCrd);
            Assert.Contains("r_crd", result.Report.DuplicateHeaders);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ImportWithReport_UsesWorkbookUiSheet_AndSkipsDisplayHeaderRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_ui_workbook_{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var wb = new XLWorkbook())
            {
                var intro = wb.AddWorksheet("Intro");
                intro.Cell(1, 1).Value = "placeholder";

                var ui = wb.AddWorksheet("UI");
                ui.Cell(1, 1).Value = "recipe_code";
                ui.Cell(1, 2).Value = "n_point";
                ui.Cell(1, 3).Value = "Act";
                ui.Cell(1, 4).Value = "Safe";
                ui.Cell(1, 5).Value = "C";
                ui.Cell(1, 6).Value = "r_crd";
                ui.Cell(1, 7).Value = "z_crd";
                ui.Cell(1, 8).Value = "place";
                ui.Cell(1, 9).Value = "hidden";
                ui.Cell(1, 10).Value = "a_nozzle";
                ui.Cell(1, 11).Value = "recommended_alfa";
                ui.Cell(1, 12).Value = "alfa_crd";
                ui.Cell(1, 13).Value = "betta_crd";
                ui.Cell(1, 14).Value = "speed_table";
                ui.Cell(1, 15).Value = "time_sec";
                ui.Cell(1, 16).Value = "v_mm_min";
                ui.Cell(1, 17).Value = "recommended_ice_rate";
                ui.Cell(1, 18).Value = "ice_rate";

                ui.Cell(2, 1).Value = "#";
                ui.Cell(2, 2).Value = "Точка";
                ui.Cell(2, 3).Value = "Актив.";
                ui.Cell(2, 4).Value = "Safe";
                ui.Cell(2, 5).Value = "C";
                ui.Cell(2, 6).Value = "R";
                ui.Cell(2, 7).Value = "Z";
                ui.Cell(2, 8).Value = "Top";
                ui.Cell(2, 9).Value = "Micro";
                ui.Cell(2, 10).Value = "a";
                ui.Cell(2, 11).Value = "α rec";
                ui.Cell(2, 12).Value = "α";
                ui.Cell(2, 13).Value = "β";
                ui.Cell(2, 14).Value = "ω";
                ui.Cell(2, 15).Value = "t";
                ui.Cell(2, 16).Value = "V";
                ui.Cell(2, 17).Value = "Flow";
                ui.Cell(2, 18).Value = "Ice.R";

                ui.Cell(3, 1).Value = "H340";
                ui.Cell(3, 2).Value = 1;
                ui.Cell(3, 3).Value = 1;
                ui.Cell(3, 4).Value = 0;
                ui.Cell(3, 5).Value = 1;
                ui.Cell(3, 6).Value = 364;
                ui.Cell(3, 7).Value = 354;
                ui.Cell(3, 8).Value = 0;
                ui.Cell(3, 9).Value = 0;
                ui.Cell(3, 10).Value = 20;
                ui.Cell(3, 11).Value = 0;
                ui.Cell(3, 12).Value = -30;
                ui.Cell(3, 13).Value = 12;
                ui.Cell(3, 14).Value = 3;
                ui.Cell(3, 15).Value = 20;
                ui.Cell(3, 16).Value = 6857.76;
                ui.Cell(3, 17).Value = 100;
                ui.Cell(3, 18).Value = 100;

                wb.SaveAs(path);
            }

            var service = new RecipeExcelService();
            var result = service.ImportWithReport(path);

            Assert.Single(result.Document.Points);
            Assert.Equal("H340", result.Document.RecipeCode);
            Assert.Equal(1, result.Document.Points[0].NPoint);
            Assert.True(result.Document.Points[0].C);
            Assert.Equal(364, result.Document.Points[0].RCrd);
            Assert.Equal(354, result.Document.Points[0].ZCrd);
            Assert.Equal(20, result.Document.Points[0].ANozzle);
            Assert.Equal(20, result.Document.Points[0].TimeSec);
            Assert.Equal(6857.76, result.Document.Points[0].NozzleSpeedMmMin);
            Assert.Empty(result.Report.MissingRequiredColumns);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_WritesWorkbookSheets_InExcelMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_export_workbook_{Guid.NewGuid():N}.xlsx");

        try
        {
            var service = new RecipeExcelService();
            var doc = CreateDocument();

            service.Export(doc, path, new AppSettings());

            using var wb = new XLWorkbook(path);
            Assert.NotNull(wb.Worksheet("UI"));
            Assert.NotNull(wb.Worksheet("CALC"));
            Assert.NotNull(wb.Worksheet("CONST"));
            Assert.NotNull(wb.Worksheet("SAVE"));
            Assert.Equal("recipe_code", wb.Worksheet("UI").Cell(1, 1).GetString());
            Assert.Equal("C", wb.Worksheet("UI").Cell(1, 5).GetString());
            Assert.Equal("C", wb.Worksheet("UI").Cell(2, 5).GetString());
            Assert.Equal("Ice.R", wb.Worksheet("UI").Cell(2, 18).GetString());
            Assert.Equal(1, wb.Worksheet("UI").Cell(3, 5).GetDouble(), precision: 6);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_WritesFlatPointsSheet_InCurrentMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_export_points_{Guid.NewGuid():N}.xlsx");

        try
        {
            var service = new RecipeExcelService();
            var doc = CreateDocument();

            service.Export(doc, path, new AppSettings
            {
                ExcelExportMode = ExcelExportModes.FlatPoints
            });

            using var wb = new XLWorkbook(path);
            Assert.Single(wb.Worksheets);
            Assert.Equal("Points", wb.Worksheet(1).Name);
            Assert.Equal("recipe_code", wb.Worksheet(1).Cell(1, 1).GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static RecipeDocument CreateDocument()
    {
        var doc = new RecipeDocument
        {
            RecipeCode = "EXPORT",
            ContainerPresent = true,
            DClampForm = 800,
            DClampCont = 1600
        };

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "EXPORT",
            NPoint = 1,
            Act = true,
            Safe = false,
            C = true,
            RCrd = 364,
            ZCrd = 354,
            Place = 0,
            Hidden = false,
            ANozzle = 20,
            RecommendedAlfa = 0,
            Alfa = -30,
            Betta = 12,
            SpeedTable = 3,
            TimeSec = 20,
            NozzleSpeedMmMin = 6857.76,
            RecommendedIceRate = 100,
            IceRate = 100,
            IceGrind = 1,
            AirPressure = 5,
            AirTemp = 20,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600,
            Xr0 = 94,
            Yx0 = 0,
            Zr0 = 354
        });

        return doc;
    }

}
