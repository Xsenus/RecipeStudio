using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class DashboardView : UserControl
{
    private DashboardViewModel? _subscribedVm;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.RequestCreateRecipe -= OnRequestCreateRecipe;
        }

        _subscribedVm = DataContext as DashboardViewModel;
        if (_subscribedVm is not null)
        {
            _subscribedVm.RequestCreateRecipe += OnRequestCreateRecipe;
        }
    }

    private async void OnRequestCreateRecipe()
    {
        if (DataContext is not DashboardViewModel dashboard)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var dialog = new NewRecipeDialog();
        var created = await dialog.ShowDialog<bool>(owner);
        if (created)
        {
            dashboard.CreateNewRecipe(dialog.RecipeName);
        }
    }

    private void RecipeCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: RecipeCardViewModel recipe })
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (recipe.OpenCommand.CanExecute(null))
        {
            recipe.OpenCommand.Execute(null);
        }
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
            "Удалить",
            "Отмена");

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (confirmed)
        {
            dashboard.DeleteRecipe(recipe.Id);
        }
    }
}
