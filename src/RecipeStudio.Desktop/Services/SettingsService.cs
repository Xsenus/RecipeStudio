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

    private readonly AppLogger _logger;

    public string AppDataRoot { get; }
    public string SettingsPath { get; }

    public AppSettings Settings { get; private set; }

    public SettingsService(AppLogger logger)
    {
        _logger = logger;

        AppDataRoot = ResolveSettingsRoot();
        Directory.CreateDirectory(AppDataRoot);

        SettingsPath = Path.Combine(AppDataRoot, "settings.json");

        var hadLoadErrors = false;
        Settings = Load(out hadLoadErrors);

        EnsureDefaults();
        _logger.Configure(Settings.LoggingEnabled, Settings.LogRetentionDays, Settings.LogMode, Settings.LogsFolder);

        if (!File.Exists(SettingsPath) || hadLoadErrors)
        {
            Save();
            if (hadLoadErrors)
            {
                _logger.Warn("settings.json прочитан с ошибками, восстановлены настройки по умолчанию.");
            }
        }
    }

    public AppSettings Load() => Load(out _);

    public AppSettings Load(out bool hadErrors)
    {
        hadErrors = false;

        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            if (settings is null)
            {
                hadErrors = true;
                return new AppSettings();
            }

            if (!ValidateSettings(settings, out var validationError))
            {
                hadErrors = true;
                _logger.Warn($"settings.json не прошёл валидацию: {validationError}");
                return new AppSettings();
            }

            return settings;
        }
        catch (Exception ex)
        {
            hadErrors = true;
            _logger.Error("Ошибка чтения settings.json. Будут применены настройки по умолчанию.", ex);
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            EnsureDefaults();
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(SettingsPath, json);
            _logger.Configure(Settings.LoggingEnabled, Settings.LogRetentionDays, Settings.LogMode, Settings.LogsFolder);
        }
        catch (Exception ex)
        {
            _logger.Error("Ошибка сохранения settings.json.", ex);
        }
    }

    private void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(Settings.RecipesFolder))
        {
            Settings.RecipesFolder = Path.Combine(AppDataRoot, "recipes");
        }

        if (string.IsNullOrWhiteSpace(Settings.LogsFolder))
        {
            Settings.LogsFolder = Path.Combine(AppDataRoot, "logs");
        }

        Settings.LogMode = NormalizeLogMode(Settings.LogMode);
        Settings.LogRetentionDays = Math.Clamp(Settings.LogRetentionDays, 1, 3650);
        Settings.EditorPanels ??= new EditorPanelsSettings();
        Settings.EditorPanels.Parameters ??= new PanelPlacementSettings();
        Settings.EditorPanels.Visualization ??= new PanelPlacementSettings();
        Settings.EditorPanels.SelectedPoint ??= new PanelPlacementSettings();

        try
        {
            Directory.CreateDirectory(Settings.RecipesFolder);
            Directory.CreateDirectory(Settings.LogsFolder);
        }
        catch (Exception ex)
        {
            _logger.Error("Ошибка создания рабочих каталогов из настроек.", ex);
            Settings.RecipesFolder = Path.Combine(AppDataRoot, "recipes");
            Settings.LogsFolder = Path.Combine(AppDataRoot, "logs");
            Directory.CreateDirectory(Settings.RecipesFolder);
            Directory.CreateDirectory(Settings.LogsFolder);
        }
    }

    private static string ResolveSettingsRoot()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                var processDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (!string.IsNullOrWhiteSpace(processDir))
                {
                    return processDir;
                }
            }
        }
        catch
        {
            // ignore
        }

        return AppContext.BaseDirectory;
    }

    private static bool ValidateSettings(AppSettings s, out string error)
    {
        if (!IsFinite(s.HZone) || !IsFinite(s.HContMax) || !IsFinite(s.HBokMax) ||
            !IsFinite(s.Xm) || !IsFinite(s.Ym) || !IsFinite(s.Zm) || !IsFinite(s.Lz) ||
            !IsFinite(s.PulseX) || !IsFinite(s.PulseY) || !IsFinite(s.PulseZ) || !IsFinite(s.PulseA) ||
            !IsFinite(s.PulseB) || !IsFinite(s.PulseTop) || !IsFinite(s.PulseLow) || !IsFinite(s.PulseClamp) ||
            !IsFinite(s.PlotOpacity) || !IsFinite(s.PlotStrokeThickness) || !IsFinite(s.PlotPointRadius))
        {
            error = "Обнаружены нечисловые значения (NaN/Infinity).";
            return false;
        }

        if (s.PlotOpacity < 0.05 || s.PlotOpacity > 0.90)
        {
            error = "PlotOpacity вне допустимого диапазона 0.05..0.90.";
            return false;
        }

        if (s.SmoothSegmentsPerSpan is < 4 or > 64)
        {
            error = "SmoothSegmentsPerSpan вне диапазона 4..64.";
            return false;
        }

        if (s.LogRetentionDays is < 1 or > 3650)
        {
            error = "LogRetentionDays вне диапазона 1..3650.";
            return false;
        }

        if (NormalizeLogMode(s.LogMode) != s.LogMode && !string.IsNullOrWhiteSpace(s.LogMode))
        {
            error = "LogMode содержит недопустимое значение.";
            return false;
        }

        var panels = s.EditorPanels;
        if (panels is not null)
        {
            if (!ValidatePanel(panels.Parameters) || !ValidatePanel(panels.Visualization) || !ValidatePanel(panels.SelectedPoint))
            {
                error = "EditorPanels содержит недопустимые значения.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidatePanel(PanelPlacementSettings? panel)
    {
        if (panel is null)
            return false;

        return IsFinite(panel.Left) &&
               IsFinite(panel.Top) &&
               IsFinite(panel.Width) &&
               IsFinite(panel.Height) &&
               panel.Width >= 0 &&
               panel.Height >= 0;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static string NormalizeLogMode(string? mode)
    {
        return mode switch
        {
            LogSeverity.Error => LogSeverity.Error,
            LogSeverity.Warning => LogSeverity.Warning,
            _ => LogSeverity.Info
        };
    }
}
