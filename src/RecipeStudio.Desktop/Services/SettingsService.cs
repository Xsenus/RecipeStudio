using System;
using System.IO;
using System.Text.Json;

namespace RecipeStudio.Desktop.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public string AppDataRoot { get; }
    public string SettingsPath { get; }

    public AppSettings Settings { get; private set; }

    public SettingsService()
    {
        AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "recipe-studio");

        Directory.CreateDirectory(AppDataRoot);

        SettingsPath = Path.Combine(AppDataRoot, "settings.json");

        Settings = Load();

        if (string.IsNullOrWhiteSpace(Settings.RecipesFolder))
        {
            Settings.RecipesFolder = Path.Combine(AppDataRoot, "recipes");
            Directory.CreateDirectory(Settings.RecipesFolder);
            Save();
        }
        else
        {
            Directory.CreateDirectory(Settings.RecipesFolder);
        }
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch
        {
            // In a prototype we keep it resilient.
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // swallow in prototype
        }
    }
}
