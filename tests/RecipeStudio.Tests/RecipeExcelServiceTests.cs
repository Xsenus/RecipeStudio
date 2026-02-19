using System;
using System.IO;
using ClosedXML.Excel;
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

}