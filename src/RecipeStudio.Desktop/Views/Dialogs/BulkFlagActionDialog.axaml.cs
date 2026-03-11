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

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void SetAll_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.SetAll);

    private void ClearAll_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.ClearAll);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(BulkFlagAction.Cancel);
}
