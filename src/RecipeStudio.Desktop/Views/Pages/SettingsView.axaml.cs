using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.ViewModels;
using RecipeStudio.Desktop.Views.Dialogs;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class SettingsView : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);

        var recipesFolderBox = this.FindControl<TextBox>("RecipesFolderTextBox");
        recipesFolderBox?.AddHandler(InputElement.PointerPressedEvent, RecipesFolder_PointerPressed, RoutingStrategies.Tunnel, true);

        var logsFolderBox = this.FindControl<TextBox>("LogsFolderTextBox");
        logsFolderBox?.AddHandler(InputElement.PointerPressedEvent, LogsFolder_PointerPressed, RoutingStrategies.Tunnel, true);

        DataContextChanged += OnDataContextChanged;
    }


    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.RequestCreateSampleRecipe -= OnRequestCreateSampleRecipe;
        }

        _vm = DataContext as SettingsViewModel;
        if (_vm is not null)
        {
            _vm.RequestCreateSampleRecipe += OnRequestCreateSampleRecipe;
        }
    }

    private async void OnRequestCreateSampleRecipe()
    {
        if (_vm is null)
            return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var confirm = new ConfirmDialog(
            "Создание образца",
            "Создать образец рецепта H340_KAMA_1?",
            "Создать",
            "Отмена");

        var confirmed = await confirm.ShowDialog<bool>(owner);
        if (!confirmed)
            return;

        var created = _vm.CreateSampleRecipe();
        var info = new InfoDialog(
            created ? "Готово" : "Ошибка",
            created ? "Образец рецепта успешно создан." : "Не удалось создать образец рецепта.",
            "Закрыть");

        await info.ShowDialog(owner);
    }

    private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку хранения рецептов",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            vm.RecipesFolder = result[0].Path.LocalPath;
        }
    }

    private async void BrowseLogsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку для логов",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            vm.LogsFolder = result[0].Path.LocalPath;
        }
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.RecipesFolder);
    }

    private void OpenLogsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.LogsFolder);
    }

    private void RecipesFolder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount < 2)
            return;

        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.RecipesFolder);
    }

    private void LogsFolder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount < 2)
            return;

        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.LogsFolder);
    }

    private void SettingsTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        if (e.AddedItems.Count == 0)
            return;

        if (e.AddedItems[0] is TreeViewItem item && item.Tag is string section)
        {
            vm.SelectedSection = section;
        }
    }

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", path);
            }
        }
        catch
        {
            // ignore in prototype
        }
    }
}
