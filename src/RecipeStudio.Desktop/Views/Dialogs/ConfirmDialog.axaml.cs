using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText = "OK", string cancelText = "Отмена")
    {
        InitializeComponent();
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
