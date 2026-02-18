using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.ViewModels;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        var recipesFolderBox = this.FindControl<TextBox>("RecipesFolderTextBox");
        recipesFolderBox?.AddHandler(InputElement.PointerPressedEvent, RecipesFolder_PointerPressed, RoutingStrategies.Tunnel, true);

        var logsFolderBox = this.FindControl<TextBox>("LogsFolderTextBox");
        logsFolderBox?.AddHandler(InputElement.PointerPressedEvent, LogsFolder_PointerPressed, RoutingStrategies.Tunnel, true);
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
