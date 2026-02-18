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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Services
            var settings = new SettingsService();
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
}
