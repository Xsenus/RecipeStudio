using System;
using System.IO;

namespace RecipeStudio.Desktop.Services;

public readonly record struct AppPathEnvironment(
    bool IsWindows,
    bool IsMacOS,
    bool IsLinux,
    string? HomeDirectory,
    string? LocalApplicationData,
    string? ApplicationData,
    string? XdgStateHome);

public static class AppPaths
{
    public const string AppDirectoryName = "RecipeStudio";

    public static AppPathEnvironment CreateCurrentEnvironment()
    {
        return new AppPathEnvironment(
            OperatingSystem.IsWindows(),
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux(),
            EmptyToNull(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            EmptyToNull(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            EmptyToNull(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            EmptyToNull(Environment.GetEnvironmentVariable("XDG_STATE_HOME")));
    }

    public static string ResolveDataRoot() => ResolveDataRoot(CreateCurrentEnvironment());

    public static string ResolveSettingsRoot() => ResolveSettingsRoot(CreateCurrentEnvironment());

    public static string ResolveLogsRoot() => ResolveLogsRoot(CreateCurrentEnvironment());

    public static string ResolveDataRoot(AppPathEnvironment environment)
    {
        var root = FirstNonEmpty(
            environment.LocalApplicationData,
            environment.ApplicationData,
            environment.HomeDirectory is null ? null : Path.Combine(environment.HomeDirectory, ".local", "share"),
            AppContext.BaseDirectory);

        return Path.Combine(root, AppDirectoryName);
    }

    public static string ResolveSettingsRoot(AppPathEnvironment environment)
    {
        if (environment.IsWindows)
        {
            var root = FirstNonEmpty(
                environment.LocalApplicationData,
                environment.ApplicationData,
                environment.HomeDirectory is null ? null : Path.Combine(environment.HomeDirectory, "AppData", "Local"),
                AppContext.BaseDirectory);

            return Path.Combine(root, AppDirectoryName);
        }

        var nonWindowsRoot = FirstNonEmpty(
            environment.ApplicationData,
            environment.LocalApplicationData,
            environment.HomeDirectory is null ? null : Path.Combine(environment.HomeDirectory, ".config"),
            AppContext.BaseDirectory);

        return Path.Combine(nonWindowsRoot, AppDirectoryName);
    }

    public static string ResolveLogsRoot(AppPathEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(environment.XdgStateHome))
        {
            return Path.Combine(environment.XdgStateHome, AppDirectoryName, "logs");
        }

        return Path.Combine(ResolveDataRoot(environment), "logs");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
