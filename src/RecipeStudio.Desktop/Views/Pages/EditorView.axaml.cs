using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RecipeStudio.Desktop.Views.Dialogs;
using RecipeStudio.Desktop.ViewModels;

namespace RecipeStudio.Desktop.Views.Pages;

public sealed partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();

        DataContextChanged += (_, __) => HookVm();
        AttachedToVisualTree += (_, __) => HookVm();
    }

    private EditorViewModel? _vm;

    private void HookVm()
    {
        if (ReferenceEquals(_vm, DataContext))
            return;

        if (_vm is not null)
        {
            _vm.RequestImportExcel -= OnRequestImportExcel;
            _vm.RequestExportExcel -= OnRequestExportExcel;
            _vm.RequestShowCharts -= OnRequestShowCharts;
        }

        _vm = DataContext as EditorViewModel;
        if (_vm is not null)
        {
            _vm.RequestImportExcel += OnRequestImportExcel;
            _vm.RequestExportExcel += OnRequestExportExcel;
            _vm.RequestShowCharts += OnRequestShowCharts;
        }
    }

    private async void OnRequestShowCharts()
    {
        if (_vm is null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dialog = new RecipeAnalysisDialog(_vm.Points);
        await dialog.ShowDialog(owner);
    }

    private async void OnRequestImportExcel()
    {
        if (_vm is null) return;

        var top = TopLevel.GetTopLevel(this);
        var sp = top?.StorageProvider;
        if (sp is null) return;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Импорт рецепта из Excel",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel")
                {
                    Patterns = new[] { "*.xlsx" },
                    AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        // NOTE: runs on UI thread (simple prototype). If needed, we can move the heavy read to background
        // and apply results via Dispatcher.
        _vm.ImportFromExcel(path);
    }

    private async void OnRequestExportExcel()
    {
        if (_vm is null) return;

        var top = TopLevel.GetTopLevel(this);
        var sp = top?.StorageProvider;
        if (sp is null) return;

        var suggested = string.IsNullOrWhiteSpace(_vm.RecipeCode) ? "recipe" : _vm.RecipeCode;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт рецепта в Excel",
            SuggestedFileName = suggested + ".xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel")
                {
                    Patterns = new[] { "*.xlsx" },
                    AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
                }
            }
        });

        if (file is null) return;
        var path = file.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        _vm.ExportToExcel(path);
    }
}
