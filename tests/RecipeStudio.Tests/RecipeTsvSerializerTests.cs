using System;
using System.IO;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class RecipeTsvSerializerTests
{
    [Fact]
    public void SaveAndLoad_PreservesCFlag()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe_c_flag_{Guid.NewGuid():N}.tsv");

        try
        {
            var serializer = new RecipeTsvSerializer();
            var doc = new RecipeDocument
            {
                RecipeCode = "TSV_C",
                ContainerPresent = true,
                DClampForm = 800,
                DClampCont = 1600
            };

            doc.Points.Add(new RecipePoint
            {
                RecipeCode = "TSV_C",
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

            serializer.Save(doc, path);
            var loaded = serializer.Load(path);

            Assert.Single(loaded.Points);
            Assert.True(loaded.Points[0].C);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
