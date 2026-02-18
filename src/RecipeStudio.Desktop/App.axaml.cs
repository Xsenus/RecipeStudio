using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views;

namespace RecipeStudio.Desktop;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var loggerRoot = ResolveAppRoot();
        var logger = new AppLogger(loggerRoot);
        logger.Info("Запуск приложения.");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.Error("UnhandledException (AppDomain)", ex);
            }
            else
            {
                logger.Error($"UnhandledException (AppDomain): {e.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService(logger);
            var repo = new RecipeRepository(settings);
            var excel = new RecipeExcelService();

            var mainVm = new MainViewModel(settings, repo, excel);

            desktop.MainWindow = new MainWindow(settings)
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string ResolveAppRoot()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                var processDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
                if (!string.IsNullOrWhiteSpace(processDir))
                {
                    return processDir;
                }
            }
        }
        catch
        {
            // ignored
        }

        return AppContext.BaseDirectory;
    }
}
