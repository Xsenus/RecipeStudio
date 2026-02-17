using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed class RecipeStorageService
{
    private readonly SettingsService _settings;
    private readonly RecipeTsvSerializer _serializer = new();

    public RecipeStorageService(SettingsService settings)
    {
        _settings = settings;
        EnsureSampleRecipe();
    }

    public string RecipesFolder => _settings.Settings.RecipesFolder;

    public IEnumerable<string> EnumerateRecipeFiles()
    {
        if (!Directory.Exists(RecipesFolder))
            yield break;

        foreach (var file in Directory.EnumerateFiles(RecipesFolder, "*.csv", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(f => File.GetLastWriteTimeUtc(f)))
        {
            yield return file;
        }
    }

    public RecipeDocument Load(string filePath)
    {
        var doc = _serializer.Load(filePath);
        doc.FilePath = filePath;
        return doc;
    }

    public void Save(RecipeDocument doc)
    {
        if (string.IsNullOrWhiteSpace(doc.FilePath))
        {
            var fileName = string.IsNullOrWhiteSpace(doc.RecipeCode)
                ? $"recipe_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                : $"{doc.RecipeCode}.csv";

            doc.FilePath = Path.Combine(RecipesFolder, fileName);
        }

        _serializer.Save(doc, doc.FilePath);
    }

    public void Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // ignore
        }
    }

    public string CreateNewRecipePath(string recipeCode)
        => Path.Combine(RecipesFolder, $"{recipeCode}.csv");

    private void EnsureSampleRecipe()
    {
        try
        {
            Directory.CreateDirectory(RecipesFolder);

            // If there are already recipes - do nothing
            if (Directory.EnumerateFiles(RecipesFolder, "*.csv").Any())
                return;

            var samplePath = Path.Combine(RecipesFolder, "H340_KAMA_1.csv");
            if (File.Exists(samplePath))
                return;

            // 1) Try to copy the bundled sample from app resources.
            try
            {
                var uri = new Uri("avares://RecipeStudio.Desktop/Assets/Samples/H340_KAMA_1.csv");
                using var src = AssetLoader.Open(uri);
                using var dst = File.Create(samplePath);
                src.CopyTo(dst);
                return;
            }
            catch
            {
                // ignored -> fallback below
            }

            // 2) Fallback: create a small starter recipe.
            var starter = RecipeDocumentFactory.CreateStarter(recipeCode: "H340_KAMA_1");
            starter.FilePath = samplePath;
            _serializer.Save(starter, samplePath);
        }
        catch
        {
            // ignore
        }
    }
}
