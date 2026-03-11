using System;
using System.IO;
using System.Linq;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class EditorViewModelBulkFlagsTests : IDisposable
{
    private readonly string _recipesFolder = Path.Combine(Path.GetTempPath(), "RecipeStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RecipePoint_IsTop_KeepsPlaceInSync()
    {
        var point = new RecipePoint();

        point.IsTop = true;
        Assert.True(point.IsTop);
        Assert.Equal(1, point.Place);

        point.Place = 0;
        Assert.False(point.IsTop);
    }

    [Fact]
    public void BulkFlagMethods_ApplyValuesToAllPoints()
    {
        var vm = CreateViewModel();
        vm.LoadDocument(CreateDocument());

        vm.SetActForAll(false);
        vm.SetTopForAll(true);
        vm.SetHiddenForAll(true);

        Assert.All(vm.Points, point =>
        {
            Assert.False(point.Act);
            Assert.True(point.IsTop);
            Assert.Equal(1, point.Place);
            Assert.True(point.Hidden);
        });
    }

    [Fact]
    public void BulkNumericMethods_ApplyValuesToAllPoints()
    {
        var vm = CreateViewModel();
        vm.LoadDocument(CreateDocument());

        vm.SetANozzleForAll(42);
        vm.SetBettaForAll(-7.5);
        vm.SetSpeedTableForAll(6);
        vm.SetAirPressureForAll(8.2);
        vm.SetAirTempForAll(-15);

        Assert.All(vm.Points, point =>
        {
            Assert.Equal(42, point.ANozzle);
            Assert.Equal(-7.5, point.Betta);
            Assert.Equal(6, point.SpeedTable);
            Assert.Equal(8.2, point.AirPressure);
            Assert.Equal(-15, point.AirTemp);
        });
    }

    [Fact]
    public void SetTimeForAll_RecalculatesSpeedAndTime()
    {
        var vm = CreateViewModel();
        vm.LoadDocument(CreateDocument());

        vm.SetTimeForAll(15);

        Assert.All(vm.Points, point =>
        {
            Assert.Equal(4, point.SpeedTable, precision: 6);
            Assert.Equal(15, point.TimeSec, precision: 6);
        });
    }

    [Fact]
    public void ApplyRecommendedIceRateForAll_CopiesCurrentRecommendedValuesToIceRate()
    {
        var vm = CreateViewModel();
        vm.LoadDocument(CreateDocument());

        var expected = vm.Points.Select(point => point.RecommendedIceRate).ToArray();

        vm.ApplyRecommendedIceRateForAll();

        for (var i = 0; i < vm.Points.Count; i++)
            Assert.Equal(expected[i], vm.Points[i].IceRate, precision: 6);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_recipesFolder))
                Directory.Delete(_recipesFolder, recursive: true);
        }
        catch
        {
            // ignore test cleanup errors
        }
    }

    private EditorViewModel CreateViewModel()
    {
        var logger = new AppLogger();
        var settings = new SettingsService(logger);
        settings.Settings.AutoCreateSampleRecipeOnEmpty = false;
        settings.Settings.RecipesFolder = _recipesFolder;
        settings.Settings.LoggingEnabled = false;

        var repo = new RecipeRepository(settings);
        var excel = new RecipeExcelService();
        var importService = new RecipeImportService(excel, new RecipeTsvSerializer());
        return new EditorViewModel(settings, repo, excel, importService, () => { });
    }

    private static RecipeDocument CreateDocument()
    {
        var doc = new RecipeDocument
        {
            RecipeCode = "BULK",
            ContainerPresent = true,
            DClampForm = 800,
            DClampCont = 1600
        };

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "BULK",
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 364,
            ZCrd = 354,
            Place = 0,
            Hidden = false,
            ANozzle = 20,
            Alfa = -30,
            Betta = 12,
            SpeedTable = 3,
            IceRate = 100,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600
        });

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "BULK",
            NPoint = 2,
            Act = true,
            Safe = false,
            RCrd = 377,
            ZCrd = 337,
            Place = 0,
            Hidden = false,
            ANozzle = 20,
            Alfa = -15,
            Betta = 13,
            SpeedTable = 3,
            IceRate = 103.6,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600
        });

        return doc;
    }
}
