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
        AppDataRoot = ResolveSettingsRoot();

        Directory.CreateDirectory(AppDataRoot);

        SettingsPath = Path.Combine(AppDataRoot, "settings.json");

        TryMigrateLegacySettings();

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

    private void TryMigrateLegacySettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return;

            var legacyRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "recipe-studio");
            var legacyPath = Path.Combine(legacyRoot, "settings.json");

            if (!File.Exists(legacyPath))
                return;

            File.Copy(legacyPath, SettingsPath, overwrite: false);
        }
        catch
        {
            // ignore migration issues
        }
    }

    private static string ResolveSettingsRoot()
    {
        var executableFolder = TryGetExecutableFolder();

        if (!string.IsNullOrWhiteSpace(executableFolder) && CanWriteTo(executableFolder))
        {
            return executableFolder;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "recipe-studio");
    }

    private static string? TryGetExecutableFolder()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                return Path.GetDirectoryName(Environment.ProcessPath);
            }
        }
        catch
        {
            // ignore
        }

        return AppContext.BaseDirectory;
    }

    private static bool CanWriteTo(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probePath = Path.Combine(folder, ".settings-write-probe");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
