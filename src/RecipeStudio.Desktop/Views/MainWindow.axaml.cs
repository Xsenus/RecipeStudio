using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
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
            Close();
        }
    }

    private async Task<bool> AskConfirmationAsync(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog(title, message, confirmText);
        var result = await dialog.ShowDialog<bool>(this);
        return result;
    }
}
