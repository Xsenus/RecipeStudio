using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private SettingsService? _settings;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            return;
        }
    }

    public MainWindow(SettingsService settings)
        : this()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        _settings = settings;

        ApplyWindowPlacementIfExists();

        PositionChanged += (_, _) => SaveWindowPlacementIfNeeded();
        SizeChanged += (_, _) => SaveWindowPlacementIfNeeded();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                SaveWindowPlacementIfNeeded();
            }
        };
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            SaveWindowPlacementBeforeClose();
            return;
        }

        e.Cancel = true;

        var shouldClose = await AskConfirmationAsync(
            "Закрыть программу",
            "Вы уверены, что хотите закрыть RecipeStudio?",
            "Закрыть");

        if (shouldClose)
        {
            _closeConfirmed = true;
            SaveWindowPlacementBeforeClose();
            Close();
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private async void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var shouldClose = await AskConfirmationAsync(
            "Закрыть программу",
            "Вы уверены, что хотите закрыть RecipeStudio?",
            "Закрыть");

        if (shouldClose)
        {
            _closeConfirmed = true;
            SaveWindowPlacementBeforeClose();
            Close();
        }
    }

    private async Task<bool> AskConfirmationAsync(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog(title, message, confirmText);
        var result = await dialog.ShowDialog<bool>(this);
        return result;
    }

    private void ApplyWindowPlacementIfExists()
    {
        if (_settings is null)
        {
            return;
        }

        var placement = _settings.Settings.WindowPlacement;

        if (placement.Width is not > 0 || placement.Height is not > 0 || placement.X is null || placement.Y is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowState = WindowState.Normal;
            return;
        }

        try
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = placement.Width.Value;
            Height = placement.Height.Value;
            Position = new PixelPoint(placement.X.Value, placement.Y.Value);
            WindowState = placement.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }
        catch
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowState = WindowState.Normal;
        }
    }

    private void SaveWindowPlacementIfNeeded()
    {
        if (_settings is null)
        {
            return;
        }

        var placement = _settings.Settings.WindowPlacement;

        if (WindowState == WindowState.Minimized)
        {
            return;
        }

        placement.IsMaximized = WindowState == WindowState.Maximized;

        if (WindowState != WindowState.Normal)
        {
            _settings.Save();
            return;
        }

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        placement.Width = Bounds.Width;
        placement.Height = Bounds.Height;
        placement.X = Position.X;
        placement.Y = Position.Y;
        _settings.Save();
    }

    private void SaveWindowPlacementBeforeClose()
    {
        if (_settings is null)
        {
            return;
        }

        var placement = _settings.Settings.WindowPlacement;

        if (WindowState == WindowState.Minimized)
        {
            placement.IsMaximized = false;
            placement.X = null;
            placement.Y = null;
            placement.Width = null;
            placement.Height = null;
            _settings.Save();
            return;
        }

        SaveWindowPlacementIfNeeded();
    }
}
