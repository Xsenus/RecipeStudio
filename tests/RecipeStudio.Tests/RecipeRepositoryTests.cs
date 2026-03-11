using System;
using System.IO;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class RecipeRepositoryTests : IDisposable
{
    private readonly string _recipesFolder = Path.Combine(Path.GetTempPath(), "RecipeStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateAndLoad_PreservesCFlag()
    {
        var logger = new AppLogger();
        var settings = new SettingsService(logger);
        settings.Settings.AutoCreateSampleRecipeOnEmpty = false;
        settings.Settings.RecipesFolder = _recipesFolder;
        settings.Settings.LoggingEnabled = false;

        var repo = new RecipeRepository(settings);
        var doc = new RecipeDocument
        {
            RecipeCode = "REPO_C",
            ContainerPresent = true,
            DClampForm = 800,
            DClampCont = 1600
        };

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = "REPO_C",
            NPoint = 1,
            Act = true,
            Safe = false,
            C = true,
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

        var id = repo.Create(doc);
        var loaded = repo.Load(id);

        Assert.Single(loaded.Points);
        Assert.True(loaded.Points[0].C);
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
}
