using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class ColorPickerDialog : Window
{
    private bool _isUpdating;

    public ColorPickerDialog()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            SetFromColor(Color.FromRgb(203, 213, 225));
        }

        HexBox.LostFocus += (_, _) => ApplyHexFromText();
        RSlider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) UpdateFromSliders(); };
        GSlider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) UpdateFromSliders(); };
        BSlider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) UpdateFromSliders(); };
    }

    public ColorPickerDialog(string initialHex)
        : this()
    {
        if (!TryParseHex(initialHex, out var color))
        {
            color = Color.FromRgb(34, 197, 94);
        }

        SetFromColor(color);
    }

    public string SelectedHex { get; private set; } = "#22C55E";

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        ApplyHexFromText();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void SetFromColor(Color color)
    {
        _isUpdating = true;
        try
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            SelectedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            HexBox.Text = SelectedHex;
            PreviewBorder.Background = new SolidColorBrush(color);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateFromSliders()
    {
        if (_isUpdating)
        {
            return;
        }

        var color = Color.FromRgb((byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value);
        SelectedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        HexBox.Text = SelectedHex;
        PreviewBorder.Background = new SolidColorBrush(color);
    }

    private void ApplyHexFromText()
    {
        var text = HexBox.Text?.Trim() ?? string.Empty;
        if (!TryParseHex(text, out var color))
        {
            return;
        }

        SetFromColor(color);
    }

    private static bool TryParseHex(string value, out Color color)
    {
        if (!value.StartsWith("#"))
        {
            value = "#" + value;
        }

        if (Color.TryParse(value, out color))
        {
            color = Color.FromRgb(color.R, color.G, color.B);
            return true;
        }

        color = default;
        return false;
    }
}
