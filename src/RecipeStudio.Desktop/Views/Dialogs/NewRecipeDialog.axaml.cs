using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class NewRecipeDialog : Window
{
    public NewRecipeDialog()
    {
        InitializeComponent();
    }

    public string RecipeName => (RecipeNameBox.Text ?? string.Empty).Trim();

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void Create_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RecipeNameBox.Text))
        {
            RecipeNameBox.Focus();
            return;
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
