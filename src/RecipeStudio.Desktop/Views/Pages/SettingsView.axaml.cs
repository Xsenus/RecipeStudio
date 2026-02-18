using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.ViewModels;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
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

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.RecipesFolder);
    }

    private void RecipesFolder_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        OpenFolder(vm.RecipesFolder);
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
