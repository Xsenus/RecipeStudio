using RecipeStudio.Desktop.Services;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void WindowsEnvironment_UsesLocalApplicationDataForAllRoots()
    {
        var env = new AppPathEnvironment(
            IsWindows: true,
            IsMacOS: false,
            IsLinux: false,
            HomeDirectory: @"C:\Users\Test",
            LocalApplicationData: @"C:\Users\Test\AppData\Local",
            ApplicationData: @"C:\Users\Test\AppData\Roaming",
            XdgStateHome: null);

        Assert.Equal(
            @"C:\Users\Test\AppData\Local\RecipeStudio",
            AppPaths.ResolveSettingsRoot(env));
        Assert.Equal(
            @"C:\Users\Test\AppData\Local\RecipeStudio",
            AppPaths.ResolveDataRoot(env));
        Assert.Equal(
            @"C:\Users\Test\AppData\Local\RecipeStudio\logs",
            AppPaths.ResolveLogsRoot(env));
    }

    [Fact]
    public void LinuxEnvironment_UsesConfigDataAndStateRoots()
    {
        var env = new AppPathEnvironment(
            IsWindows: false,
            IsMacOS: false,
            IsLinux: true,
            HomeDirectory: "/home/test",
            LocalApplicationData: "/home/test/.local/share",
            ApplicationData: "/home/test/.config",
            XdgStateHome: "/home/test/.local/state");

        Assert.Equal(
            "/home/test/.config/RecipeStudio",
            NormalizeSeparators(AppPaths.ResolveSettingsRoot(env)));
        Assert.Equal(
            "/home/test/.local/share/RecipeStudio",
            NormalizeSeparators(AppPaths.ResolveDataRoot(env)));
        Assert.Equal(
            "/home/test/.local/state/RecipeStudio/logs",
            NormalizeSeparators(AppPaths.ResolveLogsRoot(env)));
    }

    [Fact]
    public void LinuxEnvironment_WithoutStateHome_FallsBackToDataRootLogs()
    {
        var env = new AppPathEnvironment(
            IsWindows: false,
            IsMacOS: false,
            IsLinux: true,
            HomeDirectory: "/home/test",
            LocalApplicationData: "/home/test/.local/share",
            ApplicationData: "/home/test/.config",
            XdgStateHome: null);

        Assert.Equal(
            "/home/test/.local/share/RecipeStudio/logs",
            NormalizeSeparators(AppPaths.ResolveLogsRoot(env)));
    }

    private static string NormalizeSeparators(string path)
        => path.Replace('\\', '/');
}
