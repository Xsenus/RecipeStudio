using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class InfoDialog : Window
{
    public InfoDialog(string title, string message, string okText = "OK")
    {
        InitializeComponent();
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        OkButton.Content = okText;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close();
}
