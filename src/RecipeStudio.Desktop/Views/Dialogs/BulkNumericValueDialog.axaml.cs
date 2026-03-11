using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class BulkNumericValueDialog : Window
{
    public BulkNumericValueDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        if (Design.IsDesignMode)
        {
            TitleBlock.Text = "Массовое изменение";
            MessageBlock.Text = "Выставить для всех точек одинаковое значение?";
            ValueBox.Value = 0;
        }
    }

    public BulkNumericValueDialog(
        string title,
        string message,
        double initialValue,
        double minimum,
        double maximum,
        double increment = 0.1,
        string formatString = "0.###")
        : this()
    {
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ValueBox.Minimum = (decimal)minimum;
        ValueBox.Maximum = (decimal)maximum;
        ValueBox.Increment = (decimal)increment;
        ValueBox.FormatString = formatString;
        ValueBox.Value = (decimal)initialValue;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close(null);
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
        => Close((double)(ValueBox.Value ?? 0m));

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(null);
}
