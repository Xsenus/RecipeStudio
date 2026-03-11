using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RecipeStudio.Desktop.Views.Dialogs;

public enum BulkFlagAction
{
    Cancel = 0,
    SetAll = 1,
    ClearAll = 2
}

public sealed partial class BulkFlagActionDialog : Window
{
    public BulkFlagActionDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        if (Design.IsDesignMode)
        {
            TitleBlock.Text = "Массовое изменение";
            MessageBlock.Text = "Выставить для всех точек одинаковое значение?";
        }
    }

    public BulkFlagActionDialog(string title, string message)
        : this()
    {
        TitleBlock.Text = title;
        MessageBlock.Text = message;
    }

    public BulkFlagActionDialog(
        string title,
        string message,
        string setAllButtonText,
        string cancelButtonText,
        bool showClearAllButton)
        : this(title, message)
    {
        SetAllButton.Content = setAllButtonText;
        CancelButton.Content = cancelButtonText;
        ClearAllButton.IsVisible = showClearAllButton;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close(BulkFlagAction.Cancel);
    }

    private void SetAll_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.SetAll);

    private void ClearAll_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.ClearAll);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.Cancel);
}
