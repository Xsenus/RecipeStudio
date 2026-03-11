using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using RecipeStudio.Desktop.Services;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;
    private const int VkMenu = 0x12;
    private const int VkX = 0x58;

    private SettingsService? _settings;
    private bool _closeConfirmed;
    private readonly Win32Properties.CustomWndProcHookCallback _wndProcHook;
    private bool _wndProcHookAttached;

    public MainWindow()
    {
        InitializeComponent();
        _wndProcHook = WndProcHook;
        Opened += OnOpened;
        Closed += OnClosed;

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

    private void OnOpened(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows() || _wndProcHookAttached)
        {
            return;
        }

        Win32Properties.AddWndProcHookCallback(this, _wndProcHook);
        _wndProcHookAttached = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows() || !_wndProcHookAttached)
        {
            return;
        }

        Win32Properties.RemoveWndProcHookCallback(this, _wndProcHook);
        _wndProcHookAttached = false;
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((msg != WmKeyDown && msg != WmSysKeyDown) || wParam.ToInt32() != VkX)
        {
            return IntPtr.Zero;
        }

        if (DataContext is not MainViewModel { CurrentPage: SettingsViewModel settingsVm })
        {
            return IntPtr.Zero;
        }

        if (!IsKeyPressed(VkControl) || !IsKeyPressed(VkShift) || !IsKeyPressed(VkMenu))
        {
            return IntPtr.Zero;
        }

        settingsVm.ToggleExcelCompatibilityVisibility();
        handled = true;
        return IntPtr.Zero;
    }

    private static bool IsKeyPressed(int virtualKey)
        => (GetKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

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
