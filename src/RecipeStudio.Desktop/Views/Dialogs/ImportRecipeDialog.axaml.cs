using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class ImportRecipeDialog : Window
{
    private readonly bool _allowRename;
    private readonly bool _isSuccessful;

    public string RecipeName => RecipeNameBox.Text?.Trim() ?? "";

    public ImportRecipeDialog(RecipeImportPreview preview, bool allowRename, string title)
    {
        InitializeComponent();

        _allowRename = allowRename;
        _isSuccessful = preview.IsSuccess && !preview.HasBlockingIssues;

        TitleBlock.Text = title;
        StatusBlock.Text = preview.Status;
        StatusBlock.Foreground = _isSuccessful ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.IndianRed;

        FileBlock.Text = $"Файл: {preview.FileName} ({preview.Extension})";
        PointsBlock.Text = $"Точек найдено: {preview.PointCount}";
        DiagnosticsBlock.Text = preview.Diagnostics;

        NamePanel.IsVisible = allowRename;
        RecipeNameBox.Text = preview.SuggestedRecipeName;

        ConfirmButton.Content = allowRename ? "Сохранить" : "Применить";
        ConfirmButton.IsEnabled = _isSuccessful && (!_allowRename || !string.IsNullOrWhiteSpace(RecipeName));

        if (!_isSuccessful)
        {
            ConfirmButton.Content = "Недоступно";
        }
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void RecipeNameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_allowRename)
        {
            return;
        }

        ConfirmButton.IsEnabled = _isSuccessful && !string.IsNullOrWhiteSpace(RecipeName);
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);
}
