using Avalonia.Controls;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private async void DeleteRecipe_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel dashboard || sender is not Button { Tag: RecipeCardViewModel recipe })
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var dialog = new ConfirmDialog(
            "Удаление рецепта",
            $"Удалить рецепт '{recipe.Name}'?",
            "Удалить");

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (confirmed)
        {
            dashboard.DeleteRecipe(recipe.Id);
        }
    }
}
